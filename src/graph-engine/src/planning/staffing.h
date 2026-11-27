#ifndef COORDINA_GRAPH_ENGINE_STAFFING_H
#define COORDINA_GRAPH_ENGINE_STAFFING_H

#include "planning/forecast.h"
#include "planning/planning_board.h"

#include <chrono>
#include <cstdint>
#include <vector>

namespace coordina::graph
{
struct StaffingOptions
{
  NodeId milestone{};
  SimulationOptions simulation;
  // Runs that compare plans during the search, on the same draws.
  std::uint32_t screen_run_count = 8U;
  // Whether the search may move cards that already have assignees.
  bool allow_reassignment = false;
  // The search compares at most this many plans. When time_limit is not
  // zero, it also stops early enough for the whole call to end within it.
  // Only the time limit makes the result depend on the machine.
  std::uint32_t evaluation_limit = 20000U;
  std::chrono::milliseconds time_limit{0};
};

// Who a plan may involve, besides the people already on the board.
struct StaffingTeam
{
  // The people the search may give cards to.
  std::vector<std::uint32_t> receivers;
  // A person who has done cards like this one, sharing one of its labels.
  std::vector<Assignee> skilled;
};

struct StaffChange
{
  NodeId node{};
  std::uint32_t person = 0U;
  // Working days from now to when the card starts if every card takes its
  // median time, before and after the plan.
  double start_days_before = 0.0;
  double start_days_after = 0.0;
};

struct StaffingPlan
{
  PlanError error = PlanError::none;
  // The cards to give to one person each, the first to start first. Applying
  // the plan makes that person the card's only assignee.
  std::vector<StaffChange> changes;
  // The milestone's forecast before the plan, and the one it shows once the
  // plan is applied, in working days.
  double p50_days_before = 0.0;
  double p85_days_before = 0.0;
  double p50_days_after = 0.0;
  double p85_days_after = 0.0;
  // How late the other milestones are on average, together, by their weights.
  double others_late_days_before = 0.0;
  double others_late_days_after = 0.0;
};

// Looks for who should take which card so the milestone ships soonest: its
// 85% date first, then its 50% one, without making the other milestones late
// for it. Only cards of its scope move, never those in progress, nor those
// with assignees unless the options allow it, and a card only goes to one of
// the receivers, preferably someone skilled for it. A plan that would not
// bring a date in proposes no change.
StaffingPlan propose_staffing(
  const PlanningBoard& board,
  const StaffingTeam& team,
  const StaffingOptions& options);

// Measures changes someone chose. Cards outside the scope are ignored, and so
// are changes that give a card to its only assignee.
StaffingPlan evaluate_staffing(
  const PlanningBoard& board,
  const std::vector<Assignee>& changes,
  const StaffingOptions& options);
}

#endif
