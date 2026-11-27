#include "coordina_graph_engine.h"

#include "interop/abi_errors.h"
#include "interop/planning_bridge.h"
#include "planning/forecast.h"
#include "planning/scope_cut.h"
#include "planning/staffing.h"

#include <algorithm>
#include <chrono>
#include <cstddef>
#include <vector>

// The exports that plan a board's milestones: their forecast, what to cut so
// one ships on time, and who should take which card.
namespace
{
namespace abi = coordina::graph::abi;

using coordina::graph::Forecast;
using coordina::graph::MilestoneForecast;
using coordina::graph::ScopeCut;
using coordina::graph::ScopeOptions;
using coordina::graph::StaffingOptions;
using coordina::graph::StaffingPlan;
using coordina::graph::to_abi_node_id;
using coordina::graph::to_node_id;

coordina_graph_status check_screening(std::uint32_t screen_run_count) noexcept
{
  return screen_run_count == 0U || screen_run_count > abi::run_count_limit
    ? abi::invalid("screen_run_count is outside 1 to 100000.")
    : COORDINA_GRAPH_OK;
}

// The first check that fails.
template <typename... Checks>
coordina_graph_status first_failure(Checks... checks)
{
  auto status = COORDINA_GRAPH_OK;
  ((status = status == COORDINA_GRAPH_OK ? checks() : status), ...);
  return status;
}

coordina_graph_milestone_forecast to_abi(const MilestoneForecast& milestone) noexcept
{
  return coordina_graph_milestone_forecast{
    .node = to_abi_node_id(milestone.node),
    .p10_days = milestone.p10_days,
    .p50_days = milestone.p50_days,
    .p85_days = milestone.p85_days,
    .p95_days = milestone.p95_days,
    .on_time = milestone.on_time,
    .late_days = milestone.late_days,
    .chain_days = milestone.chain_days,
    .card_count = milestone.card_count,
    .unsized_count = milestone.unsized_count
  };
}

std::vector<coordina_graph_card_forecast> cards_of(const Forecast& forecast)
{
  std::vector<coordina_graph_card_forecast> cards;

  for (std::uint32_t milestone = 0U; milestone < forecast.milestones.size(); ++milestone)
  {
    for (const auto& card : forecast.milestones[milestone].cards)
    {
      cards.push_back(
        coordina_graph_card_forecast{
          .node = to_abi_node_id(card.node),
          .milestone = milestone,
          .reserved = 0U,
          .criticality = card.criticality,
          .sensitivity = card.sensitivity
        });
    }
  }

  return cards;
}

coordina_graph_status write_forecast(
  const Forecast& forecast,
  coordina_graph_forecast_output& output)
{
  const auto cards = cards_of(forecast);
  output.milestone_count = forecast.milestones.size();
  output.card_count = cards.size();
  output.plan_count = forecast.plan.size();

  if (!abi::fits(output.milestone_count, output.milestones, output.milestone_capacity)
    || !abi::fits(output.card_count, output.cards, output.card_capacity)
    || !abi::fits(output.plan_count, output.plan, output.plan_capacity))
  {
    return COORDINA_GRAPH_INSUFFICIENT_CAPACITY;
  }

  for (std::size_t index = 0U; index < forecast.milestones.size(); ++index)
  {
    output.milestones[index] = to_abi(forecast.milestones[index]);
  }

  std::ranges::copy(cards, output.cards);

  for (std::size_t index = 0U; index < forecast.plan.size(); ++index)
  {
    const auto& card = forecast.plan[index];
    output.plan[index] = coordina_graph_planned_card{
      .node = to_abi_node_id(card.node),
      .person = card.person,
      .reserved = 0U,
      .start_days = card.start_days,
      .finish_days = card.finish_days
    };
  }

  return COORDINA_GRAPH_OK;
}

ScopeOptions build_options(const coordina_graph_scope_options& options) noexcept
{
  return ScopeOptions{
    .milestone = to_node_id(options.milestone),
    .goal = options.goal,
    .simulation = abi::build_simulation(options.simulation),
    .screen_run_count = options.screen_run_count,
    .evaluation_limit = options.evaluation_limit,
    .time_limit = std::chrono::milliseconds(options.time_limit_ms)
  };
}

coordina_graph_status check_scope(
  const coordina_graph_board* board,
  const coordina_graph_node_id* cards,
  std::size_t card_count,
  const coordina_graph_scope_options* options,
  const coordina_graph_scope_cut* output)
{
  return first_failure(
    [&]
    {
      return abi::check_board(board);
    },
    [&]
    {
      return options == nullptr || output == nullptr ? abi::invalid("An argument is null.")
                                                     : COORDINA_GRAPH_OK;
    },
    [&]
    {
      return abi::check_array(cards, card_count);
    },
    [&]
    {
      return abi::check_simulation(options->simulation);
    },
    [&]
    {
      return check_screening(options->screen_run_count);
    },
    [&]
    {
      return options->goal > 0.0 && options->goal <= 1.0 ? COORDINA_GRAPH_OK
                                                         : abi::invalid("goal is outside 0 to 1.");
    });
}

coordina_graph_status write_scope(const ScopeCut& cut, coordina_graph_scope_cut& output)
{
  if (cut.error != coordina::graph::PlanError::none)
  {
    return abi::to_status(cut.error);
  }

  output.cut_count = cut.cuts.size();
  output.dropped_count = cut.dropped.size();

  if (!abi::fits(output.cut_count, output.cuts, output.cut_capacity)
    || !abi::fits(output.dropped_count, output.dropped, output.dropped_capacity))
  {
    return COORDINA_GRAPH_INSUFFICIENT_CAPACITY;
  }

  for (std::size_t index = 0U; index < cut.cuts.size(); ++index)
  {
    output.cuts[index] = to_abi_node_id(cut.cuts[index]);
  }

  for (std::size_t index = 0U; index < cut.dropped.size(); ++index)
  {
    output.dropped[index] = coordina_graph_dropped_card{
      .node = to_abi_node_id(cut.dropped[index].node),
      .cut = cut.dropped[index].cut,
      .reserved = 0U
    };
  }

  output.on_time_before = cut.on_time_before;
  output.on_time_after = cut.on_time_after;
  output.p50_days = cut.p50_days;
  output.p85_days = cut.p85_days;
  return COORDINA_GRAPH_OK;
}

StaffingOptions build_options(const coordina_graph_staffing_options& options) noexcept
{
  return StaffingOptions{
    .milestone = to_node_id(options.milestone),
    .simulation = abi::build_simulation(options.simulation),
    .screen_run_count = options.screen_run_count,
    .allow_reassignment = options.allow_reassignment != 0U,
    .evaluation_limit = options.evaluation_limit,
    .time_limit = std::chrono::milliseconds(options.time_limit_ms)
  };
}

coordina_graph_status check_staffing(
  const coordina_graph_board* board,
  const coordina_graph_staffing_options* options,
  const coordina_graph_staffing* output)
{
  return first_failure(
    [&]
    {
      return abi::check_board(board);
    },
    [&]
    {
      return options == nullptr || output == nullptr ? abi::invalid("An argument is null.")
                                                     : COORDINA_GRAPH_OK;
    },
    [&]
    {
      return abi::check_simulation(options->simulation);
    },
    [&]
    {
      return check_screening(options->screen_run_count);
    });
}

coordina_graph_status write_staffing(const StaffingPlan& plan, coordina_graph_staffing& output)
{
  if (plan.error != coordina::graph::PlanError::none)
  {
    return abi::to_status(plan.error);
  }

  const auto fits = abi::write_items(
    plan.changes,
    output.changes,
    output.change_capacity,
    output.change_count,
    [](const coordina::graph::StaffChange& change)
    {
      return coordina_graph_staff_change{
        .node = to_abi_node_id(change.node),
        .person = change.person,
        .reserved = 0U,
        .start_days_before = change.start_days_before,
        .start_days_after = change.start_days_after
      };
    });

  if (!fits)
  {
    return COORDINA_GRAPH_INSUFFICIENT_CAPACITY;
  }

  output.p50_days_before = plan.p50_days_before;
  output.p85_days_before = plan.p85_days_before;
  output.p50_days_after = plan.p50_days_after;
  output.p85_days_after = plan.p85_days_after;
  output.others_late_days_before = plan.others_late_days_before;
  output.others_late_days_after = plan.others_late_days_after;
  return COORDINA_GRAPH_OK;
}
}

extern "C" coordina_graph_status coordina_graph_forecast(
  const coordina_graph_board* board,
  const coordina_graph_simulation* simulation,
  coordina_graph_forecast_output* output)
{
  return abi::guarded(
    [&]
    {
      if (const auto status = abi::check_board(board); status != COORDINA_GRAPH_OK)
      {
        return status;
      }

      if (simulation == nullptr || output == nullptr)
      {
        return abi::invalid("An argument is null.");
      }

      if (const auto status = abi::check_simulation(*simulation); status != COORDINA_GRAPH_OK)
      {
        return status;
      }

      const auto forecast =
        coordina::graph::forecast(abi::build_board(*board), abi::build_simulation(*simulation));
      return forecast.error == coordina::graph::PlanError::none ? write_forecast(forecast, *output)
                                                                : abi::to_status(forecast.error);
    });
}

extern "C" coordina_graph_status coordina_graph_propose_scope(
  const coordina_graph_board* board,
  const coordina_graph_node_id* keep,
  std::size_t keep_count,
  const coordina_graph_scope_options* options,
  coordina_graph_scope_cut* output)
{
  return abi::guarded(
    [&]
    {
      if (const auto status = check_scope(board, keep, keep_count, options, output);
        status != COORDINA_GRAPH_OK)
      {
        return status;
      }

      return write_scope(
        coordina::graph::propose_scope(
          abi::build_board(*board),
          abi::build_node_ids(keep, keep_count),
          build_options(*options)),
        *output);
    });
}

extern "C" coordina_graph_status coordina_graph_evaluate_scope(
  const coordina_graph_board* board,
  const coordina_graph_node_id* cuts,
  std::size_t cut_count,
  const coordina_graph_scope_options* options,
  coordina_graph_scope_cut* output)
{
  return abi::guarded(
    [&]
    {
      const auto status = check_scope(board, cuts, cut_count, options, output);
      return status == COORDINA_GRAPH_OK ? write_scope(
                                             coordina::graph::evaluate_scope(
                                               abi::build_board(*board),
                                               abi::build_node_ids(cuts, cut_count),
                                               build_options(*options)),
                                             *output)
                                         : status;
    });
}

extern "C" coordina_graph_status coordina_graph_propose_staffing(
  const coordina_graph_board* board,
  const std::uint32_t* receivers,
  std::size_t receiver_count,
  const coordina_graph_assignee* skilled,
  std::size_t skilled_count,
  const coordina_graph_staffing_options* options,
  coordina_graph_staffing* output)
{
  return abi::guarded(
    [&]
    {
      const auto status = first_failure(
        [&]
        {
          return check_staffing(board, options, output);
        },
        [&]
        {
          return abi::check_people(receivers, receiver_count, *board);
        },
        [&]
        {
          return abi::check_people(skilled, skilled_count, *board);
        });

      if (status != COORDINA_GRAPH_OK)
      {
        return status;
      }

      return write_staffing(
        coordina::graph::propose_staffing(
          abi::build_board(*board),
          coordina::graph::StaffingTeam{
            .receivers = std::vector<std::uint32_t>(receivers, receivers + receiver_count),
            .skilled = abi::build_assignees(skilled, skilled_count)
          },
          build_options(*options)),
        *output);
    });
}

extern "C" coordina_graph_status coordina_graph_evaluate_staffing(
  const coordina_graph_board* board,
  const coordina_graph_assignee* changes,
  std::size_t change_count,
  const coordina_graph_staffing_options* options,
  coordina_graph_staffing* output)
{
  return abi::guarded(
    [&]
    {
      const auto status = first_failure(
        [&]
        {
          return check_staffing(board, options, output);
        },
        [&]
        {
          return abi::check_people(changes, change_count, *board);
        });
      return status == COORDINA_GRAPH_OK ? write_staffing(
                                             coordina::graph::evaluate_staffing(
                                               abi::build_board(*board),
                                               abi::build_assignees(changes, change_count),
                                               build_options(*options)),
                                             *output)
                                         : status;
    });
}
