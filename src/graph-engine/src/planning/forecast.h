#ifndef COORDINA_GRAPH_ENGINE_FORECAST_H
#define COORDINA_GRAPH_ENGINE_FORECAST_H

#include "core/graph_snapshot.h"
#include "planning/card_size.h"
#include "planning/schedule.h"
#include "planning/work_planner.h"

#include <cstddef>
#include <cstdint>
#include <vector>

namespace coordina::graph
{
// A person working on a card. People are numbered from 0 to the team size.
struct Assignee
{
  NodeId node{};
  std::uint32_t person = 0U;
};

enum class ForecastError : std::uint8_t
{
  none = 0U,
  unknown_milestone = 1U,
  // The team is empty, or an assignee is outside it.
  unknown_person = 2U,
  // The open cards depend on each other in a loop, so none can start.
  cycle = 3U
};

struct CardCriticality
{
  NodeId node{};
  // The share of runs where the card is on the driving chain, 0 to 1.
  double criticality = 0.0;
};

struct Forecast
{
  // Working days until the scope is done, for one chance in two and for a
  // realistic commitment.
  double p50_days = 0.0;
  double p85_days = 0.0;
  std::uint32_t unsized_count = 0U;
  // Every open card of the scope, most critical first.
  std::vector<CardCriticality> cards;
};

// The open cards a milestone needs: the milestone and everything it depends
// on, directly or not, that is not completed. Built once, then scheduled with
// as many sets of durations as the forecast draws.
class MilestoneWork
{
public:
  MilestoneWork(
    const GraphSnapshot& graph,
    const std::vector<WorkItem>& work_items,
    const std::vector<Assignee>& assignees,
    NodeId milestone,
    std::uint32_t person_count);

  ForecastError error() const noexcept;

  // In the order people pick cards up: the board's plan_work ranking, the
  // same for everyone who looks at it.
  const std::vector<NodeId>& nodes() const noexcept;

  std::uint32_t unsized_count() const noexcept;

  // Every card takes the likely duration of its size.
  std::vector<double> likely_hours() const;

  // Draws a duration for every card within its size's range. Cards slip
  // further than they come in early, because every range leans that way.
  std::vector<double> draw_hours(std::uint64_t& random_state) const;

  // Schedules the cards with these durations, one per card in nodes() order
  // (see simulate_schedule).
  Schedule schedule(const std::vector<double>& hours) const;

private:
  void collect_scope(
    const GraphSnapshot& graph,
    const std::vector<WorkItem>& work_items,
    NodeId milestone);
  void link_cards(const GraphSnapshot& graph);
  void assign_people(const std::vector<Assignee>& assignees, std::uint32_t person_count);
  bool is_acyclic() const;

  std::vector<MilestoneCard> cards_;
  std::vector<NodeId> nodes_;
  std::uint32_t person_count_ = 0U;
  std::uint32_t unsized_count_ = 0U;
  ForecastError error_ = ForecastError::none;
};

// Schedules the work run_count times with drawn durations. The same seed
// gives the same forecast.
Forecast forecast(const MilestoneWork& work, std::uint32_t run_count, std::uint64_t seed);
}

#endif
