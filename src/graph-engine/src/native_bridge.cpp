#include "native_bridge.h"

namespace coordina::graph
{
bool has_invalid_pointer(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count) noexcept
{
  return (node_count > 0U && nodes == nullptr)
    || (edge_count > 0U && edges == nullptr);
}

GraphSnapshot build_snapshot(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count)
{
  std::vector<NodeId> native_nodes;
  native_nodes.reserve(node_count);

  for (std::size_t index = 0U; index < node_count; ++index)
  {
    native_nodes.push_back(to_node_id(nodes[index]));
  }

  std::vector<Edge> native_edges;
  native_edges.reserve(edge_count);

  for (std::size_t index = 0U; index < edge_count; ++index)
  {
    native_edges.push_back(Edge{
      to_node_id(edges[index].source),
      to_node_id(edges[index].target)
    });
  }

  return GraphSnapshot(native_nodes, native_edges);
}

std::size_t write_nodes(
  const std::vector<NodeId>& nodes,
  coordina_graph_node_id* output,
  std::size_t output_capacity)
{
  if (output == nullptr || output_capacity == 0U)
  {
    return 0U;
  }

  const auto count = nodes.size() < output_capacity
    ? nodes.size()
    : output_capacity;

  for (std::size_t index = 0U; index < count; ++index)
  {
    output[index] = to_abi_node_id(nodes[index]);
  }

  return count;
}
}
