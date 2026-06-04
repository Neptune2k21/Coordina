#include "coordina_graph_engine.h"

#include <cassert>
#include <cstddef>

namespace
{
coordina_graph_node_id node(unsigned long long value)
{
  return coordina_graph_node_id{0U, value};
}

void detects_cycles()
{
  const coordina_graph_node_id nodes[] = {node(1U), node(2U), node(3U)};
  const coordina_graph_edge edges[] = {
    coordina_graph_edge{node(1U), node(2U)},
    coordina_graph_edge{node(2U), node(3U)}
  };

  const auto status = coordina_graph_evaluate_edge(
    nodes,
    3U,
    edges,
    2U,
    node(3U),
    node(1U));

  assert(status == COORDINA_GRAPH_EDGE_CYCLE);
}

void suggests_only_safe_targets()
{
  const coordina_graph_node_id nodes[] = {
    node(1U),
    node(2U),
    node(3U),
    node(4U)
  };
  const coordina_graph_edge edges[] = {
    coordina_graph_edge{node(1U), node(2U)},
    coordina_graph_edge{node(3U), node(1U)}
  };
  coordina_graph_node_id output[4U] = {};

  const auto count = coordina_graph_suggest_targets(
    nodes,
    4U,
    edges,
    2U,
    node(1U),
    output,
    4U);

  assert(count == 1U);
  assert(output[0U].low == 4U);
}

void sorts_dependency_first()
{
  const coordina_graph_node_id nodes[] = {
    node(1U),
    node(2U),
    node(3U),
    node(4U)
  };
  const coordina_graph_edge edges[] = {
    coordina_graph_edge{node(2U), node(1U)},
    coordina_graph_edge{node(3U), node(2U)},
    coordina_graph_edge{node(4U), node(2U)}
  };
  coordina_graph_node_id output[4U] = {};

  const auto count = coordina_graph_topological_sort(
    nodes,
    4U,
    edges,
    3U,
    output,
    4U);

  assert(count == 4U);
  assert(output[0U].low == 1U);
  assert(output[1U].low == 2U);
  assert(output[2U].low == 3U);
  assert(output[3U].low == 4U);
}

void finds_ready_nodes()
{
  const coordina_graph_node_id nodes[] = {
    node(1U),
    node(2U),
    node(3U)
  };
  const coordina_graph_edge edges[] = {
    coordina_graph_edge{node(2U), node(1U)},
    coordina_graph_edge{node(3U), node(1U)}
  };
  coordina_graph_node_id output[3U] = {};

  const auto count = coordina_graph_ready_nodes(
    nodes,
    3U,
    edges,
    2U,
    output,
    3U);

  assert(count == 1U);
  assert(output[0U].low == 1U);
}

void finds_impacted_dependents()
{
  const coordina_graph_node_id nodes[] = {
    node(1U),
    node(2U),
    node(3U),
    node(4U)
  };
  const coordina_graph_edge edges[] = {
    coordina_graph_edge{node(2U), node(1U)},
    coordina_graph_edge{node(3U), node(2U)},
    coordina_graph_edge{node(4U), node(2U)}
  };
  coordina_graph_node_id output[4U] = {};

  const auto count = coordina_graph_impacted_dependents(
    nodes,
    4U,
    edges,
    3U,
    node(1U),
    output,
    4U);

  assert(count == 3U);
  assert(output[0U].low == 2U);
  assert(output[1U].low == 3U);
  assert(output[2U].low == 4U);
}
}

int main()
{
  detects_cycles();
  suggests_only_safe_targets();
  sorts_dependency_first();
  finds_ready_nodes();
  finds_impacted_dependents();

  return 0;
}
