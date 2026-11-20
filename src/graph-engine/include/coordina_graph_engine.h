#ifndef COORDINA_GRAPH_ENGINE_H
#define COORDINA_GRAPH_ENGINE_H

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32)
#define COORDINA_GRAPH_EXPORT __declspec(dllexport)
#else
#define COORDINA_GRAPH_EXPORT __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef struct coordina_graph_node_id
{
  uint64_t high;
  uint64_t low;
} coordina_graph_node_id;

typedef struct coordina_graph_edge
{
  coordina_graph_node_id source;
  coordina_graph_node_id target;
} coordina_graph_edge;

typedef struct coordina_graph_node_analysis
{
  coordina_graph_node_id node;
  uint32_t dependency_count;
  uint32_t dependent_count;
  uint32_t dependency_depth;
  uint32_t dependent_depth;
  uint32_t transitive_dependent_count;
} coordina_graph_node_analysis;

typedef struct coordina_graph_work_item
{
  coordina_graph_node_id node;
  uint32_t is_completed;
  uint32_t priority;
  int32_t due_days;
  uint32_t subtask_total;
  uint32_t subtask_completed;
  uint32_t position;
  uint32_t size;
  uint32_t assignment;
  uint32_t is_in_progress;
  uint32_t is_context_match;
} coordina_graph_work_item;

typedef enum coordina_graph_assignment
{
  COORDINA_GRAPH_UNASSIGNED = 0,
  COORDINA_GRAPH_ASSIGNED_TO_REQUESTER = 1,
  COORDINA_GRAPH_ASSIGNED_TO_OTHERS = 2
} coordina_graph_assignment;

/* How big a card is, an order of magnitude rather than a duration. */
typedef enum coordina_graph_size
{
  COORDINA_GRAPH_SIZE_NONE = 0,
  COORDINA_GRAPH_SIZE_XS = 1,
  COORDINA_GRAPH_SIZE_S = 2,
  COORDINA_GRAPH_SIZE_M = 3,
  COORDINA_GRAPH_SIZE_L = 4,
  COORDINA_GRAPH_SIZE_XL = 5,
  COORDINA_GRAPH_SIZE_XXL = 6
} coordina_graph_size;

typedef struct coordina_graph_planning_options
{
  /* The biggest size that fits the user's focus slot; NONE means any size. */
  uint32_t max_size;
  uint32_t prefer_assigned;
  uint32_t include_unassigned;
} coordina_graph_planning_options;

typedef struct coordina_graph_work_recommendation
{
  coordina_graph_node_id node;
  uint32_t score;
  uint32_t blocker_count;
  uint32_t unlock_score;
  uint32_t urgency_score;
  uint32_t priority_score;
  uint32_t progress_score;
  uint32_t is_critical_path;
  int32_t effort_score;
  int32_t assignment_score;
  uint32_t momentum_score;
  uint32_t context_score;
  uint32_t fits_max_size;
  uint32_t is_assignment_match;
  uint32_t is_context_match;
  uint32_t is_in_progress;
} coordina_graph_work_recommendation;

typedef enum coordina_graph_edge_status
{
  COORDINA_GRAPH_EDGE_ALLOWED = 0,
  COORDINA_GRAPH_EDGE_MISSING_SOURCE = 1,
  COORDINA_GRAPH_EDGE_MISSING_TARGET = 2,
  COORDINA_GRAPH_EDGE_SELF_REFERENCE = 3,
  COORDINA_GRAPH_EDGE_DUPLICATE = 4,
  COORDINA_GRAPH_EDGE_CYCLE = 5,
  COORDINA_GRAPH_EDGE_INVALID_ARGUMENT = 100
} coordina_graph_edge_status;

typedef enum coordina_graph_status
{
  COORDINA_GRAPH_OK = 0,
  COORDINA_GRAPH_INVALID_ARGUMENT = 1,
  COORDINA_GRAPH_INSUFFICIENT_CAPACITY = 2,
  COORDINA_GRAPH_CYCLE = 3
} coordina_graph_status;

typedef struct coordina_graph_node_buffer
{
  coordina_graph_node_id* items;
  size_t capacity;
  size_t count;
} coordina_graph_node_buffer;

/*
 * Output of coordina_graph_analyze_board. The caller owns every buffer and sets
 * each capacity. On return every count holds the number of items the engine
 * produced. If any capacity is too small the call returns
 * COORDINA_GRAPH_INSUFFICIENT_CAPACITY and writes no item, so the caller can
 * retry with the reported counts.
 */
typedef struct coordina_graph_board_analysis
{
  coordina_graph_node_buffer ready_nodes;
  coordina_graph_node_buffer dependency_order;
  coordina_graph_node_buffer critical_path;
  coordina_graph_node_analysis* node_analyses;
  size_t node_analysis_capacity;
  size_t node_analysis_count;
  coordina_graph_work_recommendation* recommendations;
  size_t recommendation_capacity;
  size_t recommendation_count;
  uint32_t is_acyclic;
  uint32_t reserved;
} coordina_graph_board_analysis;

/* A person working on a card. People are numbered from 0 to person_count. */
typedef struct coordina_graph_assignee
{
  coordina_graph_node_id node;
  uint32_t person;
  uint32_t reserved;
} coordina_graph_assignee;

typedef struct coordina_graph_forecast_options
{
  coordina_graph_node_id milestone;
  /* The same seed gives the same forecast, whatever thread_count is. */
  uint64_t seed;
  /* The team: unassigned cards go to the first of them who is free. */
  uint32_t person_count;
  uint32_t run_count;
  /* 0 uses every core. */
  uint32_t thread_count;
  uint32_t reserved;
} coordina_graph_forecast_options;

typedef struct coordina_graph_card_forecast
{
  coordina_graph_node_id node;
  /* The share of runs where the card drives the date, from 0 to 1. */
  double criticality;
  /* The correlation between the card's duration and the date, from -1 to 1. */
  double sensitivity;
} coordina_graph_card_forecast;

/*
 * Output of coordina_graph_forecast. The caller owns the card buffer and sets
 * its capacity; node_count is always enough. If it is too small the call
 * returns COORDINA_GRAPH_INSUFFICIENT_CAPACITY, sets card_count and writes no
 * card.
 */
typedef struct coordina_graph_milestone_forecast
{
  /* Every open card of the scope, most critical first. */
  coordina_graph_card_forecast* cards;
  size_t card_capacity;
  size_t card_count;
  /* Working days until the scope is done: one chance in two, and 85%. */
  double p50_days;
  double p85_days;
  /* Open cards without a size, planned as M. */
  uint32_t unsized_count;
  uint32_t reserved;
} coordina_graph_milestone_forecast;

COORDINA_GRAPH_EXPORT const char* coordina_graph_version(void);

COORDINA_GRAPH_EXPORT coordina_graph_edge_status coordina_graph_evaluate_edge(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  coordina_graph_node_id source,
  coordina_graph_node_id target);

COORDINA_GRAPH_EXPORT int coordina_graph_has_path(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  coordina_graph_node_id source,
  coordina_graph_node_id target);

COORDINA_GRAPH_EXPORT size_t coordina_graph_suggest_targets(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  coordina_graph_node_id source,
  coordina_graph_node_id* output,
  size_t output_capacity);

COORDINA_GRAPH_EXPORT int coordina_graph_is_acyclic(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count);

COORDINA_GRAPH_EXPORT size_t coordina_graph_topological_sort(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  coordina_graph_node_id* output,
  size_t output_capacity);

COORDINA_GRAPH_EXPORT size_t coordina_graph_ready_nodes(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  coordina_graph_node_id* output,
  size_t output_capacity);

COORDINA_GRAPH_EXPORT size_t coordina_graph_impacted_dependents(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  coordina_graph_node_id dependency,
  coordina_graph_node_id* output,
  size_t output_capacity);

COORDINA_GRAPH_EXPORT size_t coordina_graph_analyze_nodes(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  coordina_graph_node_analysis* output,
  size_t output_capacity);

/*
 * The dependency chain that takes longest, weighted by the likely duration of
 * each card's size (an unsized card counts as M, a node without a work item as
 * an unsized card). Completed cards take no part in it.
 */
COORDINA_GRAPH_EXPORT size_t coordina_graph_critical_path(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  const coordina_graph_work_item* work_items,
  size_t work_item_count,
  coordina_graph_node_id* output,
  size_t output_capacity);

COORDINA_GRAPH_EXPORT size_t coordina_graph_plan_work(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  const coordina_graph_work_item* work_items,
  size_t work_item_count,
  const coordina_graph_planning_options* options,
  coordina_graph_work_recommendation* output,
  size_t output_capacity);

/*
 * Builds the graph once and returns everything the board planning view needs:
 * acyclicity, ready nodes, dependency order, critical path, node analyses, and
 * work recommendations. Results match the individual functions above.
 */
COORDINA_GRAPH_EXPORT coordina_graph_status coordina_graph_analyze_board(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  const coordina_graph_work_item* work_items,
  size_t work_item_count,
  const coordina_graph_planning_options* options,
  coordina_graph_board_analysis* output);

/*
 * Forecasts when a milestone ships. Its scope is the milestone and every card
 * it depends on, directly or not; completed cards are ignored. Each run draws
 * a duration for every open card within its size's range, from a random
 * stream of the card's own, then schedules the cards: each person works 6 focus hours a day
 * on one card at a time, cards start once their dependencies are done, and
 * people pick cards up in the board's plan_work order (computed for nobody in
 * particular). A card goes to the first of its assignees who is free, or to
 * anyone when it has none. Runs spread over threads.
 *
 * Returns COORDINA_GRAPH_INVALID_ARGUMENT when the milestone is not a node,
 * person_count or run_count is 0, or an assignee is outside the team, and
 * COORDINA_GRAPH_CYCLE when the open cards depend on each other in a loop.
 */
COORDINA_GRAPH_EXPORT coordina_graph_status coordina_graph_forecast(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  const coordina_graph_work_item* work_items,
  size_t work_item_count,
  const coordina_graph_assignee* assignees,
  size_t assignee_count,
  const coordina_graph_forecast_options* options,
  coordina_graph_milestone_forecast* output);

#ifdef __cplusplus
}
#endif

#endif
