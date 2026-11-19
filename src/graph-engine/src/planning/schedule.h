#ifndef COORDINA_GRAPH_ENGINE_SCHEDULE_H
#define COORDINA_GRAPH_ENGINE_SCHEDULE_H

#include "core/graph_types.h"
#include "planning/card_size.h"

#include <cstddef>
#include <cstdint>
#include <span>
#include <vector>

namespace coordina::graph
{
// An open card of a milestone's scope, as the schedule sees it.
struct MilestoneCard
{
  NodeId node{};
  SizeRange range;
  // The people who may take it, in order; nobody means anyone.
  std::vector<std::uint32_t> people;
  // Indexes of the cards that wait for this one.
  std::vector<std::size_t> dependents;
  std::size_t dependency_count = 0U;
};

struct ScheduledCard
{
  NodeId node{};
  std::uint32_t person = 0U;
  double start_hours = 0.0;
  double finish_hours = 0.0;
};

// One way the work can unfold. Time counts focus hours on a shared clock:
// day d runs from d * focus_hours_per_day to the start of day d + 1.
struct Schedule
{
  // In card order.
  std::vector<ScheduledCard> cards;
  // The cards that set the finish, first one first, as indexes into cards.
  // Each one waited for the one before: a dependency, or its person.
  std::vector<std::size_t> driving_chain;
  double finish_hours = 0.0;
};

// Each person works on one card at a time, and a card starts once its open
// dependencies are done. Whenever people are free, the first card in order
// that one of them can take goes to the first of them who can: one of its
// assignees, or anyone when it has none. A card's index is its place in that
// order, and hours holds one duration per card.
Schedule simulate_schedule(
  std::span<const MilestoneCard> cards,
  std::uint32_t person_count,
  std::span<const double> hours);
}

#endif
