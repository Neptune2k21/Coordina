#ifndef COORDINA_GRAPH_ENGINE_SCOPE_SEARCH_H
#define COORDINA_GRAPH_ENGINE_SCOPE_SEARCH_H

#include "planning/duration_sampler.h"
#include "planning/plan_simulator.h"
#include "planning/planned_board.h"
#include "planning/scope_cut.h"
#include "planning/scope_graph.h"

#include <chrono>
#include <cstddef>
#include <cstdint>
#include <unordered_map>
#include <utility>
#include <vector>

namespace coordina::graph
{
// Choosing what to take out of a milestone is a knapsack with precedence
// constraints, which is NP-hard. The search is a heuristic: a greedy pass by
// value given up per hour saved, then a local search that gives back cuts the
// goal no longer needs and trades each cut for a cheaper one, every candidate
// measured by simulation on the same draws.
class ScopeSearch
{
public:
  // The milestone is an open one of the board.
  ScopeSearch(const PlannedBoard& planned, std::size_t milestone, const ScopeOptions& options);

  ScopeCut propose(const std::vector<NodeId>& keep);
  ScopeCut evaluate(const std::vector<NodeId>& cuts);

private:
  using Clock = std::chrono::steady_clock;
  using Ranking = std::vector<std::pair<double, std::size_t>>;

  struct Margin
  {
    double value = 0.0;
    double gain = 0.0;
  };

  std::size_t index_of(NodeId node) const;
  bool timed_out(Clock::duration reserve) const;
  bool exhausted() const;
  void find_cuttable();
  void rebuild();
  PlanSample measure(std::size_t runs);
  Margin margin_of(std::size_t card);
  double value_of_cut() const;
  bool leaves(std::size_t card) const noexcept;
  Margin margin_alone(std::size_t card);
  void grow(PlanSample& screen);
  bool grow_batch(const PlanSample& sample);
  void shrink(double floor);
  void swap(double floor);
  std::vector<std::size_t> by_value();
  std::vector<std::size_t> by_value_per_hour();
  std::vector<std::size_t> cheapest_cuts(double budget);
  void apply_trial();
  std::vector<std::size_t> roots();
  void repair();
  ScopeCut finish(const MilestoneForecast& before);

  const PlannedBoard* planned_;
  const PlanModel* model_;
  const ScopeOptions* options_;
  std::size_t milestone_;
  DurationSampler sampler_;
  ScopeGraph graph_;
  PlanSimulator simulator_;
  std::unordered_map<NodeId, std::size_t, NodeHash> index_;
  std::vector<double> values_;
  CardMask blocked_;
  std::vector<std::size_t> cuttable_;
  std::vector<std::size_t> chosen_;
  CardMask cut_;
  CardMask dropped_;
  CardMask active_;
  CardMask trial_cut_;
  CardMask trial_dropped_;
  CardMask trial_active_;
  std::vector<double> contribution_;
  std::uint32_t evaluations_ = 0U;
  Clock::time_point started_;
  Clock::duration full_run_cost_{};
};
}

#endif
