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
struct any_field
{
  template <typename T>
  operator T() const noexcept;
};

template <typename T, std::size_t... Indexes>
constexpr bool is_initializable_with(std::index_sequence<Indexes...>)
{
  return requires { T{((void)Indexes, any_field{})...}; };
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

struct field_layout
{
  const char* name;
  std::size_t offset;
  std::size_t size;
};

struct enum_value
{
  const char* name;
  long long value;
};

bool first_entry = true;

void open_entry()
{
  std::printf(first_entry ? "\n" : ",\n");
  first_entry = false;
}

void print_struct(
  const char* name,
  std::size_t size,
  const std::vector<field_layout>& fields)
{
  open_entry();
  std::printf("    {\n      \"name\": \"%s\",\n      \"size\": %zu,\n      \"fields\": [", name, size);

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

void print_enum(const char* name, const std::vector<enum_value>& values)
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
  field_layout{#field, offsetof(type, field), sizeof(type::field)}

#define COORDINA_STRUCT(type, ...)                                \
  do                                                              \
  {                                                               \
    static_assert(                                                \
      field_count<type>() == std::array{__VA_ARGS__}.size(),      \
      "List every field of " #type " in abi_layout.cpp");         \
    print_struct(#type, sizeof(type), {__VA_ARGS__});             \
  } while (false)

#define COORDINA_ENUM_CASE(value) \
  case value:                     \
    return #value;

#define COORDINA_ENUM_ENTRY(value) enum_value{#value, value},

// The switch has no default, so -Wswitch rejects a value missing from the list.
#define COORDINA_ENUM(type, values)                     \
  do                                                    \
  {                                                     \
    [[maybe_unused]] auto listed = [](type value) {     \
      switch (value)                                    \
      {                                                 \
        values(COORDINA_ENUM_CASE)                      \
      }                                                 \
      return "";                                        \
    };                                                  \
    print_enum(#type, {values(COORDINA_ENUM_ENTRY)});   \
  } while (false)

#define COORDINA_ASSIGNMENT_VALUES(X)     \
  X(COORDINA_GRAPH_UNASSIGNED)            \
  X(COORDINA_GRAPH_ASSIGNED_TO_REQUESTER) \
  X(COORDINA_GRAPH_ASSIGNED_TO_OTHERS)

#define COORDINA_EDGE_STATUS_VALUES(X)       \
  X(COORDINA_GRAPH_EDGE_ALLOWED)             \
  X(COORDINA_GRAPH_EDGE_MISSING_SOURCE)      \
  X(COORDINA_GRAPH_EDGE_MISSING_TARGET)      \
  X(COORDINA_GRAPH_EDGE_SELF_REFERENCE)      \
  X(COORDINA_GRAPH_EDGE_DUPLICATE)           \
  X(COORDINA_GRAPH_EDGE_CYCLE)               \
  X(COORDINA_GRAPH_EDGE_INVALID_ARGUMENT)

#define COORDINA_STATUS_VALUES(X)      \
  X(COORDINA_GRAPH_OK)                 \
  X(COORDINA_GRAPH_INVALID_ARGUMENT)   \
  X(COORDINA_GRAPH_INSUFFICIENT_CAPACITY)

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
    COORDINA_FIELD(coordina_graph_work_item, estimated_minutes),
    COORDINA_FIELD(coordina_graph_work_item, assignment),
    COORDINA_FIELD(coordina_graph_work_item, is_in_progress),
    COORDINA_FIELD(coordina_graph_work_item, is_context_match));

  COORDINA_STRUCT(
    coordina_graph_planning_options,
    COORDINA_FIELD(coordina_graph_planning_options, available_minutes),
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
    COORDINA_FIELD(coordina_graph_work_recommendation, fits_available_time),
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

  std::printf("\n  ],\n  \"enums\": [");
  first_entry = true;

  COORDINA_ENUM(coordina_graph_assignment, COORDINA_ASSIGNMENT_VALUES);
  COORDINA_ENUM(coordina_graph_edge_status, COORDINA_EDGE_STATUS_VALUES);
  COORDINA_ENUM(coordina_graph_status, COORDINA_STATUS_VALUES);

  std::printf("\n  ]\n}\n");
  return 0;
}
