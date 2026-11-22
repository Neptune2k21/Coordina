#ifndef COORDINA_GRAPH_ENGINE_TESTS_ABI_FIXTURES_H
#define COORDINA_GRAPH_ENGINE_TESTS_ABI_FIXTURES_H

#include "coordina_graph_engine.h"

#include <cstddef>
#include <cstdint>
#include <initializer_list>
#include <limits>
#include <utility>
#include <vector>

namespace coordina::graph::fixtures
{
inline coordina_graph_node_id node(unsigned long long value)
{
  return coordina_graph_node_id{0U, value};
}

inline bool same_node(coordina_graph_node_id left, coordina_graph_node_id right)
{
  return left.high == right.high && left.low == right.low;
}

inline coordina_graph_work_item work_item(unsigned long long id, std::uint32_t position)
{
  coordina_graph_work_item item{};
  item.node = node(id);
  item.due_days = std::numeric_limits<std::int32_t>::max();
  item.position = position;
  return item;
}

inline coordina_graph_work_item sized_item(
  unsigned long long id,
  std::uint32_t size,
  std::uint32_t position,
  bool is_completed = false)
{
  auto item = work_item(id, position);
  item.size = size;
  item.is_completed = is_completed ? 1U : 0U;
  return item;
}

inline coordina_graph_planning_options options(
  std::uint32_t max_size,
  std::uint32_t prefer_assigned = 1U,
  std::uint32_t include_unassigned = 1U)
{
  return coordina_graph_planning_options{max_size, prefer_assigned, include_unassigned};
}

inline std::vector<coordina_graph_node_id> nodes_of(
  const std::vector<coordina_graph_work_item>& items)
{
  std::vector<coordina_graph_node_id> nodes;
  nodes.reserve(items.size());

  for (const auto& item : items)
  {
    nodes.push_back(item.node);
  }

  return nodes;
}

inline std::vector<unsigned long long> ids_of(const std::vector<coordina_graph_node_id>& nodes)
{
  std::vector<unsigned long long> ids;
  ids.reserve(nodes.size());

  for (const auto& value : nodes)
  {
    ids.push_back(value.low);
  }

  return ids;
}

// Nodes 1 to node_count, and an edge from each card to the card it depends on.
struct Graph
{
  Graph(
    std::size_t node_count,
    std::initializer_list<std::pair<unsigned long long, unsigned long long>> dependencies)
  {
    for (std::size_t index = 1U; index <= node_count; ++index)
    {
      nodes.push_back(node(index));
    }

    for (const auto& [card, dependency] : dependencies)
    {
      edges.push_back(coordina_graph_edge{node(card), node(dependency)});
    }
  }

  Graph(
    std::vector<coordina_graph_node_id> graph_nodes,
    std::vector<coordina_graph_edge> graph_edges)
      : nodes(std::move(graph_nodes)), edges(std::move(graph_edges))
  {
  }

  // Calls a C ABI function that takes the graph as its first four arguments.
  template <typename Function, typename... Arguments>
  auto call(Function function, Arguments... arguments) const
  {
    return function(nodes.data(), nodes.size(), edges.data(), edges.size(), arguments...);
  }

  // The node ids a C ABI function writes to an output buffer as large as the graph.
  template <typename Function, typename... Arguments>
  std::vector<unsigned long long> ids(Function function, Arguments... arguments) const
  {
    std::vector<coordina_graph_node_id> output(nodes.size());
    output.resize(call(function, arguments..., output.data(), output.size()));
    return ids_of(output);
  }

  std::vector<coordina_graph_node_id> nodes;
  std::vector<coordina_graph_edge> edges;
};

using Ids = std::vector<unsigned long long>;
}

#endif
