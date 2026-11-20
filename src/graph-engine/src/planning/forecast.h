#ifndef COORDINA_GRAPH_ENGINE_FORECAST_H
#define COORDINA_GRAPH_ENGINE_FORECAST_H

#include "planning/forecast_model.h"

#include <cstdint>
#include <vector>

namespace coordina::graph
{
struct ForecastOptions
{
  std::uint32_t run_count = 2000U;
  std::uint64_t seed = 0U;
  // 0 uses every core; the result is the same for any count.
  std::uint32_t thread_count = 0U;
};

struct CardForecast
{
  NodeId node{};
  // The share of runs where the card is on the chain that sets the date.
  double criticality = 0.0;
  // How closely the card's duration moves the date across runs (Pearson
  // correlation): the cards whose uncertainty costs most, worth splitting or
  // sizing again.
  double sensitivity = 0.0;
};

struct Forecast
{
  double p50_days = 0.0;
  double p85_days = 0.0;
  std::uint32_t unsized_count = 0U;
  // Most critical first; ties keep the scheduling order.
  std::vector<CardForecast> cards;
};

Forecast forecast(const ForecastModel& model, const ForecastOptions& options);
}

#endif
