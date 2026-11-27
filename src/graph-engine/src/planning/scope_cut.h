#ifndef COORDINA_GRAPH_ENGINE_SCOPE_CUT_H
#define COORDINA_GRAPH_ENGINE_SCOPE_CUT_H

#include "planning/forecast.h"
#include "planning/planning_board.h"

#include <chrono>
#include <cstdint>
#include <vector>

namespace coordina::graph
{
struct ScopeOptions
{
  NodeId milestone{};
  // The chance of shipping by the target that a proposal aims for.
  double goal = 0.85;
  SimulationOptions simulation;
  // Runs that compare candidates during the search, on the same draws.
  std::uint32_t screen_run_count = 256U;
  // The search compares at most this many candidates. When time_limit is not
  // zero, it also stops early enough for the whole call to end within it.
  // Only the time limit makes the result depend on the machine.
  std::uint32_t evaluation_limit = 120U;
  std::chrono::milliseconds time_limit{0};
};

struct DroppedCard
{
  NodeId node{};
  // Index in ScopeCut::cuts of the cut that takes the card out.
  std::uint32_t cut = 0U;
};

// Taking a card out of a milestone also takes out every card of the scope
// that depends on it, since they cannot ship without it, and the cards the
// milestone then no longer needs. A card it still needs through another path
// stays. Applying the cut means dropping the milestone's dependencies on the
// cards that leave; those another milestone needs are still done for it.
struct ScopeCut
{
  PlanError error = PlanError::none;
  // The cards chosen, in the order the search took them; none of them is
  // already taken out by another.
  std::vector<NodeId> cuts;
  // Every card that leaves the scope, cuts included.
  std::vector<DroppedCard> dropped;
  // The milestone's forecast before the cut, and the one it shows once the
  // cut is applied.
  double on_time_before = 0.0;
  double on_time_after = 0.0;
  double p50_days = 0.0;
  double p85_days = 0.0;
};

// Looks for the cut that gives the best chance of shipping by the target,
// reaching the goal if it can, while giving up as little value as possible: a
// card is worth its priority, whatever its size. The milestone, cards in
// progress, the kept cards, and any card whose cut would take one of them out
// are never proposed.
ScopeCut propose_scope(
  const PlanningBoard& board,
  const std::vector<NodeId>& keep,
  const ScopeOptions& options);

// Measures a cut someone chose. Cards outside the scope and the milestone
// itself are ignored.
ScopeCut evaluate_scope(
  const PlanningBoard& board,
  const std::vector<NodeId>& cuts,
  const ScopeOptions& options);
}

#endif
