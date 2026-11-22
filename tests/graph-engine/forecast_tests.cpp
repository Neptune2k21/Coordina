#include "abi_fixtures.h"
#include "coordina_graph_engine.h"
#include "planning/forecast.h"
#include "planning/sampling.h"

#include <algorithm>
#include <cassert>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <vector>

namespace
{
using namespace coordina::graph::fixtures;

struct ForecastBoard
{
  std::vector<coordina_graph_node_id> nodes;
  std::vector<coordina_graph_edge> edges;
  std::vector<coordina_graph_work_item> items;
  std::vector<coordina_graph_assignee> assignees;
};

// A release that needs two chains of work and a review, shared by two people.
ForecastBoard release_board()
{
  ForecastBoard board;
  const std::uint32_t sizes[] = {
    COORDINA_GRAPH_SIZE_M,
    COORDINA_GRAPH_SIZE_L,
    COORDINA_GRAPH_SIZE_S,
    COORDINA_GRAPH_SIZE_XL,
    COORDINA_GRAPH_SIZE_NONE,
    COORDINA_GRAPH_SIZE_XS
  };

  for (std::uint32_t index = 0U; index < 6U; ++index)
  {
    board.nodes.push_back(node(index + 1U));
    board.items.push_back(sized_item(index + 1U, sizes[index], index));
  }

  board.edges = {
    coordina_graph_edge{node(2U), node(1U)},
    coordina_graph_edge{node(4U), node(3U)},
    coordina_graph_edge{node(5U), node(2U)},
    coordina_graph_edge{node(5U), node(4U)},
    coordina_graph_edge{node(6U), node(5U)}
  };
  board.assignees = {
    coordina_graph_assignee{node(1U), 0U, 0U},
    coordina_graph_assignee{node(3U), 1U, 0U},
    coordina_graph_assignee{node(4U), 1U, 0U}
  };
  return board;
}

coordina_graph_status run_forecast(
  const ForecastBoard& board,
  const coordina_graph_forecast_options& options,
  std::vector<coordina_graph_card_forecast>& cards,
  coordina_graph_milestone_forecast& output)
{
  output = coordina_graph_milestone_forecast{};
  output.cards = cards.data();
  output.card_capacity = cards.size();

  return coordina_graph_forecast(
    board.nodes.data(),
    board.nodes.size(),
    board.edges.data(),
    board.edges.size(),
    board.items.data(),
    board.items.size(),
    board.assignees.data(),
    board.assignees.size(),
    &options,
    &output);
}

void forecasts_a_reproducible_range()
{
  const auto board = release_board();
  const coordina_graph_forecast_options options{node(6U), 42U, 2U, 2000U, 0U, 0U};
  std::vector<coordina_graph_card_forecast> cards(6U);
  coordina_graph_milestone_forecast output{};

  assert(run_forecast(board, options, cards, output) == COORDINA_GRAPH_OK);
  assert(output.card_count == 6U);
  assert(output.unsized_count == 1U);
  assert(output.p50_days > 0.0);
  assert(output.p50_days <= output.p85_days);

  // Every run ends with the milestone, since it waits for all the rest.
  assert(cards[0U].node.low == 6U && cards[0U].criticality == 1.0);

  for (std::size_t index = 0U; index < output.card_count; ++index)
  {
    assert(cards[index].criticality >= 0.0 && cards[index].criticality <= 1.0);
    assert(index == 0U || cards[index - 1U].criticality >= cards[index].criticality);
  }

  // The same seed gives the same forecast, card for card.
  std::vector<coordina_graph_card_forecast> again(6U);
  coordina_graph_milestone_forecast repeated{};
  assert(run_forecast(board, options, again, repeated) == COORDINA_GRAPH_OK);
  assert(repeated.p50_days == output.p50_days && repeated.p85_days == output.p85_days);

  for (std::size_t index = 0U; index < output.card_count; ++index)
  {
    assert(same_node(again[index].node, cards[index].node));
    assert(again[index].criticality == cards[index].criticality);
  }

  // Alone, one person does everything in a row: between the sum of the low
  // and the sum of the high durations (unsized counts as M).
  auto alone = board;
  alone.assignees.clear();
  const coordina_graph_forecast_options solo{node(6U), 7U, 1U, 2000U, 0U, 0U};
  assert(run_forecast(alone, solo, cards, output) == COORDINA_GRAPH_OK);

  constexpr auto day = coordina::graph::focus_hours_per_day;
  const auto low = (3.0 + 12.0 + 1.0 + 24.0 + 3.0 + 0.25) / day;
  const auto high = (12.0 + 24.0 + 3.0 + 48.0 + 12.0 + 1.0) / day;
  assert(output.p50_days > low && output.p85_days < high);

  // In a row, every card drives the date in every run.
  for (std::size_t index = 0U; index < output.card_count; ++index)
  {
    assert(cards[index].criticality == 1.0);
  }
}

void forecast_reports_missing_capacity_and_bad_arguments()
{
  const auto board = release_board();
  const coordina_graph_forecast_options options{node(6U), 1U, 2U, 10U, 0U, 0U};
  std::vector<coordina_graph_card_forecast> small(2U);
  coordina_graph_milestone_forecast output{};

  assert(run_forecast(board, options, small, output) == COORDINA_GRAPH_INSUFFICIENT_CAPACITY);
  assert(output.card_count == 6U);
  assert(small[0U].node.low == 0U);

  std::vector<coordina_graph_card_forecast> cards(6U);
  auto unknown = options;
  unknown.milestone = node(99U);
  assert(run_forecast(board, unknown, cards, output) == COORDINA_GRAPH_INVALID_ARGUMENT);

  auto no_runs = options;
  no_runs.run_count = 0U;
  assert(run_forecast(board, no_runs, cards, output) == COORDINA_GRAPH_INVALID_ARGUMENT);

  auto small_team = options;
  small_team.person_count = 1U;
  assert(run_forecast(board, small_team, cards, output) == COORDINA_GRAPH_INVALID_ARGUMENT);

  auto loop = board;
  loop.edges.push_back(coordina_graph_edge{node(1U), node(6U)});
  assert(run_forecast(loop, options, cards, output) == COORDINA_GRAPH_CYCLE);

  assert(
    coordina_graph_forecast(
      board.nodes.data(),
      board.nodes.size(),
      board.edges.data(),
      board.edges.size(),
      board.items.data(),
      board.items.size(),
      nullptr,
      1U,
      &options,
      &output)
    == COORDINA_GRAPH_INVALID_ARGUMENT);
}

ForecastBoard wide_board(std::uint32_t card_count)
{
  ForecastBoard board;
  const auto release = node(card_count + 1U);

  for (std::uint32_t index = 0U; index < card_count; ++index)
  {
    board.nodes.push_back(node(index + 1U));
    board.items.push_back(sized_item(index + 1U, 1U + (index % 6U), index));
    board.edges.push_back(coordina_graph_edge{release, node(index + 1U)});

    if (index % 3U != 0U)
    {
      board.edges.push_back(coordina_graph_edge{node(index + 1U), node(index)});
    }

    if (index % 4U != 0U)
    {
      board.assignees.push_back(coordina_graph_assignee{node(index + 1U), index % 4U, 0U});
    }
  }

  board.nodes.push_back(release);
  board.items.push_back(sized_item(card_count + 1U, COORDINA_GRAPH_SIZE_XS, card_count));
  return board;
}

void keeps_the_forecast_when_an_unrelated_card_arrives()
{
  const auto board = wide_board(40U);
  std::vector<coordina_graph_card_forecast> before(41U);
  coordina_graph_milestone_forecast first{};
  assert(
    run_forecast(board, {node(41U), 3U, 4U, 500U, 1U, 0U}, before, first) == COORDINA_GRAPH_OK);

  // A new card ranks first and shifts every scheduling index, but it is
  // outside the scope, so no card of the scope draws differently.
  auto grown = board;
  grown.nodes.insert(grown.nodes.begin(), node(500U));
  auto urgent = sized_item(500U, COORDINA_GRAPH_SIZE_XL, 0U);
  urgent.priority = 3U;
  urgent.due_days = 0;
  grown.items.insert(grown.items.begin(), urgent);
  std::vector<coordina_graph_card_forecast> after(42U);
  coordina_graph_milestone_forecast second{};
  assert(
    run_forecast(grown, {node(41U), 3U, 4U, 500U, 1U, 0U}, after, second) == COORDINA_GRAPH_OK);

  assert(first.p50_days == second.p50_days && first.p85_days == second.p85_days);
  assert(first.card_count == second.card_count);

  for (std::size_t index = 0U; index < first.card_count; ++index)
  {
    assert(same_node(before[index].node, after[index].node));
    assert(before[index].criticality == after[index].criticality);
  }
}

void gives_the_same_forecast_with_any_thread_count()
{
  const auto board = wide_board(80U);
  std::vector<coordina_graph_card_forecast> alone(81U);
  std::vector<coordina_graph_card_forecast> shared(81U);
  coordina_graph_milestone_forecast first{};
  coordina_graph_milestone_forecast second{};

  assert(
    run_forecast(board, {node(81U), 9U, 4U, 2000U, 1U, 0U}, alone, first) == COORDINA_GRAPH_OK);
  assert(
    run_forecast(board, {node(81U), 9U, 4U, 2000U, 7U, 0U}, shared, second) == COORDINA_GRAPH_OK);
  assert(first.p50_days == second.p50_days && first.p85_days == second.p85_days);

  for (std::size_t index = 0U; index < first.card_count; ++index)
  {
    assert(same_node(alone[index].node, shared[index].node));
    assert(alone[index].criticality == shared[index].criticality);
    assert(alone[index].sensitivity == shared[index].sensitivity);
  }
}

void measures_how_much_each_card_moves_the_date()
{
  // Two people: one on an XXL card, the other on an XS card; the release
  // waits for both, so only the XXL card's duration moves the date.
  ForecastBoard board;
  board.nodes = {node(1U), node(2U), node(3U)};
  board.items = {
    sized_item(1U, COORDINA_GRAPH_SIZE_XXL, 0U),
    sized_item(2U, COORDINA_GRAPH_SIZE_XS, 1U),
    sized_item(3U, COORDINA_GRAPH_SIZE_XS, 2U)
  };
  board.edges = {coordina_graph_edge{node(3U), node(1U)}, coordina_graph_edge{node(3U), node(2U)}};
  board.assignees = {
    coordina_graph_assignee{node(1U), 0U, 0U},
    coordina_graph_assignee{node(2U), 1U, 0U}
  };
  std::vector<coordina_graph_card_forecast> cards(3U);
  coordina_graph_milestone_forecast output{};

  assert(
    run_forecast(board, {node(3U), 5U, 2U, 1000U, 0U, 0U}, cards, output) == COORDINA_GRAPH_OK);

  for (const auto& card : cards)
  {
    if (card.node.low == 1U)
    {
      assert(card.criticality == 1.0 && card.sensitivity > 0.95);
    }

    if (card.node.low == 2U)
    {
      assert(card.criticality == 0.0 && std::fabs(card.sensitivity) < 0.1);
    }
  }
}

void reports_the_middle_run_as_p50()
{
  // One person works through every card, so each run ends after the sum of
  // its draws, and the median of those sums is the 50% date.
  auto board = release_board();
  board.assignees.clear();
  const coordina_graph_forecast_options options{node(6U), 42U, 1U, 2000U, 1U, 0U};
  std::vector<coordina_graph_card_forecast> cards(6U);
  coordina_graph_milestone_forecast output{};
  assert(run_forecast(board, options, cards, output) == COORDINA_GRAPH_OK);

  std::vector<coordina::graph::NodeId> ids;
  std::vector<coordina::graph::Triangular> durations;

  for (const auto& item : board.items)
  {
    ids.push_back(coordina::graph::to_node_id(item.node));
    durations.emplace_back(
      coordina::graph::planned_range(coordina::graph::to_card_size(item.size)));
  }

  const coordina::graph::CardStreams streams(42U, ids);
  std::vector<double> finishes;

  for (std::uint64_t run = 0U; run < 2000U; ++run)
  {
    auto finish = 0.0;

    for (std::size_t card = 0U; card < ids.size(); ++card)
    {
      finish += durations[card](streams.unit(card, run));
    }

    finishes.push_back(finish);
  }

  const auto median = finishes.begin() + 999;
  std::ranges::nth_element(finishes, median);
  assert(std::abs(output.p50_days - (*median / 6.0)) < 1e-9);
}
}

int main()
{
  forecasts_a_reproducible_range();
  forecast_reports_missing_capacity_and_bad_arguments();
  keeps_the_forecast_when_an_unrelated_card_arrives();
  gives_the_same_forecast_with_any_thread_count();
  measures_how_much_each_card_moves_the_date();
  reports_the_middle_run_as_p50();

  return 0;
}
