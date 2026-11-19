#ifndef COORDINA_GRAPH_ENGINE_CRITICAL_PATH_H
#define COORDINA_GRAPH_ENGINE_CRITICAL_PATH_H

#include "core/graph_snapshot.h"

#include <unordered_map>
#include <vector>

namespace coordina::graph
{
// How long each node takes. A node left out takes no time and no part in the
// path: it is done.
using NodeHours = std::unordered_map<NodeId, double, NodeHash>;

// The chain of dependencies that takes longest to finish, its first
// dependency first. Only chains of at least two nodes count: without a
// dependency there is no path.
std::vector<NodeId> critical_path(
  const GraphSnapshot& graph,
  const std::vector<NodeId>& dependency_order,
  const NodeHours& hours);
}

#endif
