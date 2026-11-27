#ifndef COORDINA_GRAPH_ENGINE_ABI_QUERIES_H
#define COORDINA_GRAPH_ENGINE_ABI_QUERIES_H

#include "coordina_graph_engine.h"
#include "core/graph_snapshot.h"
#include "interop/abi_errors.h"
#include "interop/native_bridge.h"
#include "planning/work_planner.h"

#include <cstddef>
#include <vector>

// What every exported function does first: check the pointers it was given,
// build the graph, and turn the work items into the engine's.
namespace coordina::graph::abi
{
template <typename Result, typename Query>
Result query_graph(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  Result invalid,
  Query query)
{
  return guarded_value(
    invalid,
    [&]
    {
      if (is_missing(nodes, node_count) || is_missing(edges, edge_count))
      {
        set_error("An array is null but its count is not 0.");
        return invalid;
      }

      return static_cast<Result>(query(build_snapshot(nodes, node_count, edges, edge_count)));
    });
}

template <typename Result, typename Query>
Result query_work(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  const coordina_graph_work_item* work_items,
  std::size_t work_item_count,
  Result invalid,
  Query query)
{
  if (is_missing(work_items, work_item_count))
  {
    set_error("An array is null but its count is not 0.");
    return invalid;
  }

  return query_graph(
    nodes,
    node_count,
    edges,
    edge_count,
    invalid,
    [&](const GraphSnapshot& graph)
    {
      return query(graph, build_work_items(work_items, work_item_count));
    });
}

inline bool fits(std::size_t required, const void* items, std::size_t capacity) noexcept
{
  return required == 0U || (items != nullptr && capacity >= required);
}
}

#endif
