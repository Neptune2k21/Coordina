#include "benchmark_board.h"
#include "benchmark_planning.h"
#include "coordina_graph_engine.h"

#include <algorithm>
#include <chrono>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <string>
#include <vector>

namespace
{
using namespace coordina::graph::benchmarks;

struct Buffers
{
  explicit Buffers(std::size_t size)
      : ready(size), order(size), critical(size), analyses(size), recommendations(size)
  {
  }

  std::vector<coordina_graph_node_id> ready;
  std::vector<coordina_graph_node_id> order;
  std::vector<coordina_graph_node_id> critical;
  std::vector<coordina_graph_node_analysis> analyses;
  std::vector<coordina_graph_work_recommendation> recommendations;
};

std::size_t run_primitive_sequence(
  const BoardGraph& graph,
  const coordina_graph_planning_options& options,
  Buffers& buffers)
{
  const auto node_count = graph.nodes.size();
  const auto edge_count = graph.edges.size();
  auto written = static_cast<std::size_t>(
    coordina_graph_is_acyclic(graph.nodes.data(), node_count, graph.edges.data(), edge_count));
  written += coordina_graph_ready_nodes(
    graph.nodes.data(),
    node_count,
    graph.edges.data(),
    edge_count,
    buffers.ready.data(),
    buffers.ready.size());
  written += coordina_graph_topological_sort(
    graph.nodes.data(),
    node_count,
    graph.edges.data(),
    edge_count,
    buffers.order.data(),
    buffers.order.size());
  written += coordina_graph_analyze_nodes(
    graph.nodes.data(),
    node_count,
    graph.edges.data(),
    edge_count,
    buffers.analyses.data(),
    buffers.analyses.size());
  written += coordina_graph_critical_path(
    graph.nodes.data(),
    node_count,
    graph.edges.data(),
    edge_count,
    graph.items.data(),
    graph.items.size(),
    buffers.critical.data(),
    buffers.critical.size());
  written += coordina_graph_plan_work(
    graph.nodes.data(),
    node_count,
    graph.edges.data(),
    edge_count,
    graph.items.data(),
    graph.items.size(),
    &options,
    buffers.recommendations.data(),
    buffers.recommendations.size());
  return written;
}

coordina_graph_board_analysis batch_output(Buffers& buffers)
{
  coordina_graph_board_analysis output{};
  output.ready_nodes = {buffers.ready.data(), buffers.ready.size(), 0U};
  output.dependency_order = {buffers.order.data(), buffers.order.size(), 0U};
  output.critical_path = {buffers.critical.data(), buffers.critical.size(), 0U};
  output.node_analyses = buffers.analyses.data();
  output.node_analysis_capacity = buffers.analyses.size();
  output.recommendations = buffers.recommendations.data();
  output.recommendation_capacity = buffers.recommendations.size();
  return output;
}

std::size_t run_batch(
  const BoardGraph& graph,
  const coordina_graph_planning_options& options,
  Buffers& buffers)
{
  auto output = batch_output(buffers);
  const auto status = coordina_graph_analyze_board(
    graph.nodes.data(),
    graph.nodes.size(),
    graph.edges.data(),
    graph.edges.size(),
    graph.items.data(),
    graph.items.size(),
    &options,
    &output);

  if (status != COORDINA_GRAPH_OK)
  {
    std::fprintf(stderr, "batch analysis failed with status %d\n", status);
    std::exit(1);
  }

  return output.recommendation_count;
}

bool same_ranking(const BoardGraph& graph, const coordina_graph_planning_options& options)
{
  Buffers primitive(graph.nodes.size());
  Buffers batch(graph.nodes.size());
  const auto primitive_count = coordina_graph_plan_work(
    graph.nodes.data(),
    graph.nodes.size(),
    graph.edges.data(),
    graph.edges.size(),
    graph.items.data(),
    graph.items.size(),
    &options,
    primitive.recommendations.data(),
    primitive.recommendations.size());
  const auto batch_count = run_batch(graph, options, batch);

  if (primitive_count != batch_count)
  {
    return false;
  }

  for (std::size_t index = 0U; index < batch_count; ++index)
  {
    const auto& left = primitive.recommendations[index];
    const auto& right = batch.recommendations[index];

    if (left.node.low != right.node.low || left.score != right.score)
    {
      return false;
    }
  }

  return true;
}

int runs_for(std::size_t size)
{
  if (size <= 100U)
  {
    return 50;
  }

  return size <= 1000U ? 15 : 3;
}
}

int main(int argc, char** argv)
{
  std::vector<std::size_t> sizes;

  for (int index = 1; index < argc; ++index)
  {
    sizes.push_back(static_cast<std::size_t>(std::strtoull(argv[index], nullptr, 10)));
  }

  if (sizes.empty())
  {
    sizes = {100U, 1000U, 10000U};
  }

  const coordina_graph_planning_options options{60U, 1U, 1U};
  std::printf("engine %s\n", coordina_graph_version());
  std::printf(
    "%8s %8s %14s %10s %8s %7s\n",
    "nodes",
    "edges",
    "primitive_ms",
    "batch_ms",
    "speedup",
    "parity");

  for (const auto size : sizes)
  {
    const auto graph = build_board(size);
    Buffers buffers(size);
    volatile std::size_t sink = 0U;
    const auto primitive = median_milliseconds(
      runs_for(size),
      [&]
      {
        sink = sink + run_primitive_sequence(graph, options, buffers);
      });

    const auto batch = median_milliseconds(
      runs_for(size),
      [&]
      {
        sink = sink + run_batch(graph, options, buffers);
      });
    const auto parity = same_ranking(graph, options);

    std::printf(
      "%8zu %8zu %14.3f %10.3f %7.1fx %7s\n",
      size,
      graph.edges.size(),
      primitive,
      batch,
      primitive / batch,
      parity ? "ok" : "FAILED");

    if (!parity)
    {
      return 1;
    }
  }

  std::printf(
    "\n%8s %8s %8s %10s %10s %9s %9s\n",
    "nodes",
    "runs",
    "scope",
    "1_core_ms",
    "all_ms",
    "p50_days",
    "p85_days");

  for (const auto size : sizes)
  {
    const auto graph = with_release(build_board(size));
    ForecastResult result{COORDINA_GRAPH_OK, {}};
    const auto samples = size <= 1000U ? 5 : 1;
    const auto single = median_milliseconds(
      samples,
      [&]
      {
        result = run_forecast(graph, 1U);
      });
    const auto parallel = median_milliseconds(
      samples,
      [&]
      {
        result = run_forecast(graph, 0U);
      });

    if (result.status != COORDINA_GRAPH_OK || result.output.p50_days > result.output.p85_days)
    {
      std::fprintf(stderr, "forecast failed with status %d\n", result.status);
      return 1;
    }

    std::printf(
      "%8zu %8u %8zu %10.3f %10.3f %9.1f %9.1f\n",
      size,
      forecast_runs,
      result.output.card_count,
      single,
      parallel,
      result.output.p50_days,
      result.output.p85_days);
  }

  std::printf(
    "\n%8s %8s %8s %10s %10s %10s %7s\n",
    "nodes",
    "cuts",
    "dropped",
    "on_time",
    "after",
    "scope_ms",
    "budget");

  for (const auto size : sizes)
  {
    const auto graph = with_release(build_board(size));
    const auto forecast = run_forecast(graph, 0U);
    ScopeResult result{COORDINA_GRAPH_OK, {}};
    const auto elapsed = median_milliseconds(
      size <= 1000U ? 3 : 1,
      [&]
      {
        result = run_scope(graph, forecast.output.p50_days);
      });
    // Past 1,000 cards the forecasts before and after alone take longer.
    const auto within_budget = size > 1000U || elapsed < scope_budget_ms;

    if (result.status != COORDINA_GRAPH_OK
      || result.output.on_time_after < result.output.on_time_before)
    {
      std::fprintf(stderr, "scope proposal failed with status %d\n", result.status);
      return 1;
    }

    std::printf(
      "%8zu %8zu %8zu %10.3f %10.3f %10.3f %7s\n",
      size,
      result.output.cut_count,
      result.output.dropped_count,
      result.output.on_time_before,
      result.output.on_time_after,
      elapsed,
      within_budget ? "ok" : "FAILED");

    if (!within_budget)
    {
      return 1;
    }
  }

  std::printf(
    "\n%8s %8s %8s %10s %10s %12s %7s\n",
    "nodes",
    "people",
    "changes",
    "p85_days",
    "after",
    "staffing_ms",
    "budget");

  for (const auto size : sizes)
  {
    const auto graph = with_release(build_board(size));
    StaffingResult result{COORDINA_GRAPH_OK, {}};
    const auto elapsed = median_milliseconds(
      size <= 1000U ? 3 : 1,
      [&]
      {
        result = run_staffing(graph);
      });
    const auto within_budget = size > 1000U || elapsed < staffing_budget_ms;

    if (result.status != COORDINA_GRAPH_OK
      || result.output.p85_days_after > result.output.p85_days_before)
    {
      std::fprintf(stderr, "staffing proposal failed with status %d\n", result.status);
      return 1;
    }

    std::printf(
      "%8zu %8u %8zu %10.1f %10.1f %12.3f %7s\n",
      size,
      staffing_team,
      result.output.change_count,
      result.output.p85_days_before,
      result.output.p85_days_after,
      elapsed,
      within_budget ? "ok" : "FAILED");

    if (!within_budget)
    {
      return 1;
    }
  }

  return 0;
}
