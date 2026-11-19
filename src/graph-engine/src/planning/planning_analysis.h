#ifndef COORDINA_GRAPH_ENGINE_PLANNING_ANALYSIS_H
#define COORDINA_GRAPH_ENGINE_PLANNING_ANALYSIS_H

#include "algorithms/critical_path.h"
#include "algorithms/node_analysis.h"
#include "core/graph_snapshot.h"
#include "planning/work_planner.h"

#include <vector>

namespace coordina::graph
{
struct PlanningAnalysis
{
  bool is_acyclic;
  std::vector<NodeId> dependency_order;
  std::vector<NodeId> ready_nodes;
  std::vector<NodeAnalysis> node_analyses;
  std::vector<NodeId> critical_path;
};

struct BoardAnalysis
{
  PlanningAnalysis planning;
  std::vector<WorkRecommendation> recommendations;
};

// The likely focus hours each open card needs. Completed cards are left out,
// and a node without a work item counts as an open, unsized card.
NodeHours open_card_hours(const GraphSnapshot& graph, const std::vector<WorkItem>& work_items);

PlanningAnalysis analyze_planning(
  const GraphSnapshot& graph,
  const std::vector<WorkItem>& work_items);

BoardAnalysis analyze_board(
  const GraphSnapshot& graph,
  const std::vector<WorkItem>& work_items,
  const PlanningOptions& options);
}

#endif
