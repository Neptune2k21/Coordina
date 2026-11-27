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
  /* Focus hours already spent on a card in progress. */
  double elapsed_hours;
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
  COORDINA_GRAPH_CYCLE = 3,
  COORDINA_GRAPH_OUT_OF_MEMORY = 4,
  COORDINA_GRAPH_INTERNAL_ERROR = 5
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

/* A person on a card, open or done. People are numbered from 0 to person_count. */
typedef struct coordina_graph_assignee
{
  coordina_graph_node_id node;
  uint32_t person;
  uint32_t reserved;
} coordina_graph_assignee;

/* A label of a card, open or done, under any number. */
typedef struct coordina_graph_card_label
{
  coordina_graph_node_id node;
  uint32_t label;
  uint32_t reserved;
} coordina_graph_card_label;

/*
 * A card with a target date. Its scope is the card and every open card it
 * depends on, directly or through open cards.
 */
typedef struct coordina_graph_milestone
{
  coordina_graph_node_id node;
  /* Focus hours, 6 a working day, from now to the end of the target day. */
  double target_hours;
  /* What being late costs against the other milestones, above 0. */
  double weight;
} coordina_graph_milestone;

/* How long a completed card took. */
typedef struct coordina_graph_finished_card
{
  coordina_graph_node_id node;
  /* Focus hours, above 0. */
  double hours;
  /* Days since it was completed. */
  double age_days;
} coordina_graph_finished_card;

/*
 * Everything the planning functions read about a board. Assignees and labels
 * of completed cards say who did them and what they were. The caller owns
 * every array.
 */
typedef struct coordina_graph_board
{
  const coordina_graph_node_id* nodes;
  size_t node_count;
  const coordina_graph_edge* edges;
  size_t edge_count;
  const coordina_graph_work_item* work_items;
  size_t work_item_count;
  const coordina_graph_assignee* assignees;
  size_t assignee_count;
  const coordina_graph_card_label* labels;
  size_t label_count;
  const coordina_graph_milestone* milestones;
  size_t milestone_count;
  const coordina_graph_finished_card* history;
  size_t history_count;
  uint32_t person_count;
  /* Cards added to a milestone's scope after its work started. */
  uint32_t discovered_count;
  /* Cards completed in those scopes. */
  uint32_t finished_count;
  /* People nobody has named yet, who would take unassigned cards too. */
  uint32_t extra_people;
} coordina_graph_board;

typedef struct coordina_graph_simulation
{
  /* The same seed gives the same result, whatever thread_count is. */
  uint64_t seed;
  /* From 1 to 100000. */
  uint32_t run_count;
  /* 0 uses the cores other calls leave free. */
  uint32_t thread_count;
} coordina_graph_simulation;

typedef struct coordina_graph_milestone_forecast
{
  coordina_graph_node_id node;
  /* Working days until the scope is done, with 10, 50, 85 and 95% chances. */
  double p10_days;
  double p50_days;
  double p85_days;
  double p95_days;
  /* The share of runs done by the target, from 0 to 1. */
  double on_time;
  /* How late it is on average, counting 0 when on time. */
  double late_days;
  /* The 50% date with as many people as it takes. */
  double chain_days;
  uint32_t card_count;
  /* Open cards of the scope without a size. */
  uint32_t unsized_count;
} coordina_graph_milestone_forecast;

typedef struct coordina_graph_card_forecast
{
  coordina_graph_node_id node;
  /* The index of the milestone in the board's. */
  uint32_t milestone;
  uint32_t reserved;
  /* The share of runs where the card drives the date, from 0 to 1. */
  double criticality;
  /*
   * The correlation between the card's own uncertainty and the date, from -1
   * to 1. Squared, it is the share of the date's spread the card explains.
   */
  double sensitivity;
} coordina_graph_card_forecast;

typedef struct coordina_graph_planned_card
{
  coordina_graph_node_id node;
  /* person_count or more when nobody in particular is expected to take it. */
  uint32_t person;
  uint32_t reserved;
  /* Working days from now, if every card takes its median time. */
  double start_days;
  double finish_days;
} coordina_graph_planned_card;

/*
 * Output of coordina_graph_forecast. The caller owns the buffers and sets
 * their capacities; milestone_count milestones, node_count planned cards, and
 * milestone_count * node_count cards are always enough. If one is too small
 * the call returns COORDINA_GRAPH_INSUFFICIENT_CAPACITY, sets the counts and
 * writes nothing else.
 */
typedef struct coordina_graph_forecast_output
{
  /* In the board's order. */
  coordina_graph_milestone_forecast* milestones;
  size_t milestone_capacity;
  size_t milestone_count;
  /* By milestone, then most critical first. */
  coordina_graph_card_forecast* cards;
  size_t card_capacity;
  size_t card_count;
  /* The open cards of every scope, in the order to pick them up. */
  coordina_graph_planned_card* plan;
  size_t plan_capacity;
  size_t plan_count;
} coordina_graph_forecast_output;

/* Options of coordina_graph_propose_scope and coordina_graph_evaluate_scope. */
typedef struct coordina_graph_scope_options
{
  /* One of the board's milestones. */
  coordina_graph_node_id milestone;
  coordina_graph_simulation simulation;
  /* The chance of shipping by the target a proposal aims for, above 0 and at most 1. */
  double goal;
  /* Runs that compare candidates during the search, on the same draws. */
  uint32_t screen_run_count;
  /*
   * The search compares at most evaluation_limit candidates. When
   * time_limit_ms is not 0, it also stops early enough for the whole call to
   * end within it. Only the time limit makes the result depend on the machine.
   */
  uint32_t evaluation_limit;
  uint32_t time_limit_ms;
  uint32_t reserved;
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
  /* The milestone's forecast now, and the one it shows once the cut is applied. */
  double on_time_before;
  double on_time_after;
  double p50_days;
  double p85_days;
} coordina_graph_scope_cut;

/* Options of coordina_graph_propose_staffing and coordina_graph_evaluate_staffing. */
typedef struct coordina_graph_staffing_options
{
  /* One of the board's milestones. */
  coordina_graph_node_id milestone;
  coordina_graph_simulation simulation;
  /* Runs that compare plans during the search, on the same draws. */
  uint32_t screen_run_count;
  /* 1 lets the search move cards that already have assignees. */
  uint32_t allow_reassignment;
  /*
   * The search compares at most evaluation_limit plans. When time_limit_ms is
   * not 0, it also stops early enough for the whole call to end within it.
   * Only the time limit makes the result depend on the machine.
   */
  uint32_t evaluation_limit;
  uint32_t time_limit_ms;
} coordina_graph_staffing_options;

typedef struct coordina_graph_staff_change
{
  coordina_graph_node_id node;
  /* The card's only assignee once the plan is applied. */
  uint32_t person;
  uint32_t reserved;
  /* Working days from now until the card starts, if every card takes its median time. */
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
  /* The milestone's forecast now, and the one it shows once the plan is applied. */
  double p50_days_before;
  double p85_days_before;
  double p50_days_after;
  double p85_days_after;
  /* How late the other milestones are on average, together, by their weights. */
  double others_late_days_before;
  double others_late_days_after;
} coordina_graph_staffing;

/*
 * How long cards take against what their size says: 1.6 means 60% longer. A
 * tenth of the chance is below low and a tenth above high.
 */
typedef struct coordina_graph_duration_ratio
{
  double low;
  double median;
  double high;
  /* A size, or a label's number; 0 for the whole team. */
  uint32_t key;
  /* The completed cards it was learned from. */
  uint32_t card_count;
} coordina_graph_duration_ratio;

/*
 * Output of coordina_graph_calibrate. Six sizes and 32 labels are always
 * enough.
 */
typedef struct coordina_graph_calibration
{
  /* From XS to XXL. */
  coordina_graph_duration_ratio* sizes;
  size_t size_capacity;
  size_t size_count;
  coordina_graph_duration_ratio* labels;
  size_t label_capacity;
  size_t label_count;
  coordina_graph_duration_ratio team;
  /*
   * Each card predicted from the ones completed before it: the share that took
   * no longer than its 50% and 85% durations.
   */
  double within_p50;
  double within_p85;
  /* New cards found per completed card. */
  double growth;
  uint32_t replayed_count;
  uint32_t reserved;
} coordina_graph_calibration;

COORDINA_GRAPH_EXPORT const char* coordina_graph_version(void);

/*
 * Why the last call on this thread failed, or an empty string. Functions that
 * return a count report a failure as 0 and through this. The text is valid
 * until the next call on the thread.
 */
COORDINA_GRAPH_EXPORT const char* coordina_graph_last_error(void);

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
 * Forecasts when every milestone ships, all sharing one team. Each run draws a
 * duration for every open card of their scopes, then schedules the cards: each
 * person works 6 focus hours a day on one card at a time, and a card starts
 * once its dependencies are done.
 *
 * Durations: a size stands for a range, not a number. What the board's history
 * teaches about this team, its sizes, people and labels shifts it, and each run
 * draws those shifts once for all its cards, so that when the team is slow
 * every card is. A few cards run away from their size (a power-law tail), an
 * unsized card draws its size first, a card in progress is drawn given the time
 * it already took, and completed cards uncover new ones at the rate the board
 * has seen.
 *
 * Order: cards in progress first, then by the latest they can start without
 * making a milestone late. A card goes to one of its assignees who is free, or
 * to anyone of the team when it has none, whoever has least work of their own
 * left. The team is whoever is assigned to an open card of the board; while no
 * one is, a single person nobody in particular does the work. Cards in progress
 * outside the scopes keep their people busy first.
 *
 * Every planning function returns COORDINA_GRAPH_INVALID_ARGUMENT for a null
 * array with a count, a milestone that is not a node, a person outside
 * person_count, a number that is not finite or outside its range, and
 * COORDINA_GRAPH_CYCLE when the open cards depend on each other in a loop;
 * coordina_graph_last_error says which.
 */
COORDINA_GRAPH_EXPORT coordina_graph_status coordina_graph_forecast(
  const coordina_graph_board* board,
  const coordina_graph_simulation* simulation,
  coordina_graph_forecast_output* output);

/*
 * Proposes cards to take out of a milestone so it ships by its target. Taking
 * a card out also takes out the cards of the scope that depend on it, and the
 * cards the milestone then no longer needs; a card it still needs through
 * another path stays, and a card another milestone needs is still done for it.
 * Applying the cut means dropping the milestone's dependencies on the cards
 * that leave.
 *
 * The proposal aims for the goal's chance of shipping by the target while
 * giving up as little value as possible: a card is worth its priority (high 4,
 * medium 2, otherwise 1), whatever its size. The milestone, cards in progress,
 * the keep cards, and any card whose cut would take one of them out are never
 * proposed.
 */
COORDINA_GRAPH_EXPORT coordina_graph_status coordina_graph_propose_scope(
  const coordina_graph_board* board,
  const coordina_graph_node_id* keep,
  size_t keep_count,
  const coordina_graph_scope_options* options,
  coordina_graph_scope_cut* output);

/*
 * Measures a cut someone chose, as coordina_graph_propose_scope measures its
 * proposals. Cards outside the scope and the milestone itself are ignored.
 */
COORDINA_GRAPH_EXPORT coordina_graph_status coordina_graph_evaluate_scope(
  const coordina_graph_board* board,
  const coordina_graph_node_id* cuts,
  size_t cut_count,
  const coordina_graph_scope_options* options,
  coordina_graph_scope_cut* output);

/*
 * Proposes who should take which card so a milestone ships soonest: its 85%
 * date first, then its 50% one, without making the other milestones late for
 * it.
 *
 * A change gives a card of the scope to one of the receivers, who becomes its
 * only assignee. Cards in progress never move, nor cards with assignees unless
 * allow_reassignment is 1. A card goes preferably to someone skilled for it,
 * a skilled pair saying that person has done cards like it; giving it to
 * someone else must ship the milestone two focus hours sooner, and taking a card
 * off someone half a day sooner. The search
 * compares plans on screen_run_count runs (simulated annealing in a fixed
 * number of chains), drops the changes the plan does not need, and proposes
 * nothing when the plan found would not bring a date in.
 */
COORDINA_GRAPH_EXPORT coordina_graph_status coordina_graph_propose_staffing(
  const coordina_graph_board* board,
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
  const coordina_graph_board* board,
  const coordina_graph_assignee* changes,
  size_t change_count,
  const coordina_graph_staffing_options* options,
  coordina_graph_staffing* output);

/* What the board's history has taught, and how right the forecasts would have been. */
COORDINA_GRAPH_EXPORT coordina_graph_status coordina_graph_calibrate(
  const coordina_graph_board* board,
  coordina_graph_calibration* output);

#ifdef __cplusplus
}
#endif

#endif
