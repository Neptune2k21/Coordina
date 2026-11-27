#ifndef COORDINA_GRAPH_ENGINE_FORECAST_H
#define COORDINA_GRAPH_ENGINE_FORECAST_H

#include "planning/duration_model.h"
#include "planning/plan_model.h"

#include <cstdint>
#include <vector>

namespace coordina::graph
{
struct SimulationOptions
{
  std::uint64_t seed = 0U;
  std::uint32_t run_count = 2000U;
  // 0 uses every core; the result is the same for any count.
  std::uint32_t thread_count = 0U;
};

struct CardForecast
{
  NodeId node{};
  // The share of runs where the card is on the chain that sets the date.
  double criticality = 0.0;
  // How closely the card's own uncertainty moves the date across runs (Pearson
  // correlation), apart from what all cards share. Squared, it is the share of
  // the date's spread the card explains: what sizing or splitting it can remove.
  double sensitivity = 0.0;
};

// When a milestone ships. Dates count working days from now.
struct MilestoneForecast
{
  NodeId node{};
  double p10_days = 0.0;
  double p50_days = 0.0;
  double p85_days = 0.0;
  double p95_days = 0.0;
  // The share of runs done by the target.
  double on_time = 0.0;
  // How late the milestone is on average, counting 0 when it is on time.
  double late_days = 0.0;
  // The 50% date with as many people as it takes: no staffing beats it.
  double chain_days = 0.0;
  std::uint32_t card_count = 0U;
  std::uint32_t unsized_count = 0U;
  // The cards of the scope, most critical first; ties keep the pick-up order.
  std::vector<CardForecast> cards;
};

// What the plan expects of a card when everything takes its median time.
struct PlannedCard
{
  NodeId node{};
  // board.person_count when anyone may take it.
  std::uint32_t person = 0U;
  double start_days = 0.0;
  double finish_days = 0.0;
};

struct Forecast
{
  PlanError error = PlanError::none;
  std::vector<MilestoneForecast> milestones;
  // The open cards of every scope, in the order to pick them up.
  std::vector<PlannedCard> plan;
};

Forecast forecast(
  const PlanModel& model,
  const DurationModel& durations,
  const SimulationOptions& options);

// Builds the model and forecasts it.
Forecast forecast(const PlanningBoard& board, const SimulationOptions& options);

// The run below which that share of the runs fall (nearest rank).
double quantile_of(std::vector<double>& values, double share);
}

#endif
