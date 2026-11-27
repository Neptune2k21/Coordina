#include "plan_board.h"

#include "planning/board_index.h"
#include "planning/duration_sampler.h"
#include "planning/planned_board.h"

#include <algorithm>
#include <cassert>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <limits>
#include <thread>
#include <vector>

namespace
{
using namespace coordina::graph::fixtures;

// A release that needs two chains of work and a review, shared by two people.
PlanBoard release_board()
{
  PlanBoard board;
  board.people = 2U;
  board.card(1U, COORDINA_GRAPH_SIZE_M);
  board.card(2U, COORDINA_GRAPH_SIZE_L);
  board.card(3U, COORDINA_GRAPH_SIZE_S);
  board.card(4U, COORDINA_GRAPH_SIZE_XL);
  board.card(5U, COORDINA_GRAPH_SIZE_NONE);
  board.card(6U, COORDINA_GRAPH_SIZE_XS);
  board.depends(2U, 1U);
  board.depends(4U, 3U);
  board.depends(5U, 2U);
  board.depends(5U, 4U);
  board.depends(6U, 5U);
  board.assign(1U, 0U);
  board.assign(3U, 1U);
  board.assign(4U, 1U);
  board.milestone(6U, 60.0);
  return board;
}

void forecasts_a_reproducible_range()
{
  const auto board = release_board();
  const auto result = board.forecast();
  const auto& release = result.first();

  assert(result.status == COORDINA_GRAPH_OK);
  assert(release.node.low == 6U);
  assert(release.card_count == 6U && release.unsized_count == 1U);
  assert(0.0 < release.p10_days && release.p10_days < release.p50_days);
  assert(release.p50_days < release.p85_days && release.p85_days < release.p95_days);
  assert(release.on_time > 0.0 && release.on_time < 1.0);
  assert(release.late_days > 0.0);
  // No staffing ends before the longest chain does.
  assert(release.chain_days > 0.0 && release.chain_days <= release.p50_days + 0.2);

  // Every run ends with the milestone, since it waits for all the rest.
  assert(result.cards.size() == 6U && result.card(6U).criticality == 1.0);

  for (std::size_t index = 1U; index < result.cards.size(); ++index)
  {
    assert(result.cards[index - 1U].criticality >= result.cards[index].criticality);
  }

  // The same seed gives the same forecast, card for card.
  const auto again = board.forecast();
  assert(again.first().p50_days == release.p50_days && again.first().p85_days == release.p85_days);

  for (std::size_t index = 0U; index < result.cards.size(); ++index)
  {
    assert(same_node(again.cards[index].node, result.cards[index].node));
    assert(again.cards[index].criticality == result.cards[index].criticality);
    assert(again.cards[index].sensitivity == result.cards[index].sensitivity);
  }

  // Alone, every card is in a row, so every card drives the date in every run.
  const auto alone = board.alone().forecast();

  for (const auto& card : alone.cards)
  {
    assert(card.criticality == 1.0);
  }
}

void plans_who_starts_what_when()
{
  const auto board = release_board();
  const auto result = board.forecast();

  assert(result.plan.size() == 6U);
  assert(result.planned(1U).person == 0U && result.planned(1U).start_days == 0.0);
  assert(result.planned(3U).person == 1U && result.planned(3U).start_days == 0.0);
  assert(result.planned(4U).start_days >= result.planned(3U).finish_days);
  assert(result.planned(6U).start_days >= result.planned(5U).finish_days);

  // Without assignees nobody in particular does the work.
  for (const auto& card : board.alone().forecast().plan)
  {
    assert(card.person >= board.people);
  }
}

void reports_missing_capacity_and_bad_arguments()
{
  const auto board = release_board();
  const auto view = board.view();
  coordina_graph_simulation simulation{1U, 10U, 1U};
  coordina_graph_forecast_output output{};

  assert(
    coordina_graph_forecast(&view, &simulation, &output) == COORDINA_GRAPH_INSUFFICIENT_CAPACITY);
  assert(output.milestone_count == 1U && output.card_count == 6U && output.plan_count == 6U);

  assert(coordina_graph_forecast(nullptr, &simulation, &output) == COORDINA_GRAPH_INVALID_ARGUMENT);
  assert(last_error_says("board"));
  assert(coordina_graph_forecast(&view, nullptr, &output) == COORDINA_GRAPH_INVALID_ARGUMENT);
  simulation.run_count = 0U;
  assert(coordina_graph_forecast(&view, &simulation, &output) == COORDINA_GRAPH_INVALID_ARGUMENT);
  assert(last_error_says("run_count"));
  simulation.run_count = 4000000000U;
  assert(coordina_graph_forecast(&view, &simulation, &output) == COORDINA_GRAPH_INVALID_ARGUMENT);

  auto missing = view;
  missing.milestones = nullptr;
  simulation.run_count = 10U;
  assert(
    coordina_graph_forecast(&missing, &simulation, &output) == COORDINA_GRAPH_INVALID_ARGUMENT);
  assert(last_error_says("null"));

  auto unknown = board;
  unknown.milestone(99U, 10.0);
  assert(unknown.forecast().status == COORDINA_GRAPH_INVALID_ARGUMENT);
  assert(last_error_says("milestone"));

  auto outsider = board;
  outsider.people = 1U;
  assert(outsider.forecast().status == COORDINA_GRAPH_INVALID_ARGUMENT);
  assert(last_error_says("person"));

  auto not_a_number = board;
  not_a_number.milestones.front().target_hours = std::numeric_limits<double>::quiet_NaN();
  assert(not_a_number.forecast().status == COORDINA_GRAPH_INVALID_ARGUMENT);
  assert(last_error_says("finite"));

  auto past = board;
  past.finished(50U, COORDINA_GRAPH_SIZE_M, -3.0);
  assert(past.forecast().status == COORDINA_GRAPH_INVALID_ARGUMENT);

  auto loop = board;
  loop.depends(1U, 6U);
  assert(loop.forecast().status == COORDINA_GRAPH_CYCLE);
  assert(last_error_says("loop"));

  // A call that works clears the error.
  assert(board.forecast().status == COORDINA_GRAPH_OK);
  assert(std::string_view(coordina_graph_last_error()).empty());
}

PlanBoard wide_board(std::uint32_t card_count)
{
  PlanBoard board;
  board.people = 4U;
  const auto release = card_count + 1ULL;

  for (std::uint32_t index = 0U; index < card_count; ++index)
  {
    board.card(index + 1U, 1U + (index % 6U));
    board.depends(release, index + 1U);

    if (index % 3U != 0U)
    {
      board.depends(index + 1U, index);
    }

    if (index % 4U != 0U)
    {
      board.assign(index + 1U, index % 4U);
    }
  }

  board.card(release, COORDINA_GRAPH_SIZE_XS);
  board.milestone(release, 300.0);
  return board;
}

void keeps_the_forecast_when_an_unrelated_card_arrives()
{
  const auto board = wide_board(40U);
  const auto before = board.forecast(500U);

  // A new card comes first and shifts every index, but it is outside the
  // scope, so no card of the scope draws differently.
  auto grown = board;
  grown.card(500U, COORDINA_GRAPH_SIZE_XL).priority = 3U;
  std::ranges::rotate(grown.nodes, grown.nodes.end() - 1);
  std::ranges::rotate(grown.items, grown.items.end() - 1);
  const auto after = grown.forecast(500U);

  assert(before.first().p50_days == after.first().p50_days);
  assert(before.first().p85_days == after.first().p85_days);
  assert(before.cards.size() == after.cards.size());

  for (std::size_t index = 0U; index < before.cards.size(); ++index)
  {
    assert(same_node(before.cards[index].node, after.cards[index].node));
    assert(before.cards[index].criticality == after.cards[index].criticality);
  }
}

void gives_the_same_forecast_with_any_thread_count()
{
  const auto board = wide_board(80U);
  const auto alone = board.forecast(2000U, 1U);
  const auto shared = board.forecast(2000U, 7U);

  assert(alone.first().p50_days == shared.first().p50_days);
  assert(alone.first().p85_days == shared.first().p85_days);
  assert(alone.first().late_days == shared.first().late_days);

  for (std::size_t index = 0U; index < alone.cards.size(); ++index)
  {
    assert(same_node(alone.cards[index].node, shared.cards[index].node));
    assert(alone.cards[index].criticality == shared.cards[index].criticality);
    assert(alone.cards[index].sensitivity == shared.cards[index].sensitivity);
  }
}

void answers_several_callers_at_once()
{
  const auto board = wide_board(80U);
  const auto expected = board.forecast(2000U, 1U).first().p85_days;
  std::vector<double> dates(8U, 0.0);
  std::vector<std::jthread> callers;
  callers.reserve(dates.size());

  for (auto& date : dates)
  {
    callers.emplace_back(
      [&board, &date]
      {
        date = board.forecast(2000U, 0U).first().p85_days;
      });
  }

  callers.clear();
  assert(
    std::ranges::all_of(
      dates,
      [expected](double date)
      {
        return date == expected;
      }));
}

void measures_how_much_each_card_moves_the_date()
{
  // Two people: one on an XXL card, the other on an XS card; the release
  // waits for both, so only the XXL card's duration moves the date.
  PlanBoard board;
  board.people = 2U;
  board.card(1U, COORDINA_GRAPH_SIZE_XXL);
  board.card(2U, COORDINA_GRAPH_SIZE_XS);
  board.card(3U, COORDINA_GRAPH_SIZE_XS);
  board.depends(3U, 1U);
  board.depends(3U, 2U);
  board.assign(1U, 0U);
  board.assign(2U, 1U);
  board.milestone(3U, 100.0);
  const auto result = board.forecast();

  assert(result.card(1U).criticality == 1.0 && result.card(1U).sensitivity > 0.5);
  assert(result.card(2U).criticality < 0.01 && std::abs(result.card(2U).sensitivity) < 0.1);
}

void reports_the_middle_run_as_p50()
{
  // One person works through every card, so each run ends after the sum of
  // its draws, and the median of those sums is the 50% date.
  using namespace coordina::graph;
  const auto abi = release_board().alone();
  PlanningBoard board;
  board.person_count = abi.people;

  for (const auto& item : abi.items)
  {
    board.nodes.push_back(to_node_id(item.node));
    WorkItem native;
    native.node = to_node_id(item.node);
    native.size = to_card_size(item.size);
    native.position = item.position;
    board.items.push_back(native);
  }

  for (const auto& edge : abi.edges)
  {
    board.edges.push_back(
      Edge{.source = to_node_id(edge.source), .target = to_node_id(edge.target)});
  }

  board.milestones.push_back(Milestone{.node = to_node_id(node(6U)), .target_hours = 60.0});
  const PlannedBoard planned(
    board,
    SimulationOptions{.seed = abi.seed, .run_count = 2000U, .thread_count = 1U});
  const DurationSampler sampler(planned.model(), planned.durations(), abi.seed);
  auto shared = sampler.make_draw();
  std::vector<double> hours(planned.model().card_count());
  std::vector<double> finishes;

  for (std::uint64_t run = 0U; run < 2000U; ++run)
  {
    sampler.draw(run, shared, hours);
    auto finish = 0.0;

    for (const auto duration : hours)
    {
      finish += duration;
    }

    finishes.push_back(finish);
  }

  assert(std::abs(abi.forecast().first().p50_days - (quantile_of(finishes, 0.5) / 6.0)) < 1e-9);
}

void widens_the_range_by_what_cards_share()
{
  // Forty cards in a row: if their durations were independent, their slips
  // would cancel out and the 90% date would be within a few percent of the
  // 50% one. Real teams are late on everything at once.
  PlanBoard board;
  board.people = 1U;
  board.card(milestone_id, COORDINA_GRAPH_SIZE_XS);

  for (unsigned long long id = 1U; id <= 40U; ++id)
  {
    board.card(id, COORDINA_GRAPH_SIZE_M);
    board.depends(milestone_id, id);
  }

  board.milestone(milestone_id, 300.0);
  const auto cold = board.forecast().first();
  assert(cold.p85_days > cold.p50_days * 1.2);

  // Forty cards done in the time their size says leave less doubt.
  for (unsigned long long id = 101U; id <= 140U; ++id)
  {
    board.finished(id, COORDINA_GRAPH_SIZE_M, id % 2U == 0U ? 5.0 : 7.0);
    board.assign(id, 0U);
  }

  const auto learned = board.forecast().first();
  assert(learned.p85_days / learned.p50_days < cold.p85_days / cold.p50_days);

  // Done in twice that time, they move the date.
  for (auto& card : board.history)
  {
    card.hours *= 2.0;
  }

  assert(board.forecast().first().p50_days > learned.p50_days * 1.4);
}

void is_less_sure_of_unsized_cards()
{
  PlanBoard board;
  board.people = 1U;
  board.card(milestone_id, COORDINA_GRAPH_SIZE_XS);
  // Sized cards outside the scope tell what the board's cards are like.
  for (unsigned long long id = 100U; id < 140U; ++id)
  {
    board.card(id, 1U + static_cast<std::uint32_t>(id % 5U));
  }

  for (unsigned long long id = 1U; id <= 6U; ++id)
  {
    board.card(id, COORDINA_GRAPH_SIZE_M);
    board.depends(milestone_id, id);
  }

  board.milestone(milestone_id, 300.0);
  const auto sized = board.forecast().first();

  for (unsigned long long id = 1U; id <= 6U; ++id)
  {
    board.item(id).size = COORDINA_GRAPH_SIZE_NONE;
  }

  const auto unsized = board.forecast().first();
  assert(unsized.unsized_count == 6U);
  assert(unsized.p85_days - unsized.p50_days > (sized.p85_days - sized.p50_days) * 1.2);
}

void counts_the_work_found_on_the_way()
{
  auto board = wide_board(30U);
  const auto steady = board.forecast().first();
  board.discovered = 15U;
  board.done_in_scope = 40U;
  const auto growing = board.forecast().first();

  // 15 found for 40 done: about 0.3 new card per card.
  assert(growing.p50_days > steady.p50_days * 1.1);
  assert(growing.p85_days > steady.p85_days * 1.1);
}

void forecasts_milestones_that_share_a_team_together()
{
  PlanBoard board;
  board.people = 1U;

  for (unsigned long long id = 1U; id <= 20U; ++id)
  {
    board.card(id, COORDINA_GRAPH_SIZE_M);
    board.assign(id, 0U);
  }

  board.card(101U, COORDINA_GRAPH_SIZE_XS);
  board.card(102U, COORDINA_GRAPH_SIZE_XS);

  for (unsigned long long id = 1U; id <= 20U; ++id)
  {
    board.depends(id <= 10U ? 101U : 102U, id);
  }

  board.milestone(102U, 120.0);
  const auto alone = board.forecast();
  board.milestones.insert(
    board.milestones.begin(),
    coordina_graph_milestone{node(101U), 60.0, 1.0});
  const auto both = board.forecast();

  assert(both.milestones.size() == 2U);
  assert(both.milestones[0U].node.low == 101U && both.milestones[1U].node.low == 102U);
  // The one due first is done first, and the other waits for the same person.
  assert(both.milestones[0U].p50_days < both.milestones[1U].p50_days);
  assert(both.milestones[1U].p50_days > alone.first().p50_days * 1.7);
  assert(both.milestones[0U].card_count == 11U && both.milestones[1U].card_count == 11U);

  // Each milestone lists its own cards.
  for (const auto& card : both.cards)
  {
    const auto is_first = card.node.low <= 10U || card.node.low == 101U;
    assert((card.milestone == 0U) == is_first);
  }
}

void forecasts_a_done_milestone_as_done()
{
  auto board = release_board();
  board.item(6U).is_completed = 1U;
  const auto result = board.forecast();

  assert(result.status == COORDINA_GRAPH_OK);
  assert(result.first().p85_days == 0.0 && result.first().on_time == 1.0);
  assert(result.first().card_count == 0U && result.cards.empty() && result.plan.empty());

  // A board without milestones has nothing to forecast.
  board.milestones.clear();
  const auto none = board.forecast();
  assert(none.status == COORDINA_GRAPH_OK && none.milestones.empty());
}
}

int main()
{
  forecasts_a_reproducible_range();
  plans_who_starts_what_when();
  reports_missing_capacity_and_bad_arguments();
  keeps_the_forecast_when_an_unrelated_card_arrives();
  gives_the_same_forecast_with_any_thread_count();
  answers_several_callers_at_once();
  measures_how_much_each_card_moves_the_date();
  reports_the_middle_run_as_p50();
  widens_the_range_by_what_cards_share();
  is_less_sure_of_unsized_cards();
  counts_the_work_found_on_the_way();
  forecasts_milestones_that_share_a_team_together();
  forecasts_a_done_milestone_as_done();

  return 0;
}
