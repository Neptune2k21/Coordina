#ifndef COORDINA_GRAPH_ENGINE_ACTIONS_H
#define COORDINA_GRAPH_ENGINE_ACTIONS_H

#include "planning/forecast.h"
#include "planning/planning_board.h"

#include <cstdint>
#include <vector>

namespace coordina::graph
{
enum class ActionKind : std::uint8_t
{
  cut_card = 1U,
  assign_card = 2U,
  add_person = 3U
};

struct ActionOptions
{
  NodeId milestone{};
  SimulationOptions simulation;
  // Runs that compare the candidates before the best ones are forecast.
  std::uint32_t screen_run_count = 128U;
  std::uint32_t limit = 6U;
};

// One thing to do, and the milestone's forecast once it is done. Every kind is
// measured the same way, so they compare.
struct Action
{
  ActionKind kind = ActionKind::cut_card;
  // The card to cut or assign; none for add_person.
  NodeId node{};
  std::uint32_t person = 0U;
  double on_time_before = 0.0;
  double on_time_after = 0.0;
  double p85_days_before = 0.0;
  double p85_days_after = 0.0;
  // How much later the other milestones end up on average, by their weights.
  double others_delay_days = 0.0;
  // The cards that leave the scope with a cut, the card itself included, and
  // what giving them up costs, by their priorities.
  std::uint32_t dropped_count = 0U;
  double value_lost = 0.0;
};

struct ActionPlan
{
  PlanError error = PlanError::none;
  // The best first: the chance of being on time, less two points for each unit
  // of value given up, then the 85% date.
  std::vector<Action> actions;
};

// Ranks the single steps that help a milestone most: taking a card out, giving
// a card to someone, adding a person. Candidates that cannot matter are left
// out from how much each card drives the date, the rest race on more and more
// of the forecast's runs, and the winners are forecast in full. An action that
// does not help is not proposed.
ActionPlan propose_actions(
  const PlanningBoard& board,
  const std::vector<std::uint32_t>& receivers,
  const ActionOptions& options);
}

#endif
