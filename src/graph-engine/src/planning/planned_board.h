#ifndef COORDINA_GRAPH_ENGINE_PLANNED_BOARD_H
#define COORDINA_GRAPH_ENGINE_PLANNED_BOARD_H

#include "planning/board_index.h"
#include "planning/duration_model.h"
#include "planning/forecast.h"
#include "planning/plan_model.h"

#include <cstddef>
#include <limits>
#include <memory>
#include <vector>

namespace coordina::graph
{
// A board with its model, learned durations and forecast, and the changes one
// can measure on it. A change is measured by forecasting the changed board, so
// what it announces is what the board shows once it is applied.
class PlannedBoard
{
public:
  static constexpr auto no_milestone = std::numeric_limits<std::size_t>::max();

  PlannedBoard(const PlanningBoard& board, const SimulationOptions& options);

  PlanError error() const noexcept;
  const PlanningBoard& board() const noexcept;
  // Only once there is no error.
  const PlanModel& model() const noexcept;
  const DurationModel& durations() const noexcept;
  const SimulationOptions& options() const noexcept;
  // The milestone's place in the board's, or no_milestone.
  std::size_t find_milestone(NodeId node) const noexcept;

  Forecast forecast_now() const;
  // The forecast without the milestone's dependencies on those cards.
  Forecast forecast_without(NodeId milestone, const std::vector<NodeId>& dropped) const;
  // The forecast once each card has one person as its only assignee.
  Forecast forecast_with(const std::vector<Assignee>& changes) const;

private:
  Forecast forecast_changed(const PlanningBoard& changed) const;

  const PlanningBoard* board_;
  SimulationOptions options_;
  BoardIndex index_;
  // Neither is built when the board has an error.
  std::unique_ptr<const DurationModel> durations_;
  std::unique_ptr<const PlanModel> model_;
  PlanError error_ = PlanError::none;
};
}

#endif
