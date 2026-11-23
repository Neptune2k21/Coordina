#ifndef COORDINA_GRAPH_ENGINE_STAFF_SEARCH_H
#define COORDINA_GRAPH_ENGINE_STAFF_SEARCH_H

#include "planning/forecast_model.h"
#include "planning/sampling.h"
#include "planning/schedule.h"
#include "planning/scope_simulator.h"
#include "planning/staffing.h"

#include <chrono>
#include <cstddef>
#include <cstdint>
#include <span>
#include <unordered_map>
#include <vector>

namespace coordina::graph
{
// Giving cards to people so a milestone ships soonest is resource-constrained
// project scheduling, which is NP-hard. The search is a heuristic: simulated
// annealing over who takes each card, in a fixed number of chains that start
// from the current plan or from everyone at hand pitching in. Plans are
// compared on a few runs drawn once, so a plan that leaves room to absorb a
// slip beats one that only fits the likely durations. The best one gives back
// the changes it does not need, then is measured on every run of the
// forecast.
class StaffSearch
{
public:
  StaffSearch(
    const ForecastModel& model,
    const std::vector<WorkItem>& work_items,
    const StaffingTeam& team,
    const StaffingOptions& options);

  StaffingPlan propose();
  StaffingPlan evaluate(const std::vector<Assignee>& changes);

private:
  using Clock = std::chrono::steady_clock;

  static constexpr std::size_t word_bits = 64U;

  // Chains split the search the same way whatever the thread count, so the
  // plan only depends on the seed and the evaluation limit.
  static constexpr std::size_t chain_count = 4U;

  // Who takes each card, and what the plan's cost counts besides its finish.
  struct Plan
  {
    explicit Plan(const ForecastModel& model);

    std::vector<std::uint32_t> people;
    // Per person: the cards of the scope they may take.
    std::vector<std::uint32_t> held;
    std::uint32_t changes = 0U;
    std::uint32_t unskilled = 0U;
  };

  // The buffers of one thread, allocated once.
  struct Runner
  {
    Runner(const ForecastModel& model, std::size_t pool_words);

    Plan plan;
    std::vector<std::uint64_t> pool;
    std::vector<std::size_t> driving;
    Scheduler scheduler;
    SplitMix random;
  };

  static void set_bit(std::vector<std::uint64_t>& words, std::uint32_t person) noexcept;

  void index_cards();
  void read_work(const std::vector<WorkItem>& work_items, const std::vector<Assignee>& assignees);
  void link_skills(const std::vector<Assignee>& skilled);
  void draw_screen_runs();
  void pick_receivers(const std::vector<std::uint32_t>& receivers);
  void find_movable();
  std::size_t index_of(NodeId node) const;
  std::span<const std::uint32_t> people_of(const Plan& plan, std::size_t card) const noexcept;
  std::uint32_t normalized(std::size_t card, std::uint32_t person) const noexcept;
  bool is_skilled(std::size_t card, std::uint32_t person) const noexcept;
  bool can_move(std::size_t card) const noexcept;
  void give(Plan& plan, std::size_t card, std::uint32_t person) const noexcept;
  void fill_pool(const Plan& plan, std::vector<std::uint64_t>& pool) const noexcept;
  double cost(Runner& runner);
  ScopeSample measure(Runner& runner);
  void anneal(Runner& runner, std::size_t chain, Clock::duration budget);
  void start_from_everyone(Runner& runner);
  std::size_t draw_card(Runner& runner) const;
  std::uint32_t draw_person(Runner& runner, std::size_t card) const;
  bool timed_out() const;
  void prune(Runner& runner, double& best);
  std::vector<double> starts_of(Runner& runner, const Plan& plan);
  StaffingPlan finish(Runner& runner, const ScopeSample& before, const ScopeSample& after);

  const ForecastModel* model_;
  const StaffingOptions* options_;
  std::uint32_t nobody_;
  std::size_t pool_words_;
  ScopeSimulator simulator_;
  std::unordered_map<NodeId, std::size_t, NodeHash> index_;
  std::vector<double> likely_;
  // One row of durations per screening run.
  std::vector<double> screen_hours_;
  std::vector<std::uint8_t> active_;
  std::vector<std::uint8_t> in_progress_;
  std::vector<std::uint32_t> skilled_offsets_;
  std::vector<std::uint32_t> skilled_;
  std::vector<std::uint32_t> receivers_;
  std::vector<std::uint8_t> is_receiver_;
  std::vector<std::uint64_t> elsewhere_;
  std::vector<std::uint8_t> movable_flags_;
  std::vector<std::size_t> movable_;
  Plan keep_;
  double temperature_ = 1.0;
  std::vector<Runner> runners_;
  std::vector<Plan> bests_;
  std::vector<double> best_costs_;
  Clock::time_point started_;
  // When the search has to stop to leave time for measuring the plan found.
  Clock::time_point deadline_;
};
}

#endif
