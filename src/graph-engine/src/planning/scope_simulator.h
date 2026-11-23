#ifndef COORDINA_GRAPH_ENGINE_SCOPE_SIMULATOR_H
#define COORDINA_GRAPH_ENGINE_SCOPE_SIMULATOR_H

#include "planning/forecast_model.h"
#include "planning/schedule.h"
#include "planning/scope_cut.h"

#include <cstddef>
#include <cstdint>
#include <span>
#include <vector>

namespace coordina::graph
{
struct ScopeSample
{
  // The share of runs done by the target.
  double on_time = 0.0;
  // The finish at the goal's rank: the goal's share of the runs end by then.
  double goal_hours = 0.0;
  double p50_hours = 0.0;
  double p85_hours = 0.0;
};

// Simulates versions of a scope, which cards stay and who takes them, on the
// cards' own random streams, so every version is measured on the same draws
// and their differences come from the change alone. Every buffer is allocated
// once, for the largest run count.
class ScopeSimulator
{
public:
  ScopeSimulator(const ForecastModel& model, const ScopeOptions& options);

  // contribution, when not null, receives the hours each card adds to the
  // mean finish: its duration in the runs where it is on the chain that sets
  // the date. That chain spans the whole run, so the contributions add up to
  // the mean finish, and taking a card out saves about its contribution.
  ScopeSample run(
    std::span<const std::uint8_t> active,
    std::size_t runs,
    std::vector<double>* contribution,
    const Staffing& staffing = {});

private:
  struct Runner
  {
    explicit Runner(const ForecastModel& model);

    std::vector<double> hours;
    std::vector<std::size_t> chain;
    Scheduler scheduler;
  };

  struct Chunk
  {
    explicit Chunk(std::size_t card_count);

    std::size_t on_time = 0U;
    std::vector<double> contribution;
  };

  void simulate(
    Runner& runner,
    std::span<const std::uint8_t> active,
    const Staffing& staffing,
    std::size_t first,
    std::size_t end,
    Chunk& chunk,
    bool with_contribution);
  ScopeSample summarize(std::size_t runs, std::size_t chunks, std::vector<double>* contribution);

  const ForecastModel* model_;
  CardStreams streams_;
  double target_hours_;
  double goal_;
  std::uint32_t thread_count_;
  std::vector<double> finishes_;
  std::vector<Runner> runners_;
  std::vector<Chunk> chunks_;
};
}

#endif
