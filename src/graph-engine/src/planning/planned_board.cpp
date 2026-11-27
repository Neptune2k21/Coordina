#include "planning/planned_board.h"

#include <algorithm>
#include <unordered_map>
#include <unordered_set>

namespace coordina::graph
{
PlannedBoard::PlannedBoard(const PlanningBoard& board, const SimulationOptions& options)
    : board_(&board), options_(options), index_(board), error_(index_.error())
{
  if (error_ != PlanError::none)
  {
    return;
  }

  durations_ = std::make_unique<const DurationModel>(board, index_, options.seed);
  model_ = std::make_unique<const PlanModel>(board, index_, *durations_);
  error_ = model_->error();
}

PlanError PlannedBoard::error() const noexcept
{
  return error_;
}

const PlanningBoard& PlannedBoard::board() const noexcept
{
  return *board_;
}

const PlanModel& PlannedBoard::model() const noexcept
{
  return *model_;
}

const DurationModel& PlannedBoard::durations() const noexcept
{
  return *durations_;
}

const SimulationOptions& PlannedBoard::options() const noexcept
{
  return options_;
}

std::size_t PlannedBoard::find_milestone(NodeId node) const noexcept
{
  for (std::size_t milestone = 0U; milestone < board_->milestones.size(); ++milestone)
  {
    if (board_->milestones[milestone].node == node)
    {
      return milestone;
    }
  }

  return no_milestone;
}

Forecast PlannedBoard::forecast_now() const
{
  return forecast(*model_, *durations_, options_);
}

Forecast PlannedBoard::forecast_without(NodeId milestone, const std::vector<NodeId>& dropped) const
{
  const std::unordered_set<NodeId, NodeHash> leaving(dropped.begin(), dropped.end());
  auto changed = *board_;
  std::erase_if(
    changed.edges,
    [&](const Edge& edge)
    {
      return edge.source == milestone && leaving.contains(edge.target);
    });
  return forecast_changed(changed);
}

Forecast PlannedBoard::forecast_with(const std::vector<Assignee>& changes) const
{
  std::unordered_set<NodeId, NodeHash> given;

  for (const auto& change : changes)
  {
    given.insert(change.node);
  }

  auto changed = *board_;
  std::erase_if(
    changed.assignees,
    [&](const Assignee& assignee)
    {
      return given.contains(assignee.node);
    });
  changed.assignees.insert(changed.assignees.end(), changes.begin(), changes.end());
  return forecast_changed(changed);
}

Forecast PlannedBoard::forecast_changed(const PlanningBoard& changed) const
{
  return forecast(changed, options_);
}
}
