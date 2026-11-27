#include "planning/scope_cut.h"

#include "planning/planned_board.h"
#include "planning/scope_search.h"

#include <algorithm>
#include <limits>

namespace coordina::graph
{
namespace
{
constexpr auto no_card = std::numeric_limits<std::size_t>::max();

// The search tunes the cut on the screening runs, so it can stop just short
// of the goal on the others. A few batches measured on every run repair that,
// each kept only if it raises the chance of shipping on time.
constexpr std::size_t repair_limit = 3U;

ScopeCut no_cut(const MilestoneForecast& forecast)
{
  return ScopeCut{
    .error = PlanError::none,
    .cuts = {},
    .dropped = {},
    .on_time_before = forecast.on_time,
    .on_time_after = forecast.on_time,
    .p50_days = forecast.p50_days,
    .p85_days = forecast.p85_days
  };
}

// Searches the milestone's cut, once the board and the milestone are known to
// be fine.
template <typename Solve>
ScopeCut search_scope(const PlanningBoard& board, const ScopeOptions& options, const Solve& solve)
{
  const PlannedBoard planned(board, options.simulation);

  if (planned.error() != PlanError::none)
  {
    return ScopeCut{.error = planned.error(), .cuts = {}, .dropped = {}};
  }

  const auto milestone = planned.find_milestone(options.milestone);

  if (milestone == PlannedBoard::no_milestone)
  {
    return ScopeCut{.error = PlanError::unknown_milestone, .cuts = {}, .dropped = {}};
  }

  if (planned.model().milestones()[milestone].card == PlanMilestone::done)
  {
    return no_cut(planned.forecast_now().milestones[milestone]);
  }

  ScopeSearch search(planned, milestone, options);
  return solve(search);
}
}

ScopeSearch::ScopeSearch(
  const PlannedBoard& planned,
  std::size_t milestone,
  const ScopeOptions& options)
    : planned_(&planned), model_(&planned.model()), options_(&options), milestone_(milestone),
      sampler_(planned.model(), planned.durations(), options.simulation.seed),
      graph_(planned.model(), milestone),
      simulator_(
        planned.model(),
        sampler_,
        milestone,
        options.goal,
        std::max(options.simulation.run_count, options.screen_run_count),
        options.simulation.thread_count),
      values_(model_->card_count(), 0.0), blocked_(model_->card_count(), 0U),
      cut_(model_->card_count(), 0U), dropped_(model_->card_count(), 0U),
      active_(model_->card_count(), 1U), trial_cut_(model_->card_count(), 0U),
      trial_dropped_(model_->card_count(), 0U), trial_active_(model_->card_count(), 1U),
      started_(Clock::now())
{
  index_.reserve(model_->card_count());

  for (std::size_t card = 0U; card < model_->card_count(); ++card)
  {
    index_.emplace(model_->nodes()[card], card);
    values_[card] = priority_weight(model_->item(card).priority);
    blocked_[card] = model_->item(card).is_in_progress ? 1U : 0U;
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

  const auto before = planned_->forecast_now().milestones[milestone_];
  full_run_cost_ = Clock::now() - started_;
  find_cuttable();
  auto screen = measure(options_->screen_run_count);

  if (screen.on_time >= options_->goal && before.on_time >= options_->goal)
  {
    return no_cut(before);
  }

  grow(screen);
  const auto floor = std::min(options_->goal, screen.on_time);
  shrink(floor);
  swap(floor);
  repair();
  auto result = finish(before);

  // Cutting changes the order cards are picked up in, which only the forecast
  // of the cut board counts: a cut that does not help there is not worth it.
  return result.on_time_after > before.on_time ? result : no_cut(before);
}

// Adds batches measured on every run while the goal is not reached.
void ScopeSearch::repair()
{
  auto after = measure(options_->simulation.run_count);

  for (std::size_t batch = 0U;
    batch < repair_limit && after.on_time < options_->goal && !timed_out(full_run_cost_);
    ++batch)
  {
    const auto kept = chosen_;

    if (!grow_batch(after))
    {
      return;
    }

    const auto repaired = measure(options_->simulation.run_count);

    if (!(repaired.on_time > after.on_time))
    {
      chosen_ = kept;
      rebuild();
      return;
    }

    after = repaired;
  }
}

ScopeCut ScopeSearch::evaluate(const std::vector<NodeId>& cuts)
{
  for (const auto node : cuts)
  {
    const auto card = index_of(node);

    if (card != no_card && card != graph_.milestone_card() && graph_.contains(card)
      && std::ranges::find(chosen_, card) == chosen_.end())
    {
      chosen_.push_back(card);
    }
  }

  return finish(planned_->forecast_now().milestones[milestone_]);
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

// Credits each card that leaves to the first cut, in order, that takes it out,
// and forecasts the board without them.
ScopeCut ScopeSearch::finish(const MilestoneForecast& before)
{
  auto result = no_cut(before);
  const auto cuts = roots();

  if (cuts.empty())
  {
    return result;
  }

  std::ranges::fill(trial_cut_, 0U);
  std::ranges::fill(dropped_, 0U);
  std::vector<NodeId> leaving;

  for (const auto card : cuts)
  {
    const auto cut = static_cast<std::uint32_t>(result.cuts.size());
    result.cuts.push_back(model_->nodes()[card]);
    graph_.close_up(card, trial_cut_);
    apply_trial();

    for (std::size_t other = 0U; other < dropped_.size(); ++other)
    {
      if (leaves(other))
      {
        result.dropped.push_back(DroppedCard{.node = model_->nodes()[other], .cut = cut});
        leaving.push_back(model_->nodes()[other]);
      }
    }

    dropped_ = trial_dropped_;
  }

  const auto after =
    planned_->forecast_without(options_->milestone, leaving).milestones[milestone_];
  result.on_time_after = after.on_time;
  result.p50_days = after.p50_days;
  result.p85_days = after.p85_days;
  return result;
}

ScopeCut propose_scope(
  const PlanningBoard& board,
  const std::vector<NodeId>& keep,
  const ScopeOptions& options)
{
  return search_scope(
    board,
    options,
    [&keep](ScopeSearch& search)
    {
      return search.propose(keep);
    });
}

ScopeCut evaluate_scope(
  const PlanningBoard& board,
  const std::vector<NodeId>& cuts,
  const ScopeOptions& options)
{
  return search_scope(
    board,
    options,
    [&cuts](ScopeSearch& search)
    {
      return search.evaluate(cuts);
    });
}
}
