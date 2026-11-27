#ifndef COORDINA_GRAPH_ENGINE_CALIBRATION_H
#define COORDINA_GRAPH_ENGINE_CALIBRATION_H

#include "planning/card_size.h"
#include "planning/planning_board.h"

#include <array>
#include <cstdint>
#include <vector>

namespace coordina::graph
{
// How long cards take against what their size says: 1.6 means 60% longer. Half
// the chance is on each side of the median, 10% below low and 10% above high.
struct DurationRatio
{
  double low = 1.0;
  double median = 1.0;
  double high = 1.0;
  std::uint32_t card_count = 0U;
};

struct LabelRatio
{
  std::uint32_t label = 0U;
  DurationRatio ratio;
};

// What the board has taught the forecasts, and how right they would have been.
struct Calibration
{
  PlanError error = PlanError::none;
  // Completed, sized cards learned from.
  std::uint32_t card_count = 0U;
  DurationRatio team;
  std::array<DurationRatio, card_size_count> sizes;
  std::vector<LabelRatio> labels;
  // Each card predicted from the ones completed before it: the share that took
  // no longer than their 50% and 85% durations. Forecasts are well calibrated
  // when these are close to 0.50 and 0.85.
  std::uint32_t replayed_count = 0U;
  double within_p50 = 0.0;
  double within_p85 = 0.0;
  // New work found per completed card.
  double growth = 0.0;
};

Calibration calibrate(const PlanningBoard& board);
}

#endif
