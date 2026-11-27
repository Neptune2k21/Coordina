// Prints the layout of every struct and the value of every enum in the C ABI,
// as the compiler sees them. `make graph-engine-abi` writes the output to
// contracts/graph-engine-abi.json, and the API's tests check that its
// marshalled structs match that file.
//
// The lists below cannot silently miss a member: a struct whose field count
// differs from its list fails a static_assert, and an enum value missing from
// its list fails -Wswitch.

#include "coordina_graph_engine.h"

#include <array>
#include <cstddef>
#include <cstdio>
#include <utility>
#include <vector>

namespace
{
struct AnyField
{
  template <typename T>
  operator T() const noexcept;
};

template <typename T, std::size_t... Indexes>
constexpr bool is_initializable_with(std::index_sequence<Indexes...> /*indexes*/)
{
  return requires { T{((void)Indexes, AnyField{})...}; };
}

// Counts an aggregate's fields: the most initializers its braces accept.
template <typename T, std::size_t Count = 0>
constexpr std::size_t field_count()
{
  if constexpr (is_initializable_with<T>(std::make_index_sequence<Count + 1>{}))
  {
    return field_count<T, Count + 1>();
  }
  else
  {
    return Count;
  }
}

struct FieldLayout
{
  const char* name;
  std::size_t offset;
  std::size_t size;
};

struct EnumValue
{
  const char* name;
  long long value;
};

// NOLINTNEXTLINE(cppcoreguidelines-avoid-non-const-global-variables): this program prints one JSON document, and the flag says whether the current array has an entry yet.
bool first_entry = true;

void open_entry()
{
  std::printf(first_entry ? "\n" : ",\n");
  first_entry = false;
}

void print_struct(const char* name, std::size_t size, const std::vector<FieldLayout>& fields)
{
  open_entry();
  std::printf(
    "    {\n      \"name\": \"%s\",\n      \"size\": %zu,\n      \"fields\": [",
    name,
    size);

  for (std::size_t index = 0; index < fields.size(); ++index)
  {
    std::printf(
      "%s\n        { \"name\": \"%s\", \"offset\": %zu, \"size\": %zu }",
      index == 0 ? "" : ",",
      fields[index].name,
      fields[index].offset,
      fields[index].size);
  }

  std::printf("\n      ]\n    }");
}

void print_enum(const char* name, const std::vector<EnumValue>& values)
{
  open_entry();
  std::printf("    {\n      \"name\": \"%s\",\n      \"values\": [", name);

  for (std::size_t index = 0; index < values.size(); ++index)
  {
    std::printf(
      "%s\n        { \"name\": \"%s\", \"value\": %lld }",
      index == 0 ? "" : ",",
      values[index].name,
      values[index].value);
  }

  std::printf("\n      ]\n    }");
}
}

#define COORDINA_FIELD(type, field) \
  (FieldLayout{#field, offsetof(type, field), sizeof(type::field)})

#define COORDINA_STRUCT(type, ...)                           \
  do                                                         \
  {                                                          \
    static_assert(                                           \
      field_count<type>() == std::array{__VA_ARGS__}.size(), \
      "List every field of " #type " in abi_layout.cpp");    \
    print_struct(#type, sizeof(type), {__VA_ARGS__});        \
  } while (false)

#define COORDINA_ENUM_CASE(value) \
  case value:                     \
    return #value;

#define COORDINA_ENUM_ENTRY(value) EnumValue{#value, value},

// The switch has no default, so -Wswitch rejects a value missing from the list.
#define COORDINA_ENUM(type, values)                   \
  do                                                  \
  {                                                   \
    [[maybe_unused]] auto listed = [](type value)     \
    {                                                 \
      switch (value)                                  \
      {                                               \
        values(COORDINA_ENUM_CASE)                    \
      }                                               \
      return "";                                      \
    };                                                \
    print_enum(#type, {values(COORDINA_ENUM_ENTRY)}); \
  } while (false)

#define COORDINA_ASSIGNMENT_VALUES(X)     \
  X(COORDINA_GRAPH_UNASSIGNED)            \
  X(COORDINA_GRAPH_ASSIGNED_TO_REQUESTER) \
  X(COORDINA_GRAPH_ASSIGNED_TO_OTHERS)

#define COORDINA_SIZE_VALUES(X) \
  X(COORDINA_GRAPH_SIZE_NONE)   \
  X(COORDINA_GRAPH_SIZE_XS)     \
  X(COORDINA_GRAPH_SIZE_S)      \
  X(COORDINA_GRAPH_SIZE_M)      \
  X(COORDINA_GRAPH_SIZE_L)      \
  X(COORDINA_GRAPH_SIZE_XL)     \
  X(COORDINA_GRAPH_SIZE_XXL)

#define COORDINA_EDGE_STATUS_VALUES(X)  \
  X(COORDINA_GRAPH_EDGE_ALLOWED)        \
  X(COORDINA_GRAPH_EDGE_MISSING_SOURCE) \
  X(COORDINA_GRAPH_EDGE_MISSING_TARGET) \
  X(COORDINA_GRAPH_EDGE_SELF_REFERENCE) \
  X(COORDINA_GRAPH_EDGE_DUPLICATE)      \
  X(COORDINA_GRAPH_EDGE_CYCLE)          \
  X(COORDINA_GRAPH_EDGE_INVALID_ARGUMENT)

#define COORDINA_STATUS_VALUES(X)         \
  X(COORDINA_GRAPH_OK)                    \
  X(COORDINA_GRAPH_INVALID_ARGUMENT)      \
  X(COORDINA_GRAPH_INSUFFICIENT_CAPACITY) \
  X(COORDINA_GRAPH_CYCLE)                 \
  X(COORDINA_GRAPH_OUT_OF_MEMORY)         \
  X(COORDINA_GRAPH_INTERNAL_ERROR)

int main()
{
  std::printf("{\n  \"structs\": [");

  COORDINA_STRUCT(
    coordina_graph_node_id,
    COORDINA_FIELD(coordina_graph_node_id, high),
    COORDINA_FIELD(coordina_graph_node_id, low));

  COORDINA_STRUCT(
    coordina_graph_edge,
    COORDINA_FIELD(coordina_graph_edge, source),
    COORDINA_FIELD(coordina_graph_edge, target));

  COORDINA_STRUCT(
    coordina_graph_node_analysis,
    COORDINA_FIELD(coordina_graph_node_analysis, node),
    COORDINA_FIELD(coordina_graph_node_analysis, dependency_count),
    COORDINA_FIELD(coordina_graph_node_analysis, dependent_count),
    COORDINA_FIELD(coordina_graph_node_analysis, dependency_depth),
    COORDINA_FIELD(coordina_graph_node_analysis, dependent_depth),
    COORDINA_FIELD(coordina_graph_node_analysis, transitive_dependent_count));

  COORDINA_STRUCT(
    coordina_graph_work_item,
    COORDINA_FIELD(coordina_graph_work_item, node),
    COORDINA_FIELD(coordina_graph_work_item, is_completed),
    COORDINA_FIELD(coordina_graph_work_item, priority),
    COORDINA_FIELD(coordina_graph_work_item, due_days),
    COORDINA_FIELD(coordina_graph_work_item, subtask_total),
    COORDINA_FIELD(coordina_graph_work_item, subtask_completed),
    COORDINA_FIELD(coordina_graph_work_item, position),
    COORDINA_FIELD(coordina_graph_work_item, size),
    COORDINA_FIELD(coordina_graph_work_item, assignment),
    COORDINA_FIELD(coordina_graph_work_item, is_in_progress),
    COORDINA_FIELD(coordina_graph_work_item, is_context_match),
    COORDINA_FIELD(coordina_graph_work_item, elapsed_hours));

  COORDINA_STRUCT(
    coordina_graph_planning_options,
    COORDINA_FIELD(coordina_graph_planning_options, max_size),
    COORDINA_FIELD(coordina_graph_planning_options, prefer_assigned),
    COORDINA_FIELD(coordina_graph_planning_options, include_unassigned));

  COORDINA_STRUCT(
    coordina_graph_work_recommendation,
    COORDINA_FIELD(coordina_graph_work_recommendation, node),
    COORDINA_FIELD(coordina_graph_work_recommendation, score),
    COORDINA_FIELD(coordina_graph_work_recommendation, blocker_count),
    COORDINA_FIELD(coordina_graph_work_recommendation, unlock_score),
    COORDINA_FIELD(coordina_graph_work_recommendation, urgency_score),
    COORDINA_FIELD(coordina_graph_work_recommendation, priority_score),
    COORDINA_FIELD(coordina_graph_work_recommendation, progress_score),
    COORDINA_FIELD(coordina_graph_work_recommendation, is_critical_path),
    COORDINA_FIELD(coordina_graph_work_recommendation, effort_score),
    COORDINA_FIELD(coordina_graph_work_recommendation, assignment_score),
    COORDINA_FIELD(coordina_graph_work_recommendation, momentum_score),
    COORDINA_FIELD(coordina_graph_work_recommendation, context_score),
    COORDINA_FIELD(coordina_graph_work_recommendation, fits_max_size),
    COORDINA_FIELD(coordina_graph_work_recommendation, is_assignment_match),
    COORDINA_FIELD(coordina_graph_work_recommendation, is_context_match),
    COORDINA_FIELD(coordina_graph_work_recommendation, is_in_progress));

  COORDINA_STRUCT(
    coordina_graph_node_buffer,
    COORDINA_FIELD(coordina_graph_node_buffer, items),
    COORDINA_FIELD(coordina_graph_node_buffer, capacity),
    COORDINA_FIELD(coordina_graph_node_buffer, count));

  COORDINA_STRUCT(
    coordina_graph_board_analysis,
    COORDINA_FIELD(coordina_graph_board_analysis, ready_nodes),
    COORDINA_FIELD(coordina_graph_board_analysis, dependency_order),
    COORDINA_FIELD(coordina_graph_board_analysis, critical_path),
    COORDINA_FIELD(coordina_graph_board_analysis, node_analyses),
    COORDINA_FIELD(coordina_graph_board_analysis, node_analysis_capacity),
    COORDINA_FIELD(coordina_graph_board_analysis, node_analysis_count),
    COORDINA_FIELD(coordina_graph_board_analysis, recommendations),
    COORDINA_FIELD(coordina_graph_board_analysis, recommendation_capacity),
    COORDINA_FIELD(coordina_graph_board_analysis, recommendation_count),
    COORDINA_FIELD(coordina_graph_board_analysis, is_acyclic),
    COORDINA_FIELD(coordina_graph_board_analysis, reserved));

  COORDINA_STRUCT(
    coordina_graph_assignee,
    COORDINA_FIELD(coordina_graph_assignee, node),
    COORDINA_FIELD(coordina_graph_assignee, person),
    COORDINA_FIELD(coordina_graph_assignee, reserved));

  COORDINA_STRUCT(
    coordina_graph_card_label,
    COORDINA_FIELD(coordina_graph_card_label, node),
    COORDINA_FIELD(coordina_graph_card_label, label),
    COORDINA_FIELD(coordina_graph_card_label, reserved));

  COORDINA_STRUCT(
    coordina_graph_milestone,
    COORDINA_FIELD(coordina_graph_milestone, node),
    COORDINA_FIELD(coordina_graph_milestone, target_hours),
    COORDINA_FIELD(coordina_graph_milestone, weight));

  COORDINA_STRUCT(
    coordina_graph_finished_card,
    COORDINA_FIELD(coordina_graph_finished_card, node),
    COORDINA_FIELD(coordina_graph_finished_card, hours),
    COORDINA_FIELD(coordina_graph_finished_card, age_days));

  COORDINA_STRUCT(
    coordina_graph_board,
    COORDINA_FIELD(coordina_graph_board, nodes),
    COORDINA_FIELD(coordina_graph_board, node_count),
    COORDINA_FIELD(coordina_graph_board, edges),
    COORDINA_FIELD(coordina_graph_board, edge_count),
    COORDINA_FIELD(coordina_graph_board, work_items),
    COORDINA_FIELD(coordina_graph_board, work_item_count),
    COORDINA_FIELD(coordina_graph_board, assignees),
    COORDINA_FIELD(coordina_graph_board, assignee_count),
    COORDINA_FIELD(coordina_graph_board, labels),
    COORDINA_FIELD(coordina_graph_board, label_count),
    COORDINA_FIELD(coordina_graph_board, milestones),
    COORDINA_FIELD(coordina_graph_board, milestone_count),
    COORDINA_FIELD(coordina_graph_board, history),
    COORDINA_FIELD(coordina_graph_board, history_count),
    COORDINA_FIELD(coordina_graph_board, person_count),
    COORDINA_FIELD(coordina_graph_board, discovered_count),
    COORDINA_FIELD(coordina_graph_board, finished_count),
    COORDINA_FIELD(coordina_graph_board, extra_people));

  COORDINA_STRUCT(
    coordina_graph_simulation,
    COORDINA_FIELD(coordina_graph_simulation, seed),
    COORDINA_FIELD(coordina_graph_simulation, run_count),
    COORDINA_FIELD(coordina_graph_simulation, thread_count));

  COORDINA_STRUCT(
    coordina_graph_milestone_forecast,
    COORDINA_FIELD(coordina_graph_milestone_forecast, node),
    COORDINA_FIELD(coordina_graph_milestone_forecast, p10_days),
    COORDINA_FIELD(coordina_graph_milestone_forecast, p50_days),
    COORDINA_FIELD(coordina_graph_milestone_forecast, p85_days),
    COORDINA_FIELD(coordina_graph_milestone_forecast, p95_days),
    COORDINA_FIELD(coordina_graph_milestone_forecast, on_time),
    COORDINA_FIELD(coordina_graph_milestone_forecast, late_days),
    COORDINA_FIELD(coordina_graph_milestone_forecast, chain_days),
    COORDINA_FIELD(coordina_graph_milestone_forecast, card_count),
    COORDINA_FIELD(coordina_graph_milestone_forecast, unsized_count));

  COORDINA_STRUCT(
    coordina_graph_card_forecast,
    COORDINA_FIELD(coordina_graph_card_forecast, node),
    COORDINA_FIELD(coordina_graph_card_forecast, milestone),
    COORDINA_FIELD(coordina_graph_card_forecast, reserved),
    COORDINA_FIELD(coordina_graph_card_forecast, criticality),
    COORDINA_FIELD(coordina_graph_card_forecast, sensitivity));

  COORDINA_STRUCT(
    coordina_graph_planned_card,
    COORDINA_FIELD(coordina_graph_planned_card, node),
    COORDINA_FIELD(coordina_graph_planned_card, person),
    COORDINA_FIELD(coordina_graph_planned_card, reserved),
    COORDINA_FIELD(coordina_graph_planned_card, start_days),
    COORDINA_FIELD(coordina_graph_planned_card, finish_days));

  COORDINA_STRUCT(
    coordina_graph_forecast_output,
    COORDINA_FIELD(coordina_graph_forecast_output, milestones),
    COORDINA_FIELD(coordina_graph_forecast_output, milestone_capacity),
    COORDINA_FIELD(coordina_graph_forecast_output, milestone_count),
    COORDINA_FIELD(coordina_graph_forecast_output, cards),
    COORDINA_FIELD(coordina_graph_forecast_output, card_capacity),
    COORDINA_FIELD(coordina_graph_forecast_output, card_count),
    COORDINA_FIELD(coordina_graph_forecast_output, plan),
    COORDINA_FIELD(coordina_graph_forecast_output, plan_capacity),
    COORDINA_FIELD(coordina_graph_forecast_output, plan_count));

  COORDINA_STRUCT(
    coordina_graph_scope_options,
    COORDINA_FIELD(coordina_graph_scope_options, milestone),
    COORDINA_FIELD(coordina_graph_scope_options, simulation),
    COORDINA_FIELD(coordina_graph_scope_options, goal),
    COORDINA_FIELD(coordina_graph_scope_options, screen_run_count),
    COORDINA_FIELD(coordina_graph_scope_options, evaluation_limit),
    COORDINA_FIELD(coordina_graph_scope_options, time_limit_ms),
    COORDINA_FIELD(coordina_graph_scope_options, reserved));

  COORDINA_STRUCT(
    coordina_graph_dropped_card,
    COORDINA_FIELD(coordina_graph_dropped_card, node),
    COORDINA_FIELD(coordina_graph_dropped_card, cut),
    COORDINA_FIELD(coordina_graph_dropped_card, reserved));

  COORDINA_STRUCT(
    coordina_graph_scope_cut,
    COORDINA_FIELD(coordina_graph_scope_cut, cuts),
    COORDINA_FIELD(coordina_graph_scope_cut, cut_capacity),
    COORDINA_FIELD(coordina_graph_scope_cut, cut_count),
    COORDINA_FIELD(coordina_graph_scope_cut, dropped),
    COORDINA_FIELD(coordina_graph_scope_cut, dropped_capacity),
    COORDINA_FIELD(coordina_graph_scope_cut, dropped_count),
    COORDINA_FIELD(coordina_graph_scope_cut, on_time_before),
    COORDINA_FIELD(coordina_graph_scope_cut, on_time_after),
    COORDINA_FIELD(coordina_graph_scope_cut, p50_days),
    COORDINA_FIELD(coordina_graph_scope_cut, p85_days));

  COORDINA_STRUCT(
    coordina_graph_staffing_options,
    COORDINA_FIELD(coordina_graph_staffing_options, milestone),
    COORDINA_FIELD(coordina_graph_staffing_options, simulation),
    COORDINA_FIELD(coordina_graph_staffing_options, screen_run_count),
    COORDINA_FIELD(coordina_graph_staffing_options, allow_reassignment),
    COORDINA_FIELD(coordina_graph_staffing_options, evaluation_limit),
    COORDINA_FIELD(coordina_graph_staffing_options, time_limit_ms));

  COORDINA_STRUCT(
    coordina_graph_staff_change,
    COORDINA_FIELD(coordina_graph_staff_change, node),
    COORDINA_FIELD(coordina_graph_staff_change, person),
    COORDINA_FIELD(coordina_graph_staff_change, reserved),
    COORDINA_FIELD(coordina_graph_staff_change, start_days_before),
    COORDINA_FIELD(coordina_graph_staff_change, start_days_after));

  COORDINA_STRUCT(
    coordina_graph_staffing,
    COORDINA_FIELD(coordina_graph_staffing, changes),
    COORDINA_FIELD(coordina_graph_staffing, change_capacity),
    COORDINA_FIELD(coordina_graph_staffing, change_count),
    COORDINA_FIELD(coordina_graph_staffing, p50_days_before),
    COORDINA_FIELD(coordina_graph_staffing, p85_days_before),
    COORDINA_FIELD(coordina_graph_staffing, p50_days_after),
    COORDINA_FIELD(coordina_graph_staffing, p85_days_after),
    COORDINA_FIELD(coordina_graph_staffing, others_late_days_before),
    COORDINA_FIELD(coordina_graph_staffing, others_late_days_after));

  std::printf("\n  ],\n  \"enums\": [");
  first_entry = true;

  COORDINA_ENUM(coordina_graph_assignment, COORDINA_ASSIGNMENT_VALUES);
  COORDINA_ENUM(coordina_graph_size, COORDINA_SIZE_VALUES);
  COORDINA_ENUM(coordina_graph_edge_status, COORDINA_EDGE_STATUS_VALUES);
  COORDINA_ENUM(coordina_graph_status, COORDINA_STATUS_VALUES);

  std::printf("\n  ]\n}\n");
  return 0;
}
