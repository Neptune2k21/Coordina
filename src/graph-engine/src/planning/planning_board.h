#ifndef COORDINA_GRAPH_ENGINE_PLANNING_BOARD_H
#define COORDINA_GRAPH_ENGINE_PLANNING_BOARD_H

#include "core/graph_types.h"
#include "planning/work_planner.h"

#include <cstdint>
#include <vector>

namespace coordina::graph
{
struct Assignee
{
  NodeId node{};
  std::uint32_t person = 0U;
};

struct CardLabel
{
  NodeId node{};
  std::uint32_t label = 0U;
};

// A card with a target date, whose scope is the card and every open card it
// depends on through open cards.
struct Milestone
{
  NodeId node{};
  // Focus hours from now to the end of the target day; negative once passed.
  double target_hours = 0.0;
  // What being late costs, against the other milestones.
  double weight = 1.0;
};

// How long a completed card really took.
struct FinishedCard
{
  NodeId node{};
  double hours = 0.0;
  // Days since it was completed: recent cards say more about the next ones.
  double age_days = 0.0;
};

// Work found on the way: cards added to a milestone's scope after its work
// started, against the cards completed in the same scopes.
struct ScopeGrowth
{
  std::uint32_t discovered = 0U;
  std::uint32_t finished = 0U;
};

// Everything the planning functions know about a board. Assignees and labels
// cover completed cards too: they say who did them and what they were.
struct PlanningBoard
{
  std::vector<NodeId> nodes;
  std::vector<Edge> edges;
  std::vector<WorkItem> items;
  std::vector<Assignee> assignees;
  std::vector<CardLabel> labels;
  std::vector<Milestone> milestones;
  std::vector<FinishedCard> history;
  ScopeGrowth growth;
  std::uint32_t person_count = 0U;
  // People nobody has named yet, who would take unassigned cards too.
  std::uint32_t extra_people = 0U;
};

enum class PlanError : std::uint8_t
{
  none = 0U,
  unknown_milestone = 1U,
  unknown_person = 2U,
  bad_number = 3U,
  cycle = 4U
};

const char* describe(PlanError error) noexcept;
}

#endif
