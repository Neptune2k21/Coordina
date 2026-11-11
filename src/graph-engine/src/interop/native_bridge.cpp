#include "interop/native_bridge.h"

#include <cstdint>
#include <limits>

namespace coordina::graph
{
namespace
{
Assignment to_assignment(std::uint32_t value) noexcept
{
  switch (value)
  {
    case COORDINA_GRAPH_ASSIGNED_TO_REQUESTER:
      return Assignment::requester;
    case COORDINA_GRAPH_ASSIGNED_TO_OTHERS:
      return Assignment::others;
    default:
      return Assignment::unassigned;
  }
}

std::uint32_t to_abi_count(std::size_t value) noexcept
{
  constexpr auto max = std::numeric_limits<std::uint32_t>::max();
  return value > max ? max : static_cast<std::uint32_t>(value);
}
}

bool has_invalid_pointer(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count) noexcept
{
  return (node_count > 0U && nodes == nullptr) || (edge_count > 0U && edges == nullptr);
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
    native_edges.push_back(
      Edge{.source = to_node_id(edges[index].source), .target = to_node_id(edges[index].target)});
  }

  return {native_nodes, native_edges};
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

  const auto count = nodes.size() < output_capacity ? nodes.size() : output_capacity;

  for (std::size_t index = 0U; index < count; ++index)
  {
    output[index] = to_abi_node_id(nodes[index]);
  }

  return count;
}

std::size_t write_node_analyses(
  const std::vector<NodeAnalysis>& analyses,
  coordina_graph_node_analysis* output,
  std::size_t output_capacity)
{
  if (output == nullptr || output_capacity == 0U)
  {
    return 0U;
  }

  const auto count = analyses.size() < output_capacity ? analyses.size() : output_capacity;

  for (std::size_t index = 0U; index < count; ++index)
  {
    output[index] = coordina_graph_node_analysis{
      .node = to_abi_node_id(analyses[index].node),
      .dependency_count = to_abi_count(analyses[index].dependency_count),
      .dependent_count = to_abi_count(analyses[index].dependent_count),
      .dependency_depth = to_abi_count(analyses[index].dependency_depth),
      .dependent_depth = to_abi_count(analyses[index].dependent_depth),
      .transitive_dependent_count = to_abi_count(analyses[index].transitive_dependent_count)
    };
  }

  return count;
}

PlanningOptions build_planning_options(const coordina_graph_planning_options* options) noexcept
{
  if (options == nullptr)
  {
    return PlanningOptions{};
  }

  return PlanningOptions{
    .available_minutes = options->available_minutes,
    .prefer_assigned = options->prefer_assigned != 0U,
    .include_unassigned = options->include_unassigned != 0U
  };
}

std::vector<WorkItem> build_work_items(
  const coordina_graph_work_item* work_items,
  std::size_t work_item_count)
{
  std::vector<WorkItem> native_work_items;
  native_work_items.reserve(work_item_count);

  for (std::size_t index = 0U; index < work_item_count; ++index)
  {
    native_work_items.push_back(
      WorkItem{
        .node = to_node_id(work_items[index].node),
        .is_completed = work_items[index].is_completed != 0U,
        .priority = work_items[index].priority,
        .due_days = work_items[index].due_days,
        .subtask_total = work_items[index].subtask_total,
        .subtask_completed = work_items[index].subtask_completed,
        .position = work_items[index].position,
        .estimated_minutes = work_items[index].estimated_minutes,
        .assignment = to_assignment(work_items[index].assignment),
        .is_in_progress = work_items[index].is_in_progress != 0U,
        .is_context_match = work_items[index].is_context_match != 0U
      });
  }

  return native_work_items;
}

std::size_t write_work_recommendations(
  const std::vector<WorkRecommendation>& recommendations,
  coordina_graph_work_recommendation* output,
  std::size_t output_capacity)
{
  if (output == nullptr || output_capacity == 0U)
  {
    return 0U;
  }

  const auto count =
    recommendations.size() < output_capacity ? recommendations.size() : output_capacity;

  for (std::size_t index = 0U; index < count; ++index)
  {
    output[index] = coordina_graph_work_recommendation{
      .node = to_abi_node_id(recommendations[index].node),
      .score = recommendations[index].score,
      .blocker_count = recommendations[index].blocker_count,
      .unlock_score = recommendations[index].unlock_score,
      .urgency_score = recommendations[index].urgency_score,
      .priority_score = recommendations[index].priority_score,
      .progress_score = recommendations[index].progress_score,
      .is_critical_path = recommendations[index].is_critical_path ? 1U : 0U,
      .effort_score = recommendations[index].effort_score,
      .assignment_score = recommendations[index].assignment_score,
      .momentum_score = recommendations[index].momentum_score,
      .context_score = recommendations[index].context_score,
      .fits_available_time = recommendations[index].fits_available_time ? 1U : 0U,
      .is_assignment_match = recommendations[index].is_assignment_match ? 1U : 0U,
      .is_context_match = recommendations[index].is_context_match ? 1U : 0U,
      .is_in_progress = recommendations[index].is_in_progress ? 1U : 0U
    };
  }

  return count;
}
}
