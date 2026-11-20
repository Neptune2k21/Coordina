#include "planning/planning_analysis.h"

#include "algorithms/critical_path.h"
#include "algorithms/topological_order.h"

namespace coordina::graph
{
NodeHours open_card_hours(const GraphSnapshot& graph, const std::vector<WorkItem>& work_items)
{
  NodeHours hours;
  hours.reserve(graph.nodes().size());

  for (const auto node : graph.nodes())
  {
    hours.emplace(node, planned_range(CardSize::none).likely_hours);
  }

  for (const auto& item : work_items)
  {
    const auto found = hours.find(item.node);

    if (found == hours.end())
    {
      continue;
    }

    if (item.is_completed)
    {
      hours.erase(found);
    }
    else
    {
      found->second = planned_range(item.size).likely_hours;
    }
  }

  return hours;
}

PlanningAnalysis analyze_planning(
  const GraphSnapshot& graph,
  const std::vector<WorkItem>& work_items)
{
  auto order = dependency_first_topological_order(graph);
  const auto is_acyclic = order.size() == graph.nodes().size();
  auto analyses = analyze_nodes(graph, order);
  auto path = critical_path(graph, order, open_card_hours(graph, work_items));

  return PlanningAnalysis{
    .is_acyclic = is_acyclic,
    .dependency_order = std::move(order),
    .ready_nodes = ready_nodes(graph),
    .node_analyses = std::move(analyses),
    .critical_path = std::move(path)
  };
}

BoardAnalysis analyze_board(
  const GraphSnapshot& graph,
  const std::vector<WorkItem>& work_items,
  const PlanningOptions& options)
{
  auto planning = analyze_planning(graph, work_items);
  auto recommendations = plan_work(graph, work_items, options, planning);

  return BoardAnalysis{
    .planning = std::move(planning),
    .recommendations = std::move(recommendations)
  };
}
}
