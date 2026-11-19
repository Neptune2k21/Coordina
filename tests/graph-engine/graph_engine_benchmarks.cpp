#include "coordina_graph_engine.h"

#include <algorithm>
#include <chrono>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <limits>
#include <string>
#include <vector>

namespace
{
using ClockType = std::chrono::steady_clock;

struct BoardGraph
{
  std::vector<coordina_graph_node_id> nodes;
  std::vector<coordina_graph_edge> edges;
  std::vector<coordina_graph_work_item> items;
};

coordina_graph_node_id node(std::uint64_t value)
{
  return coordina_graph_node_id{0U, value + 1U};
}

BoardGraph build_board(std::size_t size)
{
  BoardGraph graph;
  graph.nodes.reserve(size);
  graph.items.reserve(size);

  for (std::size_t index = 0U; index < size; ++index)
  {
    graph.nodes.push_back(node(index));

    if (index > 0U && index % 3U != 0U)
    {
      graph.edges.push_back(coordina_graph_edge{node(index), node(index - 1U)});
    }

    if (index > 1U)
    {
      graph.edges.push_back(coordina_graph_edge{node(index), node(index / 2U)});
    }

    coordina_graph_work_item item{};
    item.node = node(index);
    item.is_completed = index % 7U == 0U ? 1U : 0U;
    item.priority = static_cast<std::uint32_t>(index % 4U);
    item.due_days = index % 5U == 0U ? std::numeric_limits<std::int32_t>::max()
                                     : static_cast<std::int32_t>(index % 30U) - 5;
    item.subtask_total = static_cast<std::uint32_t>(index % 4U);
    item.subtask_completed = static_cast<std::uint32_t>(index % 2U);
    item.position = static_cast<std::uint32_t>(index);
    item.size = static_cast<std::uint32_t>(index % 7U);
    item.assignment = static_cast<std::uint32_t>(index % 3U);
    item.is_in_progress = index % 4U == 0U ? 1U : 0U;
    item.is_context_match = index % 5U == 0U ? 1U : 0U;
    graph.items.push_back(item);
  }

  return graph;
}

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

constexpr std::uint32_t forecast_runs = 2000U;
constexpr std::uint32_t forecast_team = 5U;

// Forecasts a release that depends on every card of the board, so its scope
// is the whole board. Assigned cards are spread over a team of five.
struct ForecastResult
{
  coordina_graph_status status;
  coordina_graph_milestone_forecast output;
};

BoardGraph with_release(const BoardGraph& board)
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

ForecastResult run_forecast(const BoardGraph& graph)
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

  std::vector<coordina_graph_card_criticality> cards(graph.nodes.size());
  ForecastResult result{COORDINA_GRAPH_OK, {}};
  result.output.cards = cards.data();
  result.output.card_capacity = cards.size();
  const coordina_graph_forecast_options
    options{graph.nodes.back(), 42U, forecast_team, forecast_runs};
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

template <typename Work>
double median_milliseconds(int runs, Work work)
{
  std::vector<double> samples;
  samples.reserve(static_cast<std::size_t>(runs));

  for (int run = 0; run < runs; ++run)
  {
    const auto started = ClockType::now();
    work();
    const auto elapsed = std::chrono::duration<double, std::milli>(ClockType::now() - started);
    samples.push_back(elapsed.count());
  }

  std::ranges::sort(samples);
  return samples[samples.size() / 2U];
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
    "\n%8s %8s %8s %12s %9s %9s\n",
    "nodes",
    "runs",
    "scope",
    "forecast_ms",
    "p50_days",
    "p85_days");

  for (const auto size : sizes)
  {
    const auto graph = with_release(build_board(size));
    ForecastResult result{COORDINA_GRAPH_OK, {}};
    const auto elapsed = median_milliseconds(
      size <= 1000U ? 3 : 1,
      [&]
      {
        result = run_forecast(graph);
      });

    if (result.status != COORDINA_GRAPH_OK || result.output.p50_days > result.output.p85_days)
    {
      std::fprintf(stderr, "forecast failed with status %d\n", result.status);
      return 1;
    }

    std::printf(
      "%8zu %8u %8zu %12.3f %9.1f %9.1f\n",
      size,
      forecast_runs,
      result.output.card_count,
      elapsed,
      result.output.p50_days,
      result.output.p85_days);
  }

  return 0;
}
