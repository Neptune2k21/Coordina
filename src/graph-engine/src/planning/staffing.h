#ifndef COORDINA_GRAPH_ENGINE_STAFFING_H
#define COORDINA_GRAPH_ENGINE_STAFFING_H

#include "planning/forecast_model.h"

#include <chrono>
#include <cstdint>
#include <vector>

namespace coordina::graph
{
struct StaffingOptions
{
  std::uint64_t seed = 0U;
  // Runs that measure the plan before and after, as many as a forecast's.
  std::uint32_t run_count = 2000U;
  // Runs that compare plans during the search, on the same draws.
  std::uint32_t screen_run_count = 8U;
  // 0 uses every core; the result is the same for any count.
  std::uint32_t thread_count = 0U;
  // Whether the search may move cards that already have assignees.
  bool allow_reassignment = false;
  // The search compares at most this many plans. When time_limit is not
  // zero, it also stops early enough for the whole call to end within it.
  // Only the time limit makes the result depend on the machine.
  std::uint32_t evaluation_limit = 20000U;
  std::chrono::milliseconds time_limit{0};
};

// The people around a milestone. The model's last person is nobody in
// particular: no card goes to them, and they take the unassigned cards only
// while no one is assigned to any open card, as a forecast plans a board
// nobody is assigned on.
struct StaffingTeam
{
  // The people the search may give cards to.
  std::vector<std::uint32_t> receivers;
  // Every assignee of an open card of the board. Whoever is assigned to an
  // open card is on the team and takes unassigned cards when free.
  std::vector<Assignee> assignees;
  // A person who has done cards like this one, sharing one of its labels.
  std::vector<Assignee> skilled;
};

struct StaffChange
{
  NodeId node{};
  std::uint32_t person = 0U;
  // Working days from now to when the card starts if every card takes its
  // likely time, before and after the plan.
  double start_days_before = 0.0;
  double start_days_after = 0.0;
};

struct StaffingPlan
{
  // The cards to give to one person each, the first to start first. Applying
  // the plan makes that person the card's only assignee.
  std::vector<StaffChange> changes;
  // Working days until the scope is done, before and after the plan: one
  // chance in two, and 85%. After is the forecast the milestone shows once
  // the plan is applied.
  double p50_days_before = 0.0;
  double p85_days_before = 0.0;
  double p50_days_after = 0.0;
  double p85_days_after = 0.0;
};

// Looks for who should take which card so the milestone ships soonest: its
// 85% date first, then its 50% one. Cards in progress never move, nor cards
// with assignees unless the options allow it, and a card only goes to one of
// the receivers, preferably someone skilled for it. A plan that would not
// bring a date in proposes no change.
StaffingPlan propose_staffing(
  const ForecastModel& model,
  const std::vector<WorkItem>& work_items,
  const StaffingTeam& team,
  const StaffingOptions& options);

// Measures changes someone chose. Cards outside the scope are ignored, and so
// are changes that give a card to its only assignee.
StaffingPlan evaluate_staffing(
  const ForecastModel& model,
  const std::vector<WorkItem>& work_items,
  const StaffingTeam& team,
  const std::vector<Assignee>& changes,
  const StaffingOptions& options);
}

#endif
