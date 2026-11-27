#include "planning/staff_search.h"

#include "planning/parallel_runs.h"

#include <algorithm>
#include <limits>
#include <numeric>
#include <utility>

// Setting a staffing search up from the team, and what it answers: the plan
// it proposes, or the measure of changes someone chose.
namespace coordina::graph
{
namespace
{
constexpr auto keep = Staffing::keep_assignees;
constexpr auto no_card = std::numeric_limits<std::size_t>::max();

// The share of the time left that annealing gets; pruning gets the rest.
constexpr double annealing_share = 0.8;

// How late the other milestones are, by their weights against this one's.
double late_days_of_others(const PlanModel& model, const Forecast& forecast, std::size_t milestone)
{
  auto late = 0.0;

  for (std::size_t other = 0U; other < forecast.milestones.size(); ++other)
  {
    if (other != milestone)
    {
      late += forecast.milestones[other].late_days * model.milestones()[other].weight
        / model.milestones()[milestone].weight;
    }
  }

  return late;
}

// Whether the plan ships the milestone sooner, its 85% date first, then its
// 50% one, by more than it delays the others.
bool is_worth_it(const StaffingPlan& plan)
{
  const auto harm = std::max(0.0, plan.others_late_days_after - plan.others_late_days_before);
  const auto gain = plan.p85_days_before - plan.p85_days_after;
  return gain > harm || (!(gain < harm) && plan.p50_days_after < plan.p50_days_before);
}

template <typename Solve>
StaffingPlan search_staffing(
  const PlanningBoard& board,
  const StaffingTeam& team,
  const StaffingOptions& options,
  const Solve& solve)
{
  const PlannedBoard planned(board, options.simulation);

  if (planned.error() != PlanError::none)
  {
    return StaffingPlan{.error = planned.error(), .changes = {}};
  }

  const auto milestone = planned.find_milestone(options.milestone);

  if (milestone == PlannedBoard::no_milestone)
  {
    return StaffingPlan{.error = PlanError::unknown_milestone, .changes = {}};
  }

  if (planned.model().milestones()[milestone].card == PlanMilestone::done)
  {
    return StaffingPlan{};
  }

  StaffSearch search(planned, milestone, team, options);
  return solve(search);
}
}

StaffSearch::Plan::Plan(const PlanModel& model)
    : people(model.card_count(), keep), held(model.person_count(), 0U)
{
}

StaffSearch::Runner::Runner(const PlanModel& model, std::size_t team_words)
    : plan(model), team(team_words, 0U), scheduler(model)
{
  driving.reserve(model.card_count());
}

StaffSearch::StaffSearch(
  const PlannedBoard& planned,
  std::size_t milestone,
  const StaffingTeam& team,
  const StaffingOptions& options)
    : planned_(&planned), model_(&planned.model()), options_(&options), milestone_(milestone),
      nobody_(model_->nobody()), team_words_((model_->person_count() + word_bits - 1U) / word_bits),
      is_receiver_(model_->person_count(), 0U), elsewhere_(team_words_, 0U),
      movable_flags_(model_->card_count(), 0U), keep_(*model_), started_(Clock::now())
{
  index_cards();
  find_busy_elsewhere();
  link_skills(team.skilled);
  draw_screen_runs();
  pick_receivers(team.receivers);
  find_movable();

  const auto& hours = model_->planned_hours();
  // A move changes the finish by about one card's duration.
  temperature_ = std::max(
    std::accumulate(hours.begin(), hours.end(), 0.0) / static_cast<double>(hours.size()),
    0.25);

  const auto draws = static_cast<std::size_t>(options.evaluation_limit) * options.screen_run_count
    * model_->card_count();
  runners_.assign(
    thread_count_for(options.simulation.thread_count, chain_count, draws),
    Runner(*model_, team_words_));
  bests_.assign(chain_count, keep_);
  best_costs_.assign(chain_count, 0.0);
}

// Indexes the cards by node, and counts who holds each card as it is.
void StaffSearch::index_cards()
{
  index_.reserve(model_->card_count());

  for (std::size_t card = 0U; card < model_->card_count(); ++card)
  {
    index_.emplace(model_->nodes()[card], card);

    for (const auto person : model_->people(card))
    {
      ++keep_.held[person];
    }
  }
}

// The people assigned to open cards outside the plan stay on the team
// whatever it does.
void StaffSearch::find_busy_elsewhere()
{
  const BoardIndex board(planned_->board());

  for (std::size_t card = 0U; card < board.size(); ++card)
  {
    if (!board.item(card).is_completed && index_of(board.item(card).node) == no_card)
    {
      for (const auto person : board.people(card))
      {
        set_bit(elsewhere_, person);
      }
    }
  }
}

// The people skilled for each card, sorted: the card's entries are
// skilled_[skilled_offsets_[card], skilled_offsets_[card + 1]).
void StaffSearch::link_skills(const std::vector<Assignee>& skilled)
{
  std::vector<std::pair<std::size_t, std::uint32_t>> skills;

  for (const auto& skill : skilled)
  {
    if (const auto card = index_of(skill.node); card != no_card && skill.person < nobody_)
    {
      skills.emplace_back(card, skill.person);
    }
  }

  std::ranges::sort(skills);
  const auto duplicates = std::ranges::unique(skills);
  skills.erase(duplicates.begin(), duplicates.end());
  skilled_offsets_.assign(model_->card_count() + 1U, 0U);

  for (const auto& [card, person] : skills)
  {
    ++skilled_offsets_[card + 1U];
    skilled_.push_back(person);
  }

  std::partial_sum(skilled_offsets_.begin(), skilled_offsets_.end(), skilled_offsets_.begin());
}

// The screening runs, the first of the forecast's, and when each milestone
// ends in them as things are.
void StaffSearch::draw_screen_runs()
{
  const auto cards = model_->card_count();
  const auto& milestones = model_->milestones();
  const DurationSampler sampler(*model_, planned_->durations(), options_->simulation.seed);
  auto shared = sampler.make_draw();
  Scheduler scheduler(*model_);
  screen_hours_.assign(options_->screen_run_count * cards, 0.0);

  for (std::size_t run = 0U; run < options_->screen_run_count; ++run)
  {
    const auto hours = std::span(screen_hours_).subspan(run * cards, cards);
    sampler.draw(run, shared, hours);
    scheduler.run(hours);

    for (std::size_t milestone = 0U; milestone < milestones.size(); ++milestone)
    {
      allowed_.push_back(
        std::max(scheduler.finish_of(milestone), milestones[milestone].target_hours));
    }
  }
}

void StaffSearch::pick_receivers(const std::vector<std::uint32_t>& receivers)
{
  for (const auto person : receivers)
  {
    if (person < nobody_ && is_receiver_[person] == 0U)
    {
      is_receiver_[person] = 1U;
      receivers_.push_back(person);
    }
  }

  std::ranges::sort(receivers_);
}

void StaffSearch::find_movable()
{
  for (std::size_t card = 0U; card < model_->card_count(); ++card)
  {
    if (can_move(card))
    {
      movable_flags_[card] = 1U;
      movable_.push_back(card);
    }
  }
}

std::size_t StaffSearch::index_of(NodeId node) const
{
  const auto found = index_.find(node);
  return found == index_.end() ? no_card : found->second;
}

StaffingPlan StaffSearch::propose()
{
  const auto before = planned_->forecast_now();

  if (movable_.empty())
  {
    return finish(before, keep_);
  }

  auto budget = Clock::duration::zero();

  if (options_->time_limit.count() > 0)
  {
    // Forecasting the plan found takes about as long as forecasting the
    // current one, with some room to spare.
    const auto now = Clock::now();
    deadline_ = started_ + options_->time_limit - (2 * (now - started_));

    if (deadline_ <= now)
    {
      return finish(before, keep_);
    }

    const auto left = std::chrono::duration<double>(deadline_ - now) * annealing_share;
    budget = std::chrono::duration_cast<Clock::duration>(
      left * static_cast<double>(runners_.size()) / static_cast<double>(chain_count));
  }

  run_chunks(
    std::span(runners_),
    chain_count,
    [&](Runner& runner, std::size_t chain)
    {
      anneal(runner, chain, budget);
    });

  auto& first = runners_.front();
  const auto best_chain =
    static_cast<std::size_t>(std::ranges::min_element(best_costs_) - best_costs_.begin());
  first.plan = bests_[best_chain];
  auto best = best_costs_[best_chain];
  prune(first, best);
  const auto plan = finish(before, first.plan);

  return plan.changes.empty() || is_worth_it(plan) ? plan : finish(before, keep_);
}

StaffingPlan StaffSearch::evaluate(const std::vector<Assignee>& changes)
{
  auto plan = keep_;

  for (const auto& change : changes)
  {
    if (const auto card = index_of(change.node);
      card != no_card && model_->in_scope(milestone_, card) && change.person < nobody_)
    {
      give(plan, card, normalized(card, change.person));
    }
  }

  return finish(planned_->forecast_now(), plan);
}

std::vector<Assignee> StaffSearch::changes_of(const Plan& plan) const
{
  std::vector<Assignee> changes;

  for (std::size_t card = 0U; card < plan.people.size(); ++card)
  {
    if (plan.people[card] != keep)
    {
      changes.push_back(Assignee{.node = model_->nodes()[card], .person = plan.people[card]});
    }
  }

  return changes;
}

StaffingPlan StaffSearch::finish(const Forecast& before, const Plan& plan) const
{
  const auto changes = changes_of(plan);
  const auto after = changes.empty() ? before : planned_->forecast_with(changes);
  StaffingPlan result{
    .error = PlanError::none,
    .changes = {},
    .p50_days_before = before.milestones[milestone_].p50_days,
    .p85_days_before = before.milestones[milestone_].p85_days,
    .p50_days_after = after.milestones[milestone_].p50_days,
    .p85_days_after = after.milestones[milestone_].p85_days,
    .others_late_days_before = late_days_of_others(*model_, before, milestone_),
    .others_late_days_after = late_days_of_others(*model_, after, milestone_)
  };
  const auto start_of = [](const Forecast& forecast, NodeId node)
  {
    const auto found = std::ranges::find(forecast.plan, node, &PlannedCard::node);
    return found == forecast.plan.end() ? 0.0 : found->start_days;
  };

  for (const auto& change : changes)
  {
    result.changes.push_back(
      StaffChange{
        .node = change.node,
        .person = change.person,
        .start_days_before = start_of(before, change.node),
        .start_days_after = start_of(after, change.node)
      });
  }

  std::ranges::stable_sort(
    result.changes,
    [](const StaffChange& left, const StaffChange& right)
    {
      return left.start_days_after < right.start_days_after;
    });

  return result;
}

StaffingPlan propose_staffing(
  const PlanningBoard& board,
  const StaffingTeam& team,
  const StaffingOptions& options)
{
  return search_staffing(
    board,
    team,
    options,
    [](StaffSearch& search)
    {
      return search.propose();
    });
}

StaffingPlan evaluate_staffing(
  const PlanningBoard& board,
  const std::vector<Assignee>& changes,
  const StaffingOptions& options)
{
  return search_staffing(
    board,
    StaffingTeam{},
    options,
    [&changes](StaffSearch& search)
    {
      return search.evaluate(changes);
    });
}
}
