#ifndef COORDINA_GRAPH_ENGINE_TESTS_BENCHMARK_PLANNING_H
#define COORDINA_GRAPH_ENGINE_TESTS_BENCHMARK_PLANNING_H

#include "benchmark_board.h"
#include "coordina_graph_engine.h"

#include <cstddef>
#include <cstdint>
#include <limits>
#include <numeric>
#include <vector>

// The planning calls the benchmark times on a board: its forecast, what to
// cut, and who does what.
namespace coordina::graph::benchmarks
{
constexpr std::uint32_t forecast_runs = 2000U;
constexpr std::uint32_t forecast_team = 5U;

// Forecasts a release that depends on every card of the board, so its scope
// is the whole board. Assigned cards are spread over a team of five.
struct ForecastResult
{
  coordina_graph_status status;
  coordina_graph_milestone_forecast output;
};

inline BoardGraph with_release(const BoardGraph& board)
{
  auto graph = board;
  const auto release = node(board.nodes.size());
  graph.nodes.push_back(release);

  for (const auto card : board.nodes)
  {
    graph.edges.push_back(coordina_graph_edge{release, card});
  }

  coordina_graph_work_item item{};
  item.node = release;
  item.due_days = std::numeric_limits<std::int32_t>::max();
  item.position = static_cast<std::uint32_t>(board.nodes.size());
  item.size = COORDINA_GRAPH_SIZE_XS;
  graph.items.push_back(item);
  return graph;
}

inline std::vector<coordina_graph_assignee> team_of(const BoardGraph& graph)
{
  std::vector<coordina_graph_assignee> assignees;

  for (std::size_t index = 0U; index < graph.items.size(); ++index)
  {
    if (graph.items[index].assignment != COORDINA_GRAPH_UNASSIGNED)
    {
      assignees.push_back(
        coordina_graph_assignee{
          graph.items[index].node,
          static_cast<std::uint32_t>(index % forecast_team),
          0U
        });
    }
  }

  return assignees;
}

inline ForecastResult run_forecast(const BoardGraph& graph, std::uint32_t thread_count)
{
  const auto assignees = team_of(graph);
  std::vector<coordina_graph_card_forecast> cards(graph.nodes.size());
  ForecastResult result{COORDINA_GRAPH_OK, {}};
  result.output.cards = cards.data();
  result.output.card_capacity = cards.size();
  const coordina_graph_forecast_options
    options{graph.nodes.back(), 42U, forecast_team, forecast_runs, thread_count, 0U};
  result.status = coordina_graph_forecast(
    graph.nodes.data(),
    graph.nodes.size(),
    graph.edges.data(),
    graph.edges.size(),
    graph.items.data(),
    graph.items.size(),
    assignees.data(),
    assignees.size(),
    &options,
    &result.output);
  result.output.cards = nullptr;
  return result;
}

// The API's settings: the whole proposal fits in 450 ms, which leaves room
// under the half second the issue asks for.
constexpr std::uint32_t scope_time_limit_ms = 450U;
constexpr double scope_budget_ms = 500.0;

struct ScopeResult
{
  coordina_graph_status status;
  coordina_graph_scope_cut output;
};

// Asks for the release to ship in 70% of its likely time.
inline ScopeResult run_scope(const BoardGraph& graph, double p50_days)
{
  const auto assignees = team_of(graph);
  std::vector<coordina_graph_node_id> cuts(graph.nodes.size());
  std::vector<coordina_graph_dropped_card> dropped(graph.nodes.size());
  ScopeResult result{COORDINA_GRAPH_OK, {}};
  result.output.cuts = cuts.data();
  result.output.cut_capacity = cuts.size();
  result.output.dropped = dropped.data();
  result.output.dropped_capacity = dropped.size();
  const coordina_graph_scope_options options{
    .milestone = graph.nodes.back(),
    .seed = 42U,
    .target_hours = p50_days * 6.0 * 0.7,
    .goal = 0.85,
    .person_count = forecast_team,
    .run_count = forecast_runs,
    .screen_run_count = 256U,
    .thread_count = 0U,
    .evaluation_limit = 120U,
    .time_limit_ms = scope_time_limit_ms
  };
  result.status = coordina_graph_propose_scope(
    graph.nodes.data(),
    graph.nodes.size(),
    graph.edges.data(),
    graph.edges.size(),
    graph.items.data(),
    graph.items.size(),
    assignees.data(),
    assignees.size(),
    nullptr,
    0U,
    &options,
    &result.output);
  result.output.cuts = nullptr;
  result.output.dropped = nullptr;
  return result;
}

// The API's settings: the whole proposal fits in 800 ms, under the second
// the issue asks for.
constexpr std::uint32_t staffing_team = 8U;
constexpr std::uint32_t staffing_time_limit_ms = 800U;
constexpr double staffing_budget_ms = 1000.0;

struct StaffingResult
{
  coordina_graph_status status;
  coordina_graph_staffing output;
};

// Assigned cards are spread over the first five of eight people, and anyone
// may take any card that is not in progress.
inline StaffingResult run_staffing(const BoardGraph& graph)
{
  const auto assignees = team_of(graph);
  std::vector<std::uint32_t> receivers(staffing_team);
  std::iota(receivers.begin(), receivers.end(), 0U);
  std::vector<coordina_graph_staff_change> changes(graph.nodes.size());
  StaffingResult result{COORDINA_GRAPH_OK, {}};
  result.output.changes = changes.data();
  result.output.change_capacity = changes.size();
  const coordina_graph_staffing_options options{
    .milestone = graph.nodes.back(),
    .seed = 42U,
    .person_count = staffing_team,
    .run_count = forecast_runs,
    .screen_run_count = 8U,
    .thread_count = 0U,
    .allow_reassignment = 1U,
    .evaluation_limit = 20000U,
    .time_limit_ms = staffing_time_limit_ms,
    .reserved = 0U
  };
  result.status = coordina_graph_propose_staffing(
    graph.nodes.data(),
    graph.nodes.size(),
    graph.edges.data(),
    graph.edges.size(),
    graph.items.data(),
    graph.items.size(),
    assignees.data(),
    assignees.size(),
    receivers.data(),
    receivers.size(),
    nullptr,
    0U,
    &options,
    &result.output);
  result.output.changes = nullptr;
  return result;
}
}

#endif
