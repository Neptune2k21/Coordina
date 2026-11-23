#include "planning/staff_search.h"

#include <algorithm>
#include <cmath>

namespace coordina::graph
{
namespace
{
constexpr auto keep = Staffing::keep_assignees;

// Every change costs a minute, so of two plans that finish together the one
// with fewer changes wins.
constexpr double change_hours = 1.0 / 60.0;

// Giving a card to someone who has not done cards like it costs two focus
// hours: it only happens when that ships the milestone sooner than that.
constexpr double unskilled_hours = 2.0;

// Half the moves change a card on the chain that sets the date.
constexpr double driving_share = 0.5;

// The temperature falls to this share of where it starts.
constexpr double final_cooling = 1e-3;
}

void StaffSearch::set_bit(std::vector<std::uint64_t>& words, std::uint32_t person) noexcept
{
  words[person / word_bits] |= std::uint64_t{1U} << (person % word_bits);
}

std::span<const std::uint32_t> StaffSearch::people_of(
  const Plan& plan,
  std::size_t card) const noexcept
{
  if (plan.people[card] == keep)
  {
    return model_->people(card);
  }

  return std::span(plan.people).subspan(card, 1U);
}

// Giving a card to its only assignee keeps it as it is.
std::uint32_t StaffSearch::normalized(std::size_t card, std::uint32_t person) const noexcept
{
  const auto people = model_->people(card);
  return people.size() == 1U && people.front() == person ? keep : person;
}

// A card nobody has done cards like suits anyone.
bool StaffSearch::is_skilled(std::size_t card, std::uint32_t person) const noexcept
{
  const auto first = skilled_.begin() + skilled_offsets_[card];
  const auto last = skilled_.begin() + skilled_offsets_[card + 1U];
  return first == last || std::binary_search(first, last, person);
}

// A card can move when nobody is working on it, the options let it leave its
// assignees, and some receiver would change who takes it.
bool StaffSearch::can_move(std::size_t card) const noexcept
{
  if (in_progress_[card] != 0U || (!model_->people(card).empty() && !options_->allow_reassignment))
  {
    return false;
  }

  return std::ranges::any_of(
    receivers_,
    [&](std::uint32_t person)
    {
      return normalized(card, person) != keep;
    });
}

void StaffSearch::give(Plan& plan, std::size_t card, std::uint32_t person) const noexcept
{
  const auto previous = plan.people[card];

  if (previous == person)
  {
    return;
  }

  for (const auto holder : people_of(plan, card))
  {
    --plan.held[holder];
  }

  if (previous != keep)
  {
    --plan.changes;
    plan.unskilled -= is_skilled(card, previous) ? 0U : 1U;
  }

  plan.people[card] = person;

  for (const auto holder : people_of(plan, card))
  {
    ++plan.held[holder];
  }

  if (person != keep)
  {
    ++plan.changes;
    plan.unskilled += is_skilled(card, person) ? 0U : 1U;
  }
}

// The people who take unassigned cards: everyone assigned to an open card,
// or nobody in particular when no one is.
void StaffSearch::fill_pool(const Plan& plan, std::vector<std::uint64_t>& pool) const noexcept
{
  std::ranges::copy(elsewhere_, pool.begin());

  for (std::uint32_t person = 0U; person < nobody_; ++person)
  {
    if (plan.held[person] > 0U)
    {
      set_bit(pool, person);
    }
  }

  if (std::ranges::all_of(
        pool,
        [](std::uint64_t word)
        {
          return word == 0U;
        }))
  {
    set_bit(pool, nobody_);
  }
}

// The mean finish over the screening runs, plus what the changes cost. The
// scheduler is left on the last run.
double StaffSearch::cost(Runner& runner)
{
  fill_pool(runner.plan, runner.pool);
  const Staffing staffing{.people = runner.plan.people, .pool = runner.pool};
  const auto cards = model_->card_count();
  const auto runs = options_->screen_run_count;
  auto total = 0.0;

  for (std::size_t run = 0U; run < runs; ++run)
  {
    total +=
      runner.scheduler.run(std::span(screen_hours_).subspan(run * cards, cards), {}, staffing);
  }

  return (total / runs) + (change_hours * runner.plan.changes)
    + (unskilled_hours * runner.plan.unskilled);
}

ScopeSample StaffSearch::measure(Runner& runner)
{
  fill_pool(runner.plan, runner.pool);
  return simulator_.run(
    active_,
    options_->run_count,
    nullptr,
    Staffing{.people = runner.plan.people, .pool = runner.pool});
}

// Unassigned cards go to whoever takes them when every receiver pitches in,
// a start far from the current plan when idle people could help.
void StaffSearch::start_from_everyone(Runner& runner)
{
  fill_pool(runner.plan, runner.pool);
  runner.pool[nobody_ / word_bits] &= ~(std::uint64_t{1U} << (nobody_ % word_bits));

  for (const auto person : receivers_)
  {
    set_bit(runner.pool, person);
  }

  runner.scheduler.run(likely_, {}, Staffing{.people = runner.plan.people, .pool = runner.pool});
  const auto schedule = runner.scheduler.schedule();

  for (const auto card : movable_)
  {
    if (const auto person = schedule.cards[card].person;
      model_->people(card).empty() && is_receiver_[person] != 0U)
    {
      give(runner.plan, card, person);
    }
  }
}

std::size_t StaffSearch::draw_card(Runner& runner) const
{
  if (!runner.driving.empty() && runner.random.unit() < driving_share)
  {
    if (const auto card = runner.driving[runner.random.below(runner.driving.size())];
      movable_flags_[card] != 0U)
    {
      return card;
    }
  }

  return movable_[runner.random.below(movable_.size())];
}

// Any receiver, or the card's own assignees, other than who takes it now.
std::uint32_t StaffSearch::draw_person(Runner& runner, std::size_t card) const
{
  const auto current = runner.plan.people[card];

  for (;;)
  {
    const auto drawn = runner.random.below(receivers_.size() + 1U);
    const auto person = drawn == receivers_.size() ? keep : normalized(card, receivers_[drawn]);

    if (person != current)
    {
      return person;
    }
  }
}

// One chain: moves that cost less are kept, and those that cost more are
// kept less and less often as the temperature falls.
void StaffSearch::anneal(Runner& runner, std::size_t chain, Clock::duration budget)
{
  const auto limit = std::max<std::size_t>(1U, options_->evaluation_limit / chain_count);
  const auto started = Clock::now();
  runner.plan = keep_;
  runner.random = SplitMix(options_->seed + chain);

  if (chain % 2U == 1U)
  {
    start_from_everyone(runner);
  }

  auto current = cost(runner);
  runner.scheduler.driving_chain(runner.driving);
  bests_[chain] = runner.plan;
  best_costs_[chain] = current;

  for (std::size_t step = 0U; step < limit; ++step)
  {
    auto progress = static_cast<double>(step) / static_cast<double>(limit);

    if (budget.count() > 0)
    {
      const auto elapsed = Clock::now() - started;

      if (elapsed >= budget)
      {
        break;
      }

      progress = std::max(
        progress,
        std::chrono::duration<double>(elapsed) / std::chrono::duration<double>(budget));
    }

    const auto temperature = temperature_ * std::pow(final_cooling, progress);
    const auto card = draw_card(runner);
    const auto previous = runner.plan.people[card];
    give(runner.plan, card, draw_person(runner, card));
    const auto candidate = cost(runner);

    if (!(candidate > current)
      || runner.random.unit() < std::exp((current - candidate) / temperature))
    {
      current = candidate;
      runner.scheduler.driving_chain(runner.driving);

      if (candidate < best_costs_[chain])
      {
        bests_[chain] = runner.plan;
        best_costs_[chain] = candidate;
      }
    }
    else
    {
      give(runner.plan, card, previous);
    }
  }
}

bool StaffSearch::timed_out() const
{
  return options_->time_limit.count() > 0 && Clock::now() >= deadline_;
}

// Gives back, in card order, every change the plan costs no more without.
// Out of time, the changes left stay: the plan is as good with them.
void StaffSearch::prune(Runner& runner, double& best)
{
  for (const auto card : movable_)
  {
    const auto previous = runner.plan.people[card];

    if (previous == keep)
    {
      continue;
    }

    if (timed_out())
    {
      return;
    }

    give(runner.plan, card, keep);

    if (const auto candidate = cost(runner); candidate > best)
    {
      give(runner.plan, card, previous);
    }
    else
    {
      best = candidate;
    }
  }
}
}
