#ifndef COORDINA_GRAPH_ENGINE_PLAN_MODEL_H
#define COORDINA_GRAPH_ENGINE_PLAN_MODEL_H

#include "planning/board_index.h"
#include "planning/duration_model.h"
#include "planning/planning_board.h"

#include <cstddef>
#include <cstdint>
#include <limits>
#include <span>
#include <vector>

namespace coordina::graph
{
// What a card's duration is drawn from.
struct CardDuration
{
  CardSize size = CardSize::none;
  // The logarithm of the size's likely hours, and its spread; for an unsized
  // card each run draws the size first.
  double log_likely = 0.0;
  double sigma = 0.0;
  double elapsed_hours = 0.0;
};

struct PlanMilestone
{
  static constexpr auto done = std::numeric_limits<std::size_t>::max();

  NodeId node{};
  // Its card, or done when it is completed.
  std::size_t card = done;
  double target_hours = 0.0;
  double weight = 1.0;
  std::uint32_t card_count = 0U;
  std::uint32_t unsized_count = 0U;
};

// The work of a board's milestones, sharing one team: the open cards of every
// scope, plus the cards in progress outside them, which keep their people
// busy until they are done.
//
// A card's index is its place in the order people pick cards up: cards in
// progress first, then by the latest they can start without making a
// milestone late (see pick_order.h). Every relation is a flat index array the
// scheduler reads without hashing.
//
// The team is whoever is assigned to an open card of the board: they take the
// unassigned cards. The model adds one last person, nobody in particular, who
// does the work while no one is assigned to anything.
class PlanModel
{
public:
  PlanModel(const PlanningBoard& board, const BoardIndex& index, const DurationModel& durations);

  PlanError error() const noexcept;
  std::size_t card_count() const noexcept;
  // Everyone, nobody in particular included.
  std::uint32_t person_count() const noexcept;
  std::uint32_t nobody() const noexcept;
  const std::vector<NodeId>& nodes() const noexcept;
  const WorkItem& item(std::size_t card) const noexcept;
  std::uint32_t dependency_count(std::size_t card) const noexcept;
  std::span<const std::uint32_t> dependencies(std::size_t card) const noexcept;
  std::span<const std::uint32_t> dependents(std::size_t card) const noexcept;
  // Sorted; empty means anyone of the team may take the card.
  std::span<const std::uint32_t> people(std::size_t card) const noexcept;
  // Bit p of word p / 64 marks the people who take unassigned cards.
  std::span<const std::uint64_t> team() const noexcept;
  const CardDuration& duration(std::size_t card) const noexcept;
  std::span<const EffectWeight> effects(std::size_t card) const noexcept;
  // The hours a plan counts for each card: the median of what is left of it.
  const std::vector<double>& planned_hours() const noexcept;
  const std::vector<PlanMilestone>& milestones() const noexcept;
  bool in_scope(std::size_t milestone, std::size_t card) const noexcept;
  // A card whose milestones depend on nothing: it only keeps people busy.
  bool is_outside_scopes(std::size_t card) const noexcept;

private:
  void collect_cards(const PlanningBoard& board, const BoardIndex& index);
  void mark_scope(const BoardIndex& index, std::size_t milestone, std::uint32_t card);
  void order_cards(const BoardIndex& index);
  void link_cards(const BoardIndex& index);
  void gather_team(const PlanningBoard& board, const BoardIndex& index);
  void measure_durations(const BoardIndex& index, const DurationModel& durations);
  static double planned_hours_of(
    const CardDuration& duration,
    std::span<const EffectWeight> effects,
    const DurationModel& durations);

  // The board index of each card.
  std::vector<std::uint32_t> cards_;
  std::vector<NodeId> nodes_;
  std::vector<WorkItem> items_;
  IndexRows dependencies_;
  IndexRows dependents_;
  IndexRows people_;
  std::vector<std::uint64_t> team_;
  std::vector<CardDuration> durations_;
  std::vector<std::size_t> effect_offsets_;
  std::vector<EffectWeight> effects_;
  std::vector<double> planned_hours_;
  std::vector<PlanMilestone> milestones_;
  // One row per milestone.
  std::vector<std::vector<std::uint8_t>> scopes_;
  std::uint32_t person_count_ = 0U;
  PlanError error_ = PlanError::none;
};
}

#endif
