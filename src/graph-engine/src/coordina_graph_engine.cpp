#include "coordina_graph_engine.h"

#include "dependency_validator.h"
#include "graph_traversal.h"
#include "native_bridge.h"
#include "planning_engine.h"
#include "suggestion_engine.h"

extern "C" const char* coordina_graph_version(void)
{
  return "coordina-graph-engine/0.2.0";
}

extern "C" coordina_graph_edge_status coordina_graph_evaluate_edge(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  coordina_graph_node_id source,
  coordina_graph_node_id target)
{
  if (coordina::graph::has_invalid_pointer(nodes, node_count, edges, edge_count))
  {
    return COORDINA_GRAPH_EDGE_INVALID_ARGUMENT;
  }

  const auto graph = coordina::graph::build_snapshot(
    nodes,
    node_count,
    edges,
    edge_count);

  return coordina::graph::evaluate_edge(
    graph,
    coordina::graph::to_node_id(source),
    coordina::graph::to_node_id(target));
}

extern "C" int coordina_graph_has_path(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  coordina_graph_node_id source,
  coordina_graph_node_id target)
{
  if (coordina::graph::has_invalid_pointer(nodes, node_count, edges, edge_count))
  {
    return -1;
  }

  const auto graph = coordina::graph::build_snapshot(
    nodes,
    node_count,
    edges,
    edge_count);

  return coordina::graph::has_path(
    graph,
    coordina::graph::to_node_id(source),
    coordina::graph::to_node_id(target))
      ? 1
      : 0;
}

extern "C" std::size_t coordina_graph_suggest_targets(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  coordina_graph_node_id source,
  coordina_graph_node_id* output,
  std::size_t output_capacity)
{
  if (coordina::graph::has_invalid_pointer(nodes, node_count, edges, edge_count))
  {
    return 0U;
  }

  const auto graph = coordina::graph::build_snapshot(
    nodes,
    node_count,
    edges,
    edge_count);
  const auto suggestions = coordina::graph::suggest_targets(
    graph,
    coordina::graph::to_node_id(source),
    output_capacity);

  return coordina::graph::write_nodes(suggestions, output, output_capacity);
}

extern "C" int coordina_graph_is_acyclic(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count)
{
  if (coordina::graph::has_invalid_pointer(nodes, node_count, edges, edge_count))
  {
    return -1;
  }

  const auto graph = coordina::graph::build_snapshot(
    nodes,
    node_count,
    edges,
    edge_count);

  return coordina::graph::is_acyclic(graph) ? 1 : 0;
}

extern "C" std::size_t coordina_graph_topological_sort(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  coordina_graph_node_id* output,
  std::size_t output_capacity)
{
  if (coordina::graph::has_invalid_pointer(nodes, node_count, edges, edge_count))
  {
    return 0U;
  }

  const auto graph = coordina::graph::build_snapshot(
    nodes,
    node_count,
    edges,
    edge_count);
  const auto ordered = coordina::graph::dependency_first_topological_order(graph);

  return coordina::graph::write_nodes(ordered, output, output_capacity);
}

extern "C" std::size_t coordina_graph_ready_nodes(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  coordina_graph_node_id* output,
  std::size_t output_capacity)
{
  if (coordina::graph::has_invalid_pointer(nodes, node_count, edges, edge_count))
  {
    return 0U;
  }

  const auto graph = coordina::graph::build_snapshot(
    nodes,
    node_count,
    edges,
    edge_count);
  const auto ready = coordina::graph::ready_nodes(graph);

  return coordina::graph::write_nodes(ready, output, output_capacity);
}

extern "C" std::size_t coordina_graph_impacted_dependents(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  coordina_graph_node_id dependency,
  coordina_graph_node_id* output,
  std::size_t output_capacity)
{
  if (coordina::graph::has_invalid_pointer(nodes, node_count, edges, edge_count))
  {
    return 0U;
  }

  const auto graph = coordina::graph::build_snapshot(
    nodes,
    node_count,
    edges,
    edge_count);
  const auto impacted = coordina::graph::reachable_dependents(
    graph,
    coordina::graph::to_node_id(dependency));

  return coordina::graph::write_nodes(impacted, output, output_capacity);
}
