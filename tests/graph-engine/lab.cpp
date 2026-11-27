// Measures how good the engine's answers are, not how fast: whether the dates
// it announces are met as often as it says, how close its plans come to what
// is possible, and whether its advice beats chance. Teams are made up, so the
// truth is known. Run it with `make graph-engine-lab` before and after changing
// an algorithm; it checks nothing by itself.

#include "lab_world.h"
#include "planning/forecast.h"
#include "planning/schedule.h"
#include "planning/staffing.h"

#include <algorithm>
#include <array>
#include <bit>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <utility>
#include <vector>

namespace
{
using namespace coordina::graph;
using namespace coordina::graph::lab;

constexpr std::uint32_t lab_runs = 1000U;

SimulationOptions simulation()
{
  return SimulationOptions{.seed = 129U, .run_count = lab_runs, .thread_count = 0U};
}

// Ranked probability score of a date: how far the whole distribution is from
// what happened, in days. Lower is better.
double pinball(double quantile, double share, double truth)
{
  return truth > quantile ? share * (truth - quantile) : (1.0 - share) * (quantile - truth);
}

void measure_dates(
  const char* name,
  const World& world,
  std::uint32_t history,
  double unsized_share)
{
  constexpr std::uint64_t worlds = 300U;
  auto within_p50 = 0.0;
  auto within_p85 = 0.0;
  auto within_p95 = 0.0;
  auto loss = 0.0;
  auto margin = 0.0;

  for (std::uint64_t seed = 1U; seed <= worlds; ++seed)
  {
    const auto lab = make_board(
      BoardShape{.cards = 40U, .people = 3U, .assigned_share = 0.6, .unsized_share = unsized_share},
      world,
      history,
      seed);
    const PlannedBoard planned(lab.board, simulation());
    const auto milestone = planned.forecast_now().milestones.front();
    const auto truth = true_finish_days(lab, planned.model());
    within_p50 += truth <= milestone.p50_days ? 1.0 : 0.0;
    within_p85 += truth <= milestone.p85_days ? 1.0 : 0.0;
    within_p95 += truth <= milestone.p95_days ? 1.0 : 0.0;
    loss += pinball(milestone.p85_days, 0.85, truth);
    margin += milestone.p85_days - milestone.p50_days;
  }

  std::printf(
    "  %-34s history %3u | met: P50 %2.0f%%  P85 %2.0f%%  P95 %2.0f%% | P85 loss %.2f d | margin "
    "%.1f d\n",
    name,
    history,
    100.0 * within_p50 / worlds,
    100.0 * within_p85 / worlds,
    100.0 * within_p95 / worlds,
    loss / worlds,
    margin / worlds);
}

void measure_all_dates()
{
  std::puts("Dates: how often the announced date is met (50, 85 and 95% expected)");

  for (const auto history : {0U, 10U, 40U, 150U})
  {
    measure_dates("as the prior says", World{}, history, 0.0);
  }

  for (const auto history : {0U, 10U, 40U, 150U})
  {
    measure_dates("estimates 40% too low", World{.bias = std::log(1.4)}, history, 0.0);
  }

  for (const auto history : {40U, 150U})
  {
    measure_dates("the team changes by 20%", World{.drift_spread = 0.2}, history, 0.0);
  }

  for (const auto history : {0U, 40U, 150U})
  {
    measure_dates("5% disasters (3x to 6x)", World{.disaster_share = 0.05}, history, 0.0);
  }

  for (const auto history : {0U, 40U})
  {
    measure_dates("half the cards unsized", World{}, history, 0.5);
  }
}

// No plan beats this: the longest chain, and the work shared out evenly.
double floor_days(const PlanModel& model, double chain_days)
{
  const auto& hours = model.planned_hours();
  std::vector<double> loads(model.person_count(), 0.0);
  auto total = 0.0;
  auto team = 0.0;

  for (const auto word : model.team())
  {
    team += std::popcount(word);
  }

  for (std::size_t card = 0U; card < model.card_count(); ++card)
  {
    total += hours[card];

    if (model.people(card).size() == 1U)
    {
      loads[model.people(card).front()] += hours[card];
    }
  }

  const auto load = std::max(total / team, *std::ranges::max_element(loads));
  return std::max(chain_days, load / focus_hours_per_day);
}

// The length of the longest chain when everything takes its median time, as a
// team large enough would work through it.
double chain_days(const PlanningBoard& board)
{
  auto unlimited = board;
  unlimited.assignees.clear();
  unlimited.extra_people = static_cast<std::uint32_t>(board.nodes.size());
  const PlannedBoard planned(unlimited, simulation());
  Scheduler scheduler(planned.model());
  scheduler.run(planned.model().planned_hours());
  return scheduler.finish_of(0U) / focus_hours_per_day;
}

void measure_order()
{
  std::puts("\nPick-up order: days above the lower bound, which no plan beats");

  for (const auto assigned : {0.0, 0.3, 0.6, 1.0})
  {
    for (const auto& [cards, people] :
      {std::pair{40U, 3U}, std::pair{80U, 4U}, std::pair{150U, 6U}})
    {
      constexpr std::uint64_t boards = 40U;
      auto planned_days = 0.0;
      auto bound_days = 0.0;

      for (std::uint64_t seed = 1U; seed <= boards; ++seed)
      {
        const auto lab = make_board(
          BoardShape{.cards = cards, .people = people, .assigned_share = assigned},
          World{},
          0U,
          seed);
        const PlannedBoard planned(lab.board, simulation());
        Scheduler scheduler(planned.model());
        scheduler.run(planned.model().planned_hours());
        planned_days += scheduler.finish_of(0U) / focus_hours_per_day;
        bound_days += floor_days(planned.model(), chain_days(lab.board));
      }

      std::printf(
        "  %3.0f%% assigned, %3u cards, %u people | plan %.1f d, bound %.1f d, gap %.1f%%\n",
        100.0 * assigned,
        cards,
        people,
        planned_days / boards,
        bound_days / boards,
        100.0 * ((planned_days / bound_days) - 1.0));
    }
  }
}

void measure_staffing()
{
  std::puts("\nWho does what: the gain announced against the gain on other draws");
  constexpr std::uint64_t boards = 10U;
  auto announced = 0.0;
  auto confirmed = 0.0;
  auto changes = 0.0;

  for (std::uint64_t seed = 1U; seed <= boards; ++seed)
  {
    const auto lab =
      make_board(BoardShape{.cards = 60U, .people = 4U, .assigned_share = 1.0}, World{}, 20U, seed);
    StaffingOptions options{.milestone = lab_node(milestone_node), .simulation = simulation()};
    options.allow_reassignment = true;
    options.evaluation_limit = 8000U;
    const auto plan = propose_staffing(
      lab.board,
      StaffingTeam{.receivers = {0U, 1U, 2U, 3U}, .skilled = {}},
      options);
    std::vector<Assignee> chosen;
    chosen.reserve(plan.changes.size());

    for (const auto& change : plan.changes)
    {
      chosen.push_back(Assignee{.node = change.node, .person = change.person});
    }

    options.simulation.seed = 9001U;
    const auto again = evaluate_staffing(lab.board, chosen, options);
    announced += plan.p85_days_before - plan.p85_days_after;
    confirmed += again.p85_days_before - again.p85_days_after;
    changes += static_cast<double>(plan.changes.size());
  }

  std::printf(
    "  P85 gain announced %.2f d, on other draws %.2f d, %.1f changes on average\n",
    announced / boards,
    confirmed / boards,
    changes / boards);
}

}

int main()
{
  measure_all_dates();
  measure_order();
  measure_staffing();
  return 0;
}
