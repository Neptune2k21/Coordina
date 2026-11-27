#ifndef COORDINA_GRAPH_ENGINE_SCHEDULE_H
#define COORDINA_GRAPH_ENGINE_SCHEDULE_H

#include "planning/plan_model.h"
#include "planning/ready_set.h"

#include <cstddef>
#include <cstdint>
#include <limits>
#include <span>
#include <vector>

namespace coordina::graph
{
struct ScheduledCard
{
  NodeId node{};
  std::uint32_t person = 0U;
  double start_hours = 0.0;
  double finish_hours = 0.0;
};

// Who takes the cards in a run where a plan changes the model's assignees.
struct Staffing
{
  static constexpr auto keep_assignees = std::numeric_limits<std::uint32_t>::max();

  // One per card: the only person who may take it, or keep_assignees for the
  // model's own. Empty keeps every card's.
  std::span<const std::uint32_t> people;
  // Bit p of word p / 64 marks the people who take the cards nobody is
  // assigned to. Empty means the model's team.
  std::span<const std::uint64_t> team;
};

// What a run leaves out of one milestone's scope.
struct Cut
{
  // Flags per card. A card that is not active is not done at all: it never
  // starts, and the active cards stop waiting for it. Empty keeps every card.
  std::span<const std::uint8_t> active;
  // The cards the milestone no longer waits for, though other milestones may.
  std::span<const std::uint8_t> dropped;
  std::size_t milestone_card = std::numeric_limits<std::size_t>::max();
};

struct Schedule
{
  std::vector<ScheduledCard> cards;
  double finish_hours = 0.0;
};

// Simulates one way the work unfolds, in focus hours on a shared clock: day d
// runs from d * focus_hours_per_day to the start of day d + 1. Each person
// works on one card at a time and a card starts once its open dependencies are
// done. Whenever people are free, the first card in order that one of them can
// take goes to one of its assignees, or to anyone of the team when it has none:
// to whoever has least work of their own left, so a bottleneck keeps to its
// own cards. The run stops when every milestone is done.
//
// A scheduler owns every buffer a run needs, so running it again allocates
// nothing: the forecast keeps one per thread for thousands of runs.
class Scheduler
{
public:
  explicit Scheduler(const PlanModel& model);

  // One duration per card.
  void run(std::span<const double> hours, const Cut& cut = {}, const Staffing& staffing = {});
  // When the milestone is done in the last run; 0 for a completed one.
  double finish_of(std::size_t milestone) const noexcept;
  // The cards that set when the card ends in the last run, first one first:
  // each waited for the one before, either a dependency or the card its
  // person was finishing.
  void driving_chain(std::size_t card, std::vector<std::size_t>& chain) const;
  // The last run; a card that had not started when it stopped starts at its end.
  Schedule schedule() const;

private:
  struct Running
  {
    double finish;
    std::size_t card;
  };

  // Everything a run changes about a card.
  struct CardState
  {
    double ready_at;
    double start;
    double finish;
    std::uint32_t remaining;
    std::uint32_t person;
    std::uint32_t ready_cause;
    std::uint32_t cause;
  };

  void reset(std::span<const double> hours, const Cut& cut, const Staffing& staffing);
  void leave_out() noexcept;
  bool waits(std::size_t dependent, std::size_t dependency) const noexcept;
  bool is_active(std::size_t card) const noexcept;
  std::span<const std::uint32_t> people(std::size_t card) const noexcept;
  void release(std::size_t card) noexcept;
  bool is_free(std::uint32_t person) const noexcept;
  std::size_t next_card() const noexcept;
  std::uint32_t person_for(std::size_t card) const noexcept;
  void start_cards(double now);
  void start(std::size_t card, double now);
  void finish_cards(double now) noexcept;

  const PlanModel* model_;
  std::span<const double> hours_;
  Cut cut_;
  std::vector<CardState> cards_;
  std::vector<std::uint32_t> last_card_;
  // Per person: the planned hours of the cards only they and others they share
  // them with can take, not started yet.
  std::vector<double> own_hours_;
  std::vector<std::uint64_t> free_people_;
  std::span<const std::uint64_t> team_;
  std::span<const std::uint32_t> chosen_;
  std::vector<ReadySet> own_;
  ReadySet shared_;
  std::vector<Running> running_;
  std::vector<std::uint8_t> is_milestone_;
  std::size_t milestones_left_ = 0U;
  double end_ = 0.0;
};
}

#endif
