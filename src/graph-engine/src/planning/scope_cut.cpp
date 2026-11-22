#include "planning/scope_cut.h"

#include "planning/scope_search.h"

#include <algorithm>

namespace coordina::graph
{
namespace
{
constexpr auto no_card = ScopeGraph::no_card;

// The search tunes the cut on the screening runs, so it can stop just short
// of the goal on the others. A few batches measured on every run repair that,
// each kept only if it raises the chance of shipping on time.
constexpr std::size_t repair_limit = 3U;

// A high-priority card is worth twice a medium one, and four times a low or
// unprioritised one, for the same hours.
double priority_weight(std::uint32_t priority) noexcept
{
  switch (priority)
  {
    case 3U:
      return 4.0;
    case 2U:
      return 2.0;
    default:
      return 1.0;
  }
}
}

ScopeSearch::ScopeSearch(
  const ForecastModel& model,
  const std::vector<WorkItem>& work_items,
  NodeId milestone,
  const ScopeOptions& options)
    : model_(&model), options_(&options), graph_(model, milestone), simulator_(model, options),
      values_(model.likely_hours()), blocked_(model.card_count(), 0U), cut_(model.card_count(), 0U),
      active_(model.card_count(), 1U), trial_cut_(model.card_count(), 0U),
      trial_active_(model.card_count(), 1U), started_(Clock::now())
{
  index_.reserve(model.card_count());

  for (std::size_t card = 0U; card < model.card_count(); ++card)
  {
    index_.emplace(model.nodes()[card], card);
  }

  for (const auto& item : work_items)
  {
    if (const auto card = index_of(item.node); card != no_card)
    {
      values_[card] *= priority_weight(item.priority);
      blocked_[card] = item.is_in_progress ? 1U : 0U;
    }
  }
}

ScopeCut ScopeSearch::propose(const std::vector<NodeId>& keep)
{
  for (const auto node : keep)
  {
    if (const auto card = index_of(node); card != no_card)
    {
      blocked_[card] = 1U;
    }
  }

  const auto before = simulator_.run(active_, options_->run_count, nullptr);
  full_run_cost_ = Clock::now() - started_;

  if (graph_.milestone() == no_card)
  {
    return finish(before, &before);
  }

  find_cuttable();
  auto screen = measure(options_->screen_run_count);

  if (screen.on_time < options_->goal)
  {
    grow(screen);
    const auto floor = std::min(options_->goal, screen.on_time);
    shrink(floor);
    swap(floor);
  }

  auto after = measure(options_->run_count);

  for (std::size_t repair = 0U;
    repair < repair_limit && after.on_time < options_->goal && !timed_out(full_run_cost_);
    ++repair)
  {
    const auto kept = chosen_;

    if (!grow_batch(after))
    {
      break;
    }

    const auto repaired = measure(options_->run_count);

    if (!(repaired.on_time > after.on_time))
    {
      chosen_ = kept;
      rebuild();
      break;
    }

    after = repaired;
  }

  return finish(before, &after);
}

ScopeCut ScopeSearch::evaluate(const std::vector<NodeId>& cuts)
{
  const auto before = simulator_.run(active_, options_->run_count, nullptr);

  if (graph_.milestone() != no_card)
  {
    for (const auto node : cuts)
    {
      const auto card = index_of(node);

      if (card != no_card && card != graph_.milestone() && graph_.is_reachable(card)
        && std::ranges::find(chosen_, card) == chosen_.end())
      {
        chosen_.push_back(card);
      }
    }
  }

  return finish(before, nullptr);
}

// The chosen cards that no other chosen card takes out, in order.
std::vector<std::size_t> ScopeSearch::roots()
{
  std::vector<std::size_t> kept;

  for (const auto card : chosen_)
  {
    std::ranges::fill(trial_cut_, 0U);

    for (const auto other : chosen_)
    {
      if (other != card)
      {
        graph_.close_up(other, trial_cut_);
      }
    }

    if (trial_cut_[card] == 0U)
    {
      kept.push_back(card);
    }
  }

  return kept;
}

// Credits each card that leaves to the first cut, in order, that takes it
// out. after, when not null, already measures the scope left on every run.
ScopeCut ScopeSearch::finish(const ScopeSample& before, const ScopeSample* after)
{
  ScopeCut result;
  result.on_time_before = before.on_time;
  const auto cuts = roots();
  std::ranges::fill(cut_, 0U);
  std::ranges::fill(active_, 1U);

  for (std::size_t cut = 0U; cut < cuts.size(); ++cut)
  {
    result.cuts.push_back(model_->nodes()[cuts[cut]]);
    graph_.close_up(cuts[cut], cut_);
    graph_.activate(cut_, trial_active_);

    for (std::size_t card = 0U; card < active_.size(); ++card)
    {
      if (active_[card] != 0U && trial_active_[card] == 0U)
      {
        result.dropped.push_back(
          DroppedCard{.node = model_->nodes()[card], .cut = static_cast<std::uint32_t>(cut)});
      }
    }

    active_ = trial_active_;
  }

  auto left = before;

  if (after != nullptr)
  {
    left = *after;
  }
  else if (!cuts.empty())
  {
    left = simulator_.run(active_, options_->run_count, nullptr);
  }

  result.on_time_after = left.on_time;
  result.p50_days = left.p50_hours / focus_hours_per_day;
  result.p85_days = left.p85_hours / focus_hours_per_day;
  return result;
}

ScopeCut propose_scope(
  const ForecastModel& model,
  const std::vector<WorkItem>& work_items,
  NodeId milestone,
  const std::vector<NodeId>& keep,
  const ScopeOptions& options)
{
  if (model.error() != ForecastError::none || options.run_count == 0U
    || options.screen_run_count == 0U)
  {
    return {};
  }

  ScopeSearch search(model, work_items, milestone, options);
  return search.propose(keep);
}

ScopeCut evaluate_scope(
  const ForecastModel& model,
  const std::vector<WorkItem>& work_items,
  NodeId milestone,
  const std::vector<NodeId>& cuts,
  const ScopeOptions& options)
{
  if (model.error() != ForecastError::none || options.run_count == 0U)
  {
    return {};
  }

  ScopeSearch search(model, work_items, milestone, options);
  return search.evaluate(cuts);
}
}
