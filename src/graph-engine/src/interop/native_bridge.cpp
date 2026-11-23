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

bool is_missing(const void* items, std::size_t count) noexcept
{
  return count > 0U && items == nullptr;
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
    .max_size = to_card_size(options->max_size),
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
        .size = to_card_size(work_items[index].size),
        .assignment = to_assignment(work_items[index].assignment),
        .is_in_progress = work_items[index].is_in_progress != 0U,
        .is_context_match = work_items[index].is_context_match != 0U
      });
  }

  return native_work_items;
}

std::vector<Assignee> build_assignees(
  const coordina_graph_assignee* assignees,
  std::size_t assignee_count)
{
  std::vector<Assignee> native_assignees;
  native_assignees.reserve(assignee_count);

  for (std::size_t index = 0U; index < assignee_count; ++index)
  {
    native_assignees.push_back(
      Assignee{.node = to_node_id(assignees[index].node), .person = assignees[index].person});
  }

  return native_assignees;
}

std::vector<NodeId> build_node_ids(const coordina_graph_node_id* nodes, std::size_t node_count)
{
  std::vector<NodeId> ids;
  ids.reserve(node_count);

  for (std::size_t index = 0U; index < node_count; ++index)
  {
    ids.push_back(to_node_id(nodes[index]));
  }

  return ids;
}

ScopeOptions build_scope_options(const coordina_graph_scope_options& options) noexcept
{
  return ScopeOptions{
    .target_hours = options.target_hours,
    .goal = options.goal,
    .seed = options.seed,
    .run_count = options.run_count,
    .screen_run_count = options.screen_run_count,
    .thread_count = options.thread_count,
    .evaluation_limit = options.evaluation_limit,
    .time_limit = std::chrono::milliseconds(options.time_limit_ms)
  };
}

void write_scope_cut(const ScopeCut& cut, coordina_graph_scope_cut& output)
{
  for (std::size_t index = 0U; index < cut.cuts.size(); ++index)
  {
    output.cuts[index] = to_abi_node_id(cut.cuts[index]);
  }

  for (std::size_t index = 0U; index < cut.dropped.size(); ++index)
  {
    output.dropped[index] = coordina_graph_dropped_card{
      .node = to_abi_node_id(cut.dropped[index].node),
      .cut = cut.dropped[index].cut,
      .reserved = 0U
    };
  }

  output.on_time_before = cut.on_time_before;
  output.on_time_after = cut.on_time_after;
  output.p50_days = cut.p50_days;
  output.p85_days = cut.p85_days;
}

StaffingOptions build_staffing_options(const coordina_graph_staffing_options& options) noexcept
{
  return StaffingOptions{
    .seed = options.seed,
    .run_count = options.run_count,
    .screen_run_count = options.screen_run_count,
    .thread_count = options.thread_count,
    .allow_reassignment = options.allow_reassignment != 0U,
    .evaluation_limit = options.evaluation_limit,
    .time_limit = std::chrono::milliseconds(options.time_limit_ms)
  };
}

void write_staffing(const StaffingPlan& plan, coordina_graph_staffing& output)
{
  for (std::size_t index = 0U; index < plan.changes.size(); ++index)
  {
    output.changes[index] = coordina_graph_staff_change{
      .node = to_abi_node_id(plan.changes[index].node),
      .person = plan.changes[index].person,
      .reserved = 0U,
      .start_days_before = plan.changes[index].start_days_before,
      .start_days_after = plan.changes[index].start_days_after
    };
  }

  output.p50_days_before = plan.p50_days_before;
  output.p85_days_before = plan.p85_days_before;
  output.p50_days_after = plan.p50_days_after;
  output.p85_days_after = plan.p85_days_after;
}

std::size_t write_card_forecasts(
  const std::vector<CardForecast>& cards,
  coordina_graph_card_forecast* output,
  std::size_t output_capacity)
{
  if (output == nullptr || output_capacity == 0U)
  {
    return 0U;
  }

  const auto count = cards.size() < output_capacity ? cards.size() : output_capacity;

  for (std::size_t index = 0U; index < count; ++index)
  {
    output[index] = coordina_graph_card_forecast{
      .node = to_abi_node_id(cards[index].node),
      .criticality = cards[index].criticality,
      .sensitivity = cards[index].sensitivity
    };
  }

  return count;
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
      .fits_max_size = recommendations[index].fits_max_size ? 1U : 0U,
      .is_assignment_match = recommendations[index].is_assignment_match ? 1U : 0U,
      .is_context_match = recommendations[index].is_context_match ? 1U : 0U,
      .is_in_progress = recommendations[index].is_in_progress ? 1U : 0U
    };
  }

  return count;
}
}
