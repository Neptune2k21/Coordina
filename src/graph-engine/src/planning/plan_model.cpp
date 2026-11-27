#include "planning/plan_model.h"

#include "numeric/normal.h"
#include "planning/pick_order.h"

#include <algorithm>
#include <cmath>
#include <numbers>

namespace coordina::graph
{
namespace
{
constexpr std::size_t word_bits = 64U;

// What is left of a card that has outlived its whole range is at least this.
constexpr double least_hours_left = 0.1;
}

PlanModel::PlanModel(
  const PlanningBoard& board,
  const BoardIndex& index,
  const DurationModel& durations)
    : person_count_(board.person_count + board.extra_people + 1U), error_(index.error())
{
  if (error_ != PlanError::none)
  {
    return;
  }

  collect_cards(board, index);
  link_cards(index);
  measure_durations(index, durations);
  order_cards(index);
  gather_team(board, index);
}

PlanError PlanModel::error() const noexcept
{
  return error_;
}

std::size_t PlanModel::card_count() const noexcept
{
  return nodes_.size();
}

std::uint32_t PlanModel::person_count() const noexcept
{
  return person_count_;
}

std::uint32_t PlanModel::nobody() const noexcept
{
  return person_count_ - 1U;
}

const std::vector<NodeId>& PlanModel::nodes() const noexcept
{
  return nodes_;
}

const WorkItem& PlanModel::item(std::size_t card) const noexcept
{
  return items_[card];
}

std::uint32_t PlanModel::dependency_count(std::size_t card) const noexcept
{
  return static_cast<std::uint32_t>(dependencies_.row(card).size());
}

std::span<const std::uint32_t> PlanModel::dependencies(std::size_t card) const noexcept
{
  return dependencies_.row(card);
}

std::span<const std::uint32_t> PlanModel::dependents(std::size_t card) const noexcept
{
  return dependents_.row(card);
}

std::span<const std::uint32_t> PlanModel::people(std::size_t card) const noexcept
{
  return people_.row(card);
}

std::span<const std::uint64_t> PlanModel::team() const noexcept
{
  return team_;
}

const CardDuration& PlanModel::duration(std::size_t card) const noexcept
{
  return durations_[card];
}

std::span<const EffectWeight> PlanModel::effects(std::size_t card) const noexcept
{
  return std::span(effects_).subspan(
    effect_offsets_[card],
    effect_offsets_[card + 1U] - effect_offsets_[card]);
}

const std::vector<double>& PlanModel::planned_hours() const noexcept
{
  return planned_hours_;
}

const std::vector<PlanMilestone>& PlanModel::milestones() const noexcept
{
  return milestones_;
}

bool PlanModel::in_scope(std::size_t milestone, std::size_t card) const noexcept
{
  return scopes_[milestone][card] != 0U;
}

bool PlanModel::is_outside_scopes(std::size_t card) const noexcept
{
  return std::ranges::none_of(
    scopes_,
    [card](const auto& scope)
    {
      return scope[card] != 0U;
    });
}

// The scopes are numbered by board index here, then by card in order_cards.
void PlanModel::collect_cards(const PlanningBoard& board, const BoardIndex& index)
{
  std::vector<std::uint8_t> wanted(index.size(), 0U);

  for (const auto& milestone : board.milestones)
  {
    const auto card = index.find(milestone.node);
    const auto is_open = !index.item(card).is_completed;
    milestones_.push_back(
      PlanMilestone{
        .node = milestone.node,
        .card = is_open ? card : PlanMilestone::done,
        .target_hours = milestone.target_hours,
        .weight = milestone.weight
      });
    scopes_.emplace_back(index.size(), std::uint8_t{0U});

    if (is_open)
    {
      mark_scope(index, milestones_.size() - 1U, card);
    }

    for (std::size_t other = 0U; other < index.size(); ++other)
    {
      wanted[other] |= scopes_.back()[other];
    }
  }

  for (std::uint32_t card = 0U; card < index.size(); ++card)
  {
    const auto& item = index.item(card);
    const auto is_blocked = std::ranges::any_of(
      index.dependencies(card),
      [&index](std::uint32_t dependency)
      {
        return !index.item(dependency).is_completed;
      });

    if (wanted[card] != 0U || (item.is_in_progress && !item.is_completed && !is_blocked))
    {
      cards_.push_back(card);
    }
  }
}

// Work behind a completed card is not needed any more, so the scope stops
// there.
void PlanModel::mark_scope(const BoardIndex& index, std::size_t milestone, std::uint32_t card)
{
  auto& scope = scopes_[milestone];
  auto& counts = milestones_[milestone];
  std::vector<std::uint32_t> pending{card};
  scope[card] = 1U;

  while (!pending.empty())
  {
    const auto next = pending.back();
    pending.pop_back();
    ++counts.card_count;
    counts.unsized_count += index.item(next).size == CardSize::none ? 1U : 0U;

    for (const auto dependency : index.dependencies(next))
    {
      if (scope[dependency] == 0U && !index.item(dependency).is_completed)
      {
        scope[dependency] = 1U;
        pending.push_back(dependency);
      }
    }
  }
}

void PlanModel::link_cards(const BoardIndex& index)
{
  std::vector<std::uint32_t> card_of(index.size(), BoardIndex::unknown);

  for (std::uint32_t card = 0U; card < cards_.size(); ++card)
  {
    card_of[cards_[card]] = card;
  }

  std::vector<std::pair<std::uint32_t, std::uint32_t>> forward;
  std::vector<std::pair<std::uint32_t, std::uint32_t>> backward;
  std::vector<std::pair<std::uint32_t, std::uint32_t>> people;
  nodes_.clear();
  items_.clear();

  for (std::uint32_t card = 0U; card < cards_.size(); ++card)
  {
    nodes_.push_back(index.item(cards_[card]).node);
    items_.push_back(index.item(cards_[card]));

    for (const auto dependency : index.dependencies(cards_[card]))
    {
      if (card_of[dependency] != BoardIndex::unknown)
      {
        forward.emplace_back(card, card_of[dependency]);
        backward.emplace_back(card_of[dependency], card);
      }
    }

    for (const auto person : index.people(cards_[card]))
    {
      people.emplace_back(card, person);
    }
  }

  dependencies_ = IndexRows(cards_.size(), std::move(forward));
  dependents_ = IndexRows(cards_.size(), std::move(backward));
  people_ = IndexRows(cards_.size(), std::move(people));

  for (auto& milestone : milestones_)
  {
    if (milestone.card != PlanMilestone::done)
    {
      milestone.card = card_of[index.find(milestone.node)];
    }
  }
}

void PlanModel::measure_durations(const BoardIndex& index, const DurationModel& durations)
{
  durations_.clear();
  effects_.clear();
  effect_offsets_.assign(1U, 0U);
  planned_hours_.clear();

  for (const auto card : cards_)
  {
    const auto& item = index.item(card);
    const auto range = planned_range(item.size);
    durations_.push_back(
      CardDuration{
        .size = item.size,
        .log_likely = std::log(range.likely_hours),
        .sigma = range.sigma(),
        .elapsed_hours = item.is_in_progress ? item.elapsed_hours : 0.0
      });
    durations.append_effects(index, card, effects_);
    effect_offsets_.push_back(effects_.size());
    planned_hours_.push_back(planned_hours_of(
      durations_.back(),
      std::span(effects_).subspan(effect_offsets_[effect_offsets_.size() - 2U]),
      durations));
  }
}

double PlanModel::planned_hours_of(
  const CardDuration& duration,
  std::span<const EffectWeight> effects,
  const DurationModel& durations)
{
  auto log_hours = 0.0;

  if (duration.size == CardSize::none)
  {
    for (std::size_t size = 1U; size < card_size_count; ++size)
    {
      const auto share = durations.size_mix().at(size);
      const auto known = static_cast<CardSize>(size);

      if (share > 0.0)
      {
        log_hours +=
          share * (std::log(size_range(known).likely_hours) + durations.mean_shift(known, effects));
      }
    }
  }
  else
  {
    log_hours = duration.log_likely + durations.mean_shift(duration.size, effects);
  }

  const auto growth = 1.0 + durations.mean_growth();

  if (!(duration.elapsed_hours > 0.0))
  {
    return std::exp(log_hours) * growth;
  }

  // Past its usual range, a card has more likely run away than nearly ended.
  const auto deviations = (std::log(duration.elapsed_hours) - log_hours) / duration.sigma;

  if (durations.overrun_chance(deviations, duration.sigma) > 0.5)
  {
    const auto median = std::exp(durations.overruns().excess * std::numbers::ln2);
    return std::max(least_hours_left, duration.elapsed_hours * (median - 1.0));
  }

  const auto total =
    std::exp(log_hours + (duration.sigma * normal_quantile_above(deviations, 0.5)));
  return std::max(least_hours_left, total - duration.elapsed_hours);
}

// Numbers the cards in the order people pick them up.
void PlanModel::order_cards(const BoardIndex& index)
{
  const auto order = pick_order(*this);

  if (order.size() != cards_.size())
  {
    error_ = PlanError::cycle;
    return;
  }

  const auto unordered = cards_;
  const auto hours = planned_hours_;
  const auto durations = durations_;
  const auto effects = effects_;
  const auto offsets = effect_offsets_;
  effects_.clear();
  effect_offsets_.assign(1U, 0U);

  for (std::size_t card = 0U; card < order.size(); ++card)
  {
    const auto from = order[card];
    cards_[card] = unordered[from];
    planned_hours_[card] = hours[from];
    durations_[card] = durations[from];
    effects_.insert(
      effects_.end(),
      effects.begin() + static_cast<std::ptrdiff_t>(offsets[from]),
      effects.begin() + static_cast<std::ptrdiff_t>(offsets[from + 1U]));
    effect_offsets_.push_back(effects_.size());
  }

  link_cards(index);

  for (auto& scope : scopes_)
  {
    std::vector<std::uint8_t> ordered;
    ordered.reserve(cards_.size());

    for (const auto card : cards_)
    {
      ordered.push_back(scope[card]);
    }

    scope = std::move(ordered);
  }
}

void PlanModel::gather_team(const PlanningBoard& board, const BoardIndex& index)
{
  team_.assign((person_count_ + word_bits - 1U) / word_bits, 0U);
  auto is_empty = true;

  for (const auto& assignee : board.assignees)
  {
    const auto card = index.find(assignee.node);

    if (card != BoardIndex::unknown && !index.item(card).is_completed)
    {
      team_[assignee.person / word_bits] |= std::uint64_t{1U} << (assignee.person % word_bits);
      is_empty = false;
    }
  }

  for (auto extra = board.person_count; extra < nobody(); ++extra)
  {
    team_[extra / word_bits] |= std::uint64_t{1U} << (extra % word_bits);
  }

  if (is_empty)
  {
    team_[nobody() / word_bits] |= std::uint64_t{1U} << (nobody() % word_bits);
  }
}
}
