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

Scheduler::Scheduler(const ForecastModel& model)
    : model_(&model), cards_(model.card_count()), last_card_(model.person_count()),
      free_people_((model.person_count() + word_bits - 1U) / word_bits), pool_(free_people_.size()),
      own_(model.person_count(), ReadySet(model.card_count())), shared_(model.card_count())
{
  running_.reserve(model.person_count());
}

double Scheduler::run(
  std::span<const double> hours,
  std::span<const std::uint8_t> active,
  const Staffing& staffing)
{
  const auto count = model_->card_count();
  hours_ = hours;
  staff(staffing);
  std::ranges::fill(last_card_, no_card);
  std::ranges::fill(free_people_, ~std::uint64_t{0U});
  running_.clear();
  last_ = none;

  for (std::size_t card = 0U; card < count; ++card)
  {
    cards_[card] = CardState{
      .ready_at = 0.0,
      .start = 0.0,
      .finish = 0.0,
      .remaining = model_->dependency_count(card),
      .person = 0U,
      .ready_cause = no_card,
      .cause = no_card
    };
  }

  if (!active.empty())
  {
    leave_out(active);
  }

  for (std::size_t card = 0U; card < count; ++card)
  {
    if (cards_[card].remaining == 0U)
    {
      release(card);
    }
  }

  start_cards(0.0);

  while (!running_.empty())
  {
    const auto now = running_.front().finish;
    finish_cards(now);
    start_cards(now);
  }

  return last_ == none ? 0.0 : cards_[last_].finish;
}

void Scheduler::driving_chain(std::vector<std::size_t>& chain) const
{
  chain.clear();

  if (last_ == none)
  {
    return;
  }

  for (auto card = static_cast<std::uint32_t>(last_); card != no_card; card = cards_[card].cause)
  {
    chain.push_back(card);
  }

  std::ranges::reverse(chain);
}

Schedule Scheduler::schedule() const
{
  Schedule result;
  result.cards.reserve(model_->card_count());

  for (std::size_t card = 0U; card < model_->card_count(); ++card)
  {
    result.cards.push_back(
      ScheduledCard{
        .node = model_->nodes()[card],
        .person = cards_[card].person,
        .start_hours = cards_[card].start,
        .finish_hours = cards_[card].finish
      });
  }

  driving_chain(result.driving_chain);
  result.finish_hours = last_ == none ? 0.0 : cards_[last_].finish;
  return result;
}

// A card left out never becomes ready, and the cards waiting for it stop
// counting it among their dependencies.
void Scheduler::leave_out(std::span<const std::uint8_t> active) noexcept
{
  for (std::size_t card = 0U; card < active.size(); ++card)
  {
    if (active[card] != 0U)
    {
      continue;
    }

    cards_[card].remaining = left_out;

    for (const auto dependent : model_->dependents(card))
    {
      if (active[dependent] != 0U)
      {
        --cards_[dependent].remaining;
      }
    }
  }
}

void Scheduler::staff(const Staffing& staffing) noexcept
{
  chosen_ = staffing.people;

  if (!staffing.pool.empty())
  {
    std::ranges::fill(pool_, 0U);
    const auto words = std::min(staffing.pool.size(), pool_.size());
    std::ranges::copy(staffing.pool.first(words), pool_.begin());
    return;
  }

  std::ranges::fill(pool_, ~std::uint64_t{0U});

  if (const auto extra = model_->person_count() % word_bits; extra != 0U)
  {
    pool_.back() = (std::uint64_t{1U} << extra) - 1U;
  }
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
  const auto people = this->people(card);

  if (people.empty())
  {
    shared_.insert(card);
    return;
  }

  for (const auto person : people)
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
  auto pool_free = false;

  for (std::size_t word = 0U; word < free_people_.size(); ++word)
  {
    pool_free = pool_free || (free_people_[word] & pool_[word]) != 0U;

    for (auto bits = free_people_[word]; bits != 0U; bits &= bits - 1U)
    {
      const auto person = (word * word_bits) + static_cast<std::size_t>(std::countr_zero(bits));

      if (person >= own_.size())
      {
        break;
      }

      next = std::min(next, own_[person].min());
    }
  }

  return pool_free ? std::min(next, shared_.min()) : next;
}

std::uint32_t Scheduler::person_for(std::size_t card) const noexcept
{
  const auto people = this->people(card);

  if (people.empty())
  {
    for (std::size_t word = 0U; word < free_people_.size(); ++word)
    {
      if (const auto bits = free_people_[word] & pool_[word]; bits != 0U)
      {
        return static_cast<std::uint32_t>(
          (word * word_bits) + static_cast<std::size_t>(std::countr_zero(bits)));
      }
    }
  }

  for (const auto person : people)
  {
    if (is_free(person))
    {
      return person;
    }
  }

  return 0U;
}

void Scheduler::start_cards(double now)
{
  for (auto card = next_card(); card != none; card = next_card())
  {
    const auto person = person_for(card);
    const auto people = this->people(card);

    if (people.empty())
    {
      shared_.erase(card);
    }

    for (const auto assignee : people)
    {
      own_[assignee].erase(card);
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

    if (last_ == none || done.finish > cards_[last_].finish)
    {
      last_ = done.card;
    }

    for (const auto dependent : model_->dependents(done.card))
    {
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
}
