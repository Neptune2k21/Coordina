#include "algorithms/critical_path.h"

#include <algorithm>

namespace coordina::graph
{
std::vector<NodeId> critical_path(
  const GraphSnapshot& graph,
  const std::vector<NodeId>& dependency_order,
  const NodeHours& hours)
{
  if (dependency_order.empty() || dependency_order.size() != graph.nodes().size())
  {
    return {};
  }

  // The time from the start of the chain to the end of each node.
  auto finish = hours;
  std::unordered_map<NodeId, NodeId, NodeHash> previous;
  previous.reserve(hours.size());

  for (const auto dependency : dependency_order)
  {
    const auto found = finish.find(dependency);

    if (found == finish.end())
    {
      continue;
    }

    const auto dependency_finish = found->second;

    for (const auto dependent : graph.incoming(dependency))
    {
      const auto dependent_hours = hours.find(dependent);

      if (dependent_hours == hours.end())
      {
        continue;
      }

      const auto candidate = dependency_finish + dependent_hours->second;
      auto& current = finish[dependent];

      if (candidate > current)
      {
        current = candidate;
        previous[dependent] = dependency;
      }
    }
  }

  const NodeId* end = nullptr;
  auto longest = 0.0;

  for (const auto& node : dependency_order)
  {
    if (previous.contains(node) && (end == nullptr || finish[node] > longest))
    {
      end = &node;
      longest = finish[node];
    }
  }

  if (end == nullptr)
  {
    return {};
  }

  std::vector<NodeId> path{*end};

  for (auto found = previous.find(*end); found != previous.end();
    found = previous.find(found->second))
  {
    path.push_back(found->second);
  }

  std::ranges::reverse(path);
  return path;
}
}
