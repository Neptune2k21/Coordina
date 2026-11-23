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

/* Options of coordina_graph_propose_scope and coordina_graph_evaluate_scope. */
typedef struct coordina_graph_scope_options
{
  coordina_graph_node_id milestone;
  /* The same seed gives the same result, whatever thread_count is. */
  uint64_t seed;
  /* Focus hours, 6 a working day, from now to the end of the target day. */
  double target_hours;
  /* The chance of shipping by the target a proposal aims for, above 0 and at most 1. */
  double goal;
  uint32_t person_count;
  /* Runs that measure the scope before and after the cut. */
  uint32_t run_count;
  /* Runs that compare candidates during the search, on the same draws. */
  uint32_t screen_run_count;
  /* 0 uses every core. */
  uint32_t thread_count;
  /*
   * The search compares at most evaluation_limit candidates. When
   * time_limit_ms is not 0, it also stops early enough for the whole call to
   * end within it. Only the time limit makes the result depend on the machine.
   */
  uint32_t evaluation_limit;
  uint32_t time_limit_ms;
} coordina_graph_scope_options;

typedef struct coordina_graph_dropped_card
{
  coordina_graph_node_id node;
  /* Index in cuts of the cut that takes the card out. */
  uint32_t cut;
  uint32_t reserved;
} coordina_graph_dropped_card;

/*
 * Output of coordina_graph_propose_scope and coordina_graph_evaluate_scope.
 * The caller owns both buffers and sets their capacities; node_count is always
 * enough. If one is too small the call returns
 * COORDINA_GRAPH_INSUFFICIENT_CAPACITY, sets both counts and writes no card.
 */
typedef struct coordina_graph_scope_cut
{
  /* The cards to take out, in the order the search took them. */
  coordina_graph_node_id* cuts;
  size_t cut_capacity;
  size_t cut_count;
  /* Every card that leaves the scope, cuts included. */
  coordina_graph_dropped_card* dropped;
  size_t dropped_capacity;
  size_t dropped_count;
  /* The share of runs done by the target, from 0 to 1. */
  double on_time_before;
  double on_time_after;
  /* Working days until the scope left is done: one chance in two, and 85%. */
  double p50_days;
  double p85_days;
} coordina_graph_scope_cut;

/* Options of coordina_graph_propose_staffing and coordina_graph_evaluate_staffing. */
typedef struct coordina_graph_staffing_options
{
  coordina_graph_node_id milestone;
  /* The same seed gives the same plan, whatever thread_count is. */
  uint64_t seed;
  /* Everyone the plan involves: the assignees of open cards and the receivers. */
  uint32_t person_count;
  /* Runs that measure the plan before and after. */
  uint32_t run_count;
  /* Runs that compare plans during the search, on the same draws. */
  uint32_t screen_run_count;
  /* 0 uses every core. */
  uint32_t thread_count;
  /* 1 lets the search move cards that already have assignees. */
  uint32_t allow_reassignment;
  /*
   * The search compares at most evaluation_limit plans. When time_limit_ms is
   * not 0, it also stops early enough for the whole call to end within it.
   * Only the time limit makes the result depend on the machine.
   */
  uint32_t evaluation_limit;
  uint32_t time_limit_ms;
  uint32_t reserved;
} coordina_graph_staffing_options;

typedef struct coordina_graph_staff_change
{
  coordina_graph_node_id node;
  /* The card's only assignee once the plan is applied. */
  uint32_t person;
  uint32_t reserved;
  /* Working days from now until the card starts, if every card takes its likely time. */
  double start_days_before;
  double start_days_after;
} coordina_graph_staff_change;

/*
 * Output of coordina_graph_propose_staffing and
 * coordina_graph_evaluate_staffing. The caller owns the change buffer and
 * sets its capacity; node_count is always enough. If it is too small the call
 * returns COORDINA_GRAPH_INSUFFICIENT_CAPACITY, sets change_count and writes
 * no change.
 */
typedef struct coordina_graph_staffing
{
  /* The cards to give to someone else, the first to start first. */
  coordina_graph_staff_change* changes;
  size_t change_capacity;
  size_t change_count;
  /* Working days until the scope is done, before and after: one chance in two, and 85%. */
  double p50_days_before;
  double p85_days_before;
  double p50_days_after;
  double p85_days_after;
} coordina_graph_staffing;

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

/*
 * Proposes cards to take out of a milestone so it ships by the target. Taking
 * a card out also takes out the cards of the scope that depend on it, and the
 * cards the milestone then no longer needs; a card it still needs through
 * another path stays. Applying the cut means dropping the milestone's
 * dependencies on the cards that leave.
 *
 * The proposal aims for the goal's chance of shipping by the target while
 * giving up as little value as possible: a card is worth its likely hours,
 * weighted by priority (high 4, medium 2, otherwise 1). The milestone, cards
 * in progress, the keep cards, and any card whose cut would take one of them
 * out are never proposed. Cards are scheduled as in coordina_graph_forecast,
 * on the same random streams.
 *
 * Returns COORDINA_GRAPH_INVALID_ARGUMENT for the arguments
 * coordina_graph_forecast refuses, a goal outside (0, 1], or a run_count or
 * screen_run_count of 0, and COORDINA_GRAPH_CYCLE for a loop.
 */
COORDINA_GRAPH_EXPORT coordina_graph_status coordina_graph_propose_scope(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  const coordina_graph_work_item* work_items,
  size_t work_item_count,
  const coordina_graph_assignee* assignees,
  size_t assignee_count,
  const coordina_graph_node_id* keep,
  size_t keep_count,
  const coordina_graph_scope_options* options,
  coordina_graph_scope_cut* output);

/*
 * Measures a cut someone chose, as coordina_graph_propose_scope measures its
 * proposals. Cards outside the scope and the milestone itself are ignored.
 */
COORDINA_GRAPH_EXPORT coordina_graph_status coordina_graph_evaluate_scope(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  const coordina_graph_work_item* work_items,
  size_t work_item_count,
  const coordina_graph_assignee* assignees,
  size_t assignee_count,
  const coordina_graph_node_id* cuts,
  size_t cut_count,
  const coordina_graph_scope_options* options,
  coordina_graph_scope_cut* output);

/*
 * Proposes who should take which card so a milestone ships soonest: its 85%
 * date first, then its 50% one. Cards are scheduled as in
 * coordina_graph_forecast, on the same random streams, with one difference
 * that a plan can change: the team is whoever is assigned to an open card,
 * and only they take unassigned cards; while no one is, a single person
 * nobody in particular does the work. The assignees are those of every open
 * card of the board, so the dates after are the forecast the milestone shows
 * once the plan is applied.
 *
 * A change gives a card to one of the receivers, who becomes its only
 * assignee. Cards in progress never move, nor cards with assignees unless
 * allow_reassignment is 1. A card goes preferably to someone skilled for it,
 * a skilled pair saying that person has done cards like it; giving it to
 * someone else must ship the milestone two focus hours sooner. The search
 * compares plans on screen_run_count runs (simulated annealing in a fixed
 * number of chains), drops the changes the plan does not need, and proposes
 * nothing when the plan found would not bring a date in.
 *
 * Returns COORDINA_GRAPH_INVALID_ARGUMENT when the milestone is not a node,
 * run_count or screen_run_count is 0, or a person is outside person_count, and
 * COORDINA_GRAPH_CYCLE when the open cards depend on each other in a loop.
 */
COORDINA_GRAPH_EXPORT coordina_graph_status coordina_graph_propose_staffing(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  const coordina_graph_work_item* work_items,
  size_t work_item_count,
  const coordina_graph_assignee* assignees,
  size_t assignee_count,
  const uint32_t* receivers,
  size_t receiver_count,
  const coordina_graph_assignee* skilled,
  size_t skilled_count,
  const coordina_graph_staffing_options* options,
  coordina_graph_staffing* output);

/*
 * Measures changes someone chose, each giving a card to one person, as
 * coordina_graph_propose_staffing measures its plans. Cards outside the scope
 * are ignored, and so are changes that give a card to its only assignee.
 */
COORDINA_GRAPH_EXPORT coordina_graph_status coordina_graph_evaluate_staffing(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  const coordina_graph_work_item* work_items,
  size_t work_item_count,
  const coordina_graph_assignee* assignees,
  size_t assignee_count,
  const coordina_graph_assignee* changes,
  size_t change_count,
  const coordina_graph_staffing_options* options,
  coordina_graph_staffing* output);

#ifdef __cplusplus
}
#endif

#endif
