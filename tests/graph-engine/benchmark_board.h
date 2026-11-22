#ifndef COORDINA_GRAPH_ENGINE_TESTS_BENCHMARK_BOARD_H
#define COORDINA_GRAPH_ENGINE_TESTS_BENCHMARK_BOARD_H

#include "coordina_graph_engine.h"

#include <algorithm>
#include <chrono>
#include <cstddef>
#include <cstdint>
#include <limits>
#include <vector>

// A board of any size whose cards cover every planning signal, and the timer
// the benchmark reads.
namespace coordina::graph::benchmarks
{
using ClockType = std::chrono::steady_clock;

struct BoardGraph
{
  std::vector<coordina_graph_node_id> nodes;
  std::vector<coordina_graph_edge> edges;
  std::vector<coordina_graph_work_item> items;
};

inline coordina_graph_node_id node(std::uint64_t value)
{
  return coordina_graph_node_id{0U, value + 1U};
}

inline BoardGraph build_board(std::size_t size)
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
}

#endif
