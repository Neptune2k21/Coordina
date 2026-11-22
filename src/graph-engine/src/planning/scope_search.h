#ifndef COORDINA_GRAPH_ENGINE_SCOPE_SEARCH_H
#define COORDINA_GRAPH_ENGINE_SCOPE_SEARCH_H

#include "planning/forecast_model.h"
#include "planning/scope_cut.h"
#include "planning/scope_graph.h"
#include "planning/scope_simulator.h"

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
  ScopeSearch(
    const ForecastModel& model,
    const std::vector<WorkItem>& work_items,
    NodeId milestone,
    const ScopeOptions& options);

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
  ScopeSample measure(std::size_t runs);
  Margin margin_of(std::size_t card);
  double value_of_cut() const;
  double value_of(std::size_t card);
  void grow(ScopeSample& screen);
  bool grow_batch(const ScopeSample& sample);
  void shrink(double floor);
  void swap(double floor);
  std::vector<std::size_t> by_value();
  std::vector<std::size_t> cheapest_cuts(double budget);
  std::vector<std::size_t> roots();
  ScopeCut finish(const ScopeSample& before, const ScopeSample* after);

  const ForecastModel* model_;
  const ScopeOptions* options_;
  ScopeGraph graph_;
  ScopeSimulator simulator_;
  std::unordered_map<NodeId, std::size_t, NodeHash> index_;
  std::vector<double> values_;
  CardMask blocked_;
  std::vector<std::size_t> cuttable_;
  std::vector<std::size_t> chosen_;
  CardMask cut_;
  CardMask active_;
  CardMask trial_cut_;
  CardMask trial_active_;
  std::vector<double> contribution_;
  std::uint32_t evaluations_ = 0U;
  Clock::time_point started_;
  Clock::duration full_run_cost_{};
};
}

#endif
