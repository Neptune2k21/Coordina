#ifndef COORDINA_GRAPH_ENGINE_PLAN_SIMULATOR_H
#define COORDINA_GRAPH_ENGINE_PLAN_SIMULATOR_H

#include "planning/duration_sampler.h"
#include "planning/forecast.h"
#include "planning/plan_model.h"
#include "planning/schedule.h"

#include <cstddef>
#include <cstdint>
#include <span>
#include <vector>

namespace coordina::graph
{
// How a version of a plan does, for one milestone.
struct PlanSample
{
  // The share of runs done by the target.
  double on_time = 0.0;
  // The finish at the goal's rank: the goal's share of the runs end by then.
  double goal_hours = 0.0;
  double mean_hours = 0.0;
  double p85_hours = 0.0;
};

// Simulates versions of a plan, which cards stay and who takes them, on the
// forecast's own draws, so every version is measured on the same luck and
// their differences come from the change alone. Every buffer is allocated
// once, for the largest run count.
class PlanSimulator
{
public:
  PlanSimulator(
    const PlanModel& model,
    const DurationSampler& sampler,
    std::size_t milestone,
    double goal,
    std::uint32_t largest_run_count,
    std::uint32_t thread_count);

  // contribution, when not null, receives the hours each card adds to the
  // mean finish: its duration in the runs where it is on the chain that sets
  // the date. That chain spans the whole run, so the contributions add up to
  // the mean finish, and taking a card out saves about its contribution.
  PlanSample run(
    std::size_t runs,
    const Cut& cut,
    const Staffing& staffing,
    std::vector<double>* contribution);

private:
  struct Runner
  {
    Runner(const PlanModel& model, const DurationSampler& sampler);

    SharedDraw shared;
    std::vector<double> hours;
    std::vector<std::size_t> chain;
    Scheduler scheduler;
  };

  void simulate(
    Runner& runner,
    const Cut& cut,
    const Staffing& staffing,
    std::size_t first,
    std::size_t end,
    std::vector<double>* contribution);

  const PlanModel* model_;
  const DurationSampler* sampler_;
  std::size_t milestone_;
  double goal_;
  std::uint32_t thread_count_;
  std::vector<Runner> runners_;
  // Per chunk, what each card contributes.
  std::vector<std::vector<double>> contributions_;
  std::vector<double> finishes_;
};
}

#endif
