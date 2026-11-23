#ifndef COORDINA_GRAPH_ENGINE_SCHEDULE_H
#define COORDINA_GRAPH_ENGINE_SCHEDULE_H

#include "planning/forecast_model.h"
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

// Time counts focus hours on a shared clock: day d runs from
// d * focus_hours_per_day to the start of day d + 1.
// Who takes the cards in a run where a plan changes the model's assignees.
struct Staffing
{
  static constexpr auto keep_assignees = std::numeric_limits<std::uint32_t>::max();

  // One per card: the only person who may take it, or keep_assignees for the
  // model's own. Empty keeps every card's.
  std::span<const std::uint32_t> people;
  // Bit p of word p / 64 marks the people who take the cards nobody is
  // assigned to. Empty means everyone does.
  std::span<const std::uint64_t> pool;
};

struct Schedule
{
  std::vector<ScheduledCard> cards;
  // Indexes into cards, first card first: each one waited for the one before,
  // either a dependency or the card its person was finishing.
  std::vector<std::size_t> driving_chain;
  double finish_hours = 0.0;
};

// Simulates one way the work unfolds. Each person works on one card at a
// time and a card starts once its open dependencies are done. Whenever people
// are free, the first card in order that one of them can take goes to the
// first of them who can: one of its assignees, or anyone in the pool when it
// has none.
//
// A scheduler owns every buffer a run needs, so running it again allocates
// nothing: the forecast keeps one per thread for thousands of runs.
class Scheduler
{
public:
  explicit Scheduler(const ForecastModel& model);

  // One duration per card; returns when the last card is done. When active is
  // not empty, the cards whose flag is 0 are left out, as if they were not in
  // the scope: they never start, and the active cards stop waiting for them.
  double run(
    std::span<const double> hours,
    std::span<const std::uint8_t> active = {},
    const Staffing& staffing = {});
  void driving_chain(std::vector<std::size_t>& chain) const;
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

  void leave_out(std::span<const std::uint8_t> active) noexcept;
  void staff(const Staffing& staffing) noexcept;
  std::span<const std::uint32_t> people(std::size_t card) const noexcept;
  void release(std::size_t card) noexcept;
  std::size_t next_card() const noexcept;
  std::uint32_t person_for(std::size_t card) const noexcept;
  bool is_free(std::uint32_t person) const noexcept;
  void start_cards(double now);
  void finish_cards(double now) noexcept;

  const ForecastModel* model_;
  std::span<const double> hours_;
  std::vector<CardState> cards_;
  std::vector<std::uint32_t> last_card_;
  std::vector<std::uint64_t> free_people_;
  std::vector<std::uint64_t> pool_;
  std::span<const std::uint32_t> chosen_;
  std::vector<ReadySet> own_;
  ReadySet shared_;
  std::vector<Running> running_;
  std::size_t last_ = ReadySet::none;
};
}

#endif
