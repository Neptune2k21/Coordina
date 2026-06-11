#ifndef COORDINA_GRAPH_ENGINE_WORK_PLANNER_H
#define COORDINA_GRAPH_ENGINE_WORK_PLANNER_H

#include "graph_snapshot.h"

#include <cstddef>
#include <cstdint>
#include <vector>

namespace coordina::graph
{
struct WorkItem
{
  NodeId node;
  bool is_completed;
  std::uint32_t priority;
  std::int32_t due_days;
  std::uint32_t subtask_total;
  std::uint32_t subtask_completed;
  std::uint32_t position;
};

struct WorkRecommendation
{
  NodeId node;
  std::uint32_t score;
  std::uint32_t blocker_count;
  std::uint32_t unlock_score;
  std::uint32_t urgency_score;
  std::uint32_t priority_score;
  std::uint32_t progress_score;
  bool is_critical_path;
};

std::vector<WorkRecommendation> plan_work(
  const GraphSnapshot& graph,
  const std::vector<WorkItem>& work_items);
}

#endif
