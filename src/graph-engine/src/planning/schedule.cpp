#include "planning/schedule.h"

#include <algorithm>
#include <bit>
#include <limits>

namespace coordina::graph
{
namespace
{
constexpr auto none = ReadySet::none;
constexpr auto no_card = std::numeric_limits<std::uint32_t>::max();
// Far above any dependency count, so finishing dependencies never brings it to 0.
constexpr auto left_out = std::numeric_limits<std::uint32_t>::max() / 2U;
constexpr std::size_t word_bits = 64U;

constexpr bool finishes_later(
  double left_finish,
  std::size_t left_card,
  double right_finish,
  std::size_t right_card) noexcept
{
  if (left_finish > right_finish)
  {
    return true;
  }

  return !(right_finish > left_finish) && left_card > right_card;
}
}

Scheduler::Scheduler(const PlanModel& model)
    : model_(&model), cards_(model.card_count()), last_card_(model.person_count()),
      own_hours_(model.person_count()),
      free_people_((model.person_count() + word_bits - 1U) / word_bits), team_(model.team()),
      own_(model.person_count(), ReadySet(model.card_count())), shared_(model.card_count()),
      is_milestone_(model.card_count(), 0U)
{
  running_.reserve(model.person_count());

  for (const auto& milestone : model.milestones())
  {
    if (milestone.card != PlanMilestone::done)
    {
      is_milestone_[milestone.card] = 1U;
    }
  }
}

void Scheduler::run(std::span<const double> hours, const Cut& cut, const Staffing& staffing)
{
  reset(hours, cut, staffing);

  for (std::size_t card = 0U; card < cards_.size(); ++card)
  {
    if (cards_[card].remaining == 0U)
    {
      release(card);
    }
  }

  if (milestones_left_ > 0U)
  {
    start_cards(0.0);
  }

  while (!running_.empty() && milestones_left_ > 0U)
  {
    end_ = running_.front().finish;
    finish_cards(end_);

    if (milestones_left_ > 0U)
    {
      start_cards(end_);
    }
  }
}

void Scheduler::reset(std::span<const double> hours, const Cut& cut, const Staffing& staffing)
{
  hours_ = hours;
  cut_ = cut;
  chosen_ = staffing.people;
  team_ = staffing.team.empty() ? model_->team() : staffing.team;
  std::ranges::fill(last_card_, no_card);
  std::ranges::fill(own_hours_, 0.0);
  // Only the people who can take a card matter; the rest of the workspace would
  // be scanned at every step for nothing.
  std::ranges::copy(team_, free_people_.begin());
  running_.clear();
  shared_.clear();
  milestones_left_ = 0U;
  end_ = 0.0;

  for (auto& ready : own_)
  {
    ready.clear();
  }

  for (std::size_t card = 0U; card < cards_.size(); ++card)
  {
    cards_[card] = CardState{
      .ready_at = 0.0,
      .start = std::numeric_limits<double>::infinity(),
      .finish = 0.0,
      .remaining = model_->dependency_count(card),
      .person = 0U,
      .ready_cause = no_card,
      .cause = no_card
    };
    milestones_left_ += is_milestone_[card];
    const auto takers = people(card);

    for (const auto person : takers)
    {
      own_hours_[person] += model_->planned_hours()[card] / static_cast<double>(takers.size());
      free_people_[person / word_bits] |= std::uint64_t{1U} << (person % word_bits);
    }
  }

  if (!cut_.active.empty())
  {
    leave_out();
  }
}

void Scheduler::leave_out() noexcept
{
  for (std::size_t card = 0U; card < cards_.size(); ++card)
  {
    const auto is_out = !is_active(card);

    for (const auto dependent : model_->dependents(card))
    {
      if (is_active(dependent) && (is_out || !waits(dependent, card)))
      {
        --cards_[dependent].remaining;
      }
    }

    if (is_out)
    {
      cards_[card].remaining = left_out;
      milestones_left_ -= is_milestone_[card];
    }
  }
}

bool Scheduler::is_active(std::size_t card) const noexcept
{
  return cut_.active.empty() || cut_.active[card] != 0U;
}

bool Scheduler::waits(std::size_t dependent, std::size_t dependency) const noexcept
{
  return dependent != cut_.milestone_card || cut_.dropped.empty() || cut_.dropped[dependency] == 0U;
}

std::span<const std::uint32_t> Scheduler::people(std::size_t card) const noexcept
{
  if (!chosen_.empty() && chosen_[card] != Staffing::keep_assignees)
  {
    return chosen_.subspan(card, 1U);
  }

  return model_->people(card);
}

void Scheduler::release(std::size_t card) noexcept
{
  const auto takers = people(card);

  if (takers.empty())
  {
    shared_.insert(card);
    return;
  }

  for (const auto person : takers)
  {
    own_[person].insert(card);
  }
}

bool Scheduler::is_free(std::uint32_t person) const noexcept
{
  return ((free_people_[person / word_bits] >> (person % word_bits)) & 1U) != 0U;
}

std::size_t Scheduler::next_card() const noexcept
{
  auto next = none;
  auto team_free = false;

  for (std::size_t word = 0U; word < free_people_.size(); ++word)
  {
    team_free = team_free || (free_people_[word] & team_[word]) != 0U;

    for (auto bits = free_people_[word]; bits != 0U; bits &= bits - 1U)
    {
      const auto person = (word * word_bits) + static_cast<std::size_t>(std::countr_zero(bits));
      next = std::min(next, own_[person].min());
    }
  }

  return team_free ? std::min(next, shared_.min()) : next;
}

std::uint32_t Scheduler::person_for(std::size_t card) const noexcept
{
  auto chosen = no_card;
  const auto consider = [&](std::uint32_t person)
  {
    if (chosen == no_card || own_hours_[person] < own_hours_[chosen])
    {
      chosen = person;
    }
  };
  const auto takers = people(card);

  for (const auto person : takers)
  {
    if (is_free(person))
    {
      consider(person);
    }
  }

  if (!takers.empty())
  {
    return chosen;
  }

  for (std::size_t word = 0U; word < free_people_.size(); ++word)
  {
    for (auto bits = free_people_[word] & team_[word]; bits != 0U; bits &= bits - 1U)
    {
      consider(
        static_cast<std::uint32_t>(
          (word * word_bits) + static_cast<std::size_t>(std::countr_zero(bits))));
    }
  }

  return chosen;
}

void Scheduler::start_cards(double now)
{
  for (auto card = next_card(); card != none; card = next_card())
  {
    start(card, now);
  }
}

void Scheduler::start(std::size_t card, double now)
{
  const auto person = person_for(card);
  const auto takers = people(card);

  if (takers.empty())
  {
    shared_.erase(card);
  }

  for (const auto taker : takers)
  {
    own_[taker].erase(card);
    own_hours_[taker] -= model_->planned_hours()[card] / static_cast<double>(takers.size());
  }

  // A card that was ready before now waited for its person, who has just
  // finished another card; otherwise it waited for its last dependency.
  auto& state = cards_[card];
  state.cause = state.ready_at < now ? last_card_[person] : state.ready_cause;
  free_people_[person / word_bits] &= ~(std::uint64_t{1U} << (person % word_bits));
  state.person = person;
  state.start = now;
  state.finish = now + hours_[card];
  running_.push_back(Running{.finish = state.finish, .card = card});
  std::ranges::push_heap(
    running_,
    [](const Running& left, const Running& right)
    {
      return finishes_later(left.finish, left.card, right.finish, right.card);
    });
}

void Scheduler::finish_cards(double now) noexcept
{
  const auto later = [](const Running& left, const Running& right)
  {
    return finishes_later(left.finish, left.card, right.finish, right.card);
  };

  while (!running_.empty() && !(running_.front().finish > now))
  {
    std::ranges::pop_heap(running_, later);
    const auto done = running_.back();
    running_.pop_back();
    const auto person = cards_[done.card].person;
    free_people_[person / word_bits] |= std::uint64_t{1U} << (person % word_bits);
    last_card_[person] = static_cast<std::uint32_t>(done.card);
    milestones_left_ -= is_milestone_[done.card];

    for (const auto dependent : model_->dependents(done.card))
    {
      if (!waits(dependent, done.card))
      {
        continue;
      }

      auto& waiting = cards_[dependent];

      if (!(waiting.ready_at > done.finish))
      {
        waiting.ready_at = done.finish;
        waiting.ready_cause = static_cast<std::uint32_t>(done.card);
      }

      if (--waiting.remaining == 0U)
      {
        release(dependent);
      }
    }
  }
}

double Scheduler::finish_of(std::size_t milestone) const noexcept
{
  const auto card = model_->milestones()[milestone].card;
  return card == PlanMilestone::done ? 0.0 : cards_[card].finish;
}

void Scheduler::driving_chain(std::size_t card, std::vector<std::size_t>& chain) const
{
  chain.clear();

  for (auto next = static_cast<std::uint32_t>(card); next != no_card; next = cards_[next].cause)
  {
    chain.push_back(next);
  }

  std::ranges::reverse(chain);
}

Schedule Scheduler::schedule() const
{
  Schedule result;
  result.cards.reserve(cards_.size());
  result.finish_hours = end_;

  for (std::size_t card = 0U; card < cards_.size(); ++card)
  {
    const auto& state = cards_[card];
    result.cards.push_back(
      ScheduledCard{
        .node = model_->nodes()[card],
        .person = state.person,
        .start_hours = std::min(state.start, end_),
        .finish_hours = state.start > end_ ? end_ : state.finish
      });
  }

  return result;
}
}
