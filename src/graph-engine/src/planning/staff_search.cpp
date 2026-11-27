#include "planning/staff_search.h"

#include <algorithm>
#include <cmath>

namespace coordina::graph
{
namespace
{
constexpr auto keep = Staffing::keep_assignees;

// What a change has to bring the date in by. Taking a card off someone costs
// half a day, for the handover and because a plan of forty changes is not one
// anybody applies; giving one nobody has costs a quarter of an hour. Asking
// for that much also keeps the search from drifting among equal plans: on
// random boards it gains a tenth more days than with no cost, with a sixth of
// the changes.
constexpr double handover_hours = 3.0;
constexpr double change_hours = 0.25;

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

// A card of the scope can move when nobody is working on it, the options let
// it leave its assignees, and some receiver would change who takes it.
bool StaffSearch::can_move(std::size_t card) const noexcept
{
  if (!model_->in_scope(milestone_, card) || model_->item(card).is_in_progress
    || (!model_->people(card).empty() && !options_->allow_reassignment))
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

  const auto is_handover = !model_->people(card).empty();

  if (previous != keep)
  {
    --plan.changes;
    plan.handovers -= is_handover ? 1U : 0U;
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
    plan.handovers += is_handover ? 1U : 0U;
    plan.unskilled += is_skilled(card, person) ? 0U : 1U;
  }
}

// The people who take unassigned cards: everyone assigned to an open card,
// or nobody in particular when no one is.
void StaffSearch::fill_team(const Plan& plan, std::vector<std::uint64_t>& team) const noexcept
{
  std::ranges::copy(elsewhere_, team.begin());

  for (std::uint32_t person = 0U; person < nobody_; ++person)
  {
    if (plan.held[person] > 0U)
    {
      set_bit(team, person);
    }
  }

  if (std::ranges::all_of(
        team,
        [](std::uint64_t word)
        {
          return word == 0U;
        }))
  {
    set_bit(team, nobody_);
  }
}

// The mean finish over the screening runs, plus what the changes cost and how
// much they delay the other milestones. The scheduler is left on the last run.
double StaffSearch::cost(Runner& runner)
{
  fill_team(runner.plan, runner.team);
  const Staffing staffing{.people = runner.plan.people, .team = runner.team};
  const auto cards = model_->card_count();
  const auto runs = options_->screen_run_count;
  auto total = 0.0;

  for (std::size_t run = 0U; run < runs; ++run)
  {
    runner.scheduler.run(std::span(screen_hours_).subspan(run * cards, cards), {}, staffing);
    total += runner.scheduler.finish_of(milestone_) + delay_to_others(run, runner.scheduler);
  }

  return (total / runs) + (change_hours * (runner.plan.changes - runner.plan.handovers))
    + (handover_hours * runner.plan.handovers) + (unskilled_hours * runner.plan.unskilled);
}

// Delaying a milestone that has time to spare costs nothing.
double StaffSearch::delay_to_others(std::size_t run, const Scheduler& scheduler) const noexcept
{
  const auto& milestones = model_->milestones();
  auto delay = 0.0;

  for (std::size_t other = 0U; other < milestones.size(); ++other)
  {
    if (other != milestone_)
    {
      const auto allowed = allowed_[(run * milestones.size()) + other];
      delay += std::max(0.0, scheduler.finish_of(other) - allowed) * milestones[other].weight
        / milestones[milestone_].weight;
    }
  }

  return delay;
}

// Unassigned cards go to whoever takes them when every receiver pitches in,
// a start far from the current plan when idle people could help.
void StaffSearch::start_from_everyone(Runner& runner)
{
  fill_team(runner.plan, runner.team);
  runner.team[nobody_ / word_bits] &= ~(std::uint64_t{1U} << (nobody_ % word_bits));

  for (const auto person : receivers_)
  {
    set_bit(runner.team, person);
  }

  runner.scheduler.run(
    model_->planned_hours(),
    {},
    Staffing{.people = runner.plan.people, .team = runner.team});
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
  runner.random = SplitMix(options_->simulation.seed + chain);

  if (chain % 2U == 1U)
  {
    start_from_everyone(runner);
  }

  auto current = cost(runner);
  runner.scheduler.driving_chain(model_->milestones()[milestone_].card, runner.driving);
  bests_[chain] = runner.plan;
  best_costs_[chain] = current;

  for (std::size_t step = 0U; step < limit; ++step)
  {
    auto progress = static_cast<double>(step) / static_cast<double>(limit);

    if (budget.count() > 0)
    {
      const auto elapsed = Clock::now() - started;

      if (elapsed >= budget || timed_out())
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
      runner.scheduler.driving_chain(model_->milestones()[milestone_].card, runner.driving);

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
