#ifndef COORDINA_GRAPH_ENGINE_TESTS_BENCHMARK_PLANNING_H
#define COORDINA_GRAPH_ENGINE_TESTS_BENCHMARK_PLANNING_H

#include "benchmark_board.h"
#include "plan_board.h"

#include <algorithm>
#include <cstddef>
#include <cstdint>
#include <limits>
#include <numeric>
#include <vector>

// The planning calls the benchmark times on a board.
namespace coordina::graph::benchmarks
{
constexpr std::uint32_t forecast_runs = 2000U;
constexpr std::uint32_t forecast_team = 5U;

// What the API allows each call; every one of them fits on a 1,000-card board.
constexpr std::uint32_t scope_time_limit_ms = 450U;
constexpr double scope_budget_ms = 700.0;
constexpr std::uint32_t staffing_team = 8U;
constexpr std::uint32_t staffing_time_limit_ms = 800U;
constexpr double staffing_budget_ms = 1100.0;
constexpr double advice_budget_ms = 2000.0;

// A release that depends on every card of the board, so its scope is the whole
// board. Assigned cards are spread over a team of five, and the completed ones
// have a history.
inline fixtures::PlanBoard release_of(const BoardGraph& board)
{
  fixtures::PlanBoard release;
  release.nodes = board.nodes;
  release.edges = board.edges;
  release.items = board.items;
  release.people = staffing_team;
  release.seed = 42U;
  const auto milestone = node(board.nodes.size());

  for (std::size_t index = 0U; index < board.items.size(); ++index)
  {
    const auto& item = board.items[index];
    release.edges.push_back(coordina_graph_edge{milestone, item.node});
    release.labels.push_back(
      coordina_graph_card_label{item.node, static_cast<std::uint32_t>(index % 6U), 0U});

    if (item.assignment != COORDINA_GRAPH_UNASSIGNED)
    {
      release.assignees.push_back(
        coordina_graph_assignee{item.node, static_cast<std::uint32_t>(index % forecast_team), 0U});
    }

    if (item.is_completed != 0U)
    {
      release.history.push_back(
        coordina_graph_finished_card{
          item.node,
          2.0 + static_cast<double>(index % 11U),
          static_cast<double>(index % 90U)
        });
    }
  }

  coordina_graph_work_item item{};
  item.node = milestone;
  item.due_days = std::numeric_limits<std::int32_t>::max();
  item.position = static_cast<std::uint32_t>(board.nodes.size());
  item.size = COORDINA_GRAPH_SIZE_XS;
  release.nodes.push_back(milestone);
  release.items.push_back(item);
  release.milestones.push_back(coordina_graph_milestone{milestone, 0.0, 1.0});
  release.receivers.resize(staffing_team);
  std::iota(release.receivers.begin(), release.receivers.end(), 0U);
  return release;
}

// Asks for the release to ship in 70% of its likely time.
inline void set_target(fixtures::PlanBoard& release)
{
  release.milestones.front().target_hours =
    release.forecast(forecast_runs, 0U).first().p50_days * 6.0 * 0.7;
}

inline fixtures::ScopeResult run_scope(const fixtures::PlanBoard& release)
{
  auto options = release.scope_options();
  options.milestone = release.nodes.back();
  options.simulation.thread_count = 0U;
  options.evaluation_limit = 120U;
  options.time_limit_ms = scope_time_limit_ms;
  return release.propose_scope(options);
}

inline fixtures::StaffingResult run_staffing(const fixtures::PlanBoard& release)
{
  auto options = release.staffing_options(true);
  options.milestone = release.nodes.back();
  options.simulation.thread_count = 0U;
  options.evaluation_limit = 20000U;
  options.time_limit_ms = staffing_time_limit_ms;
  return release.propose_staffing(options);
}

inline std::size_t run_actions(const fixtures::PlanBoard& release)
{
  const auto view = release.view();
  const coordina_graph_action_options options{
    .milestone = release.nodes.back(),
    .simulation = {release.seed, forecast_runs, 0U},
    .screen_run_count = 128U,
    .limit = 6U
  };
  std::vector<coordina_graph_action> actions(options.limit);
  coordina_graph_action_output output{actions.data(), actions.size(), 0U};
  const auto status = coordina_graph_propose_actions(
    &view,
    release.receivers.data(),
    release.receivers.size(),
    &options,
    &output);
  return status == COORDINA_GRAPH_OK ? output.action_count : 0U;
}

// Six words a card, shared by one card in forty.
inline std::vector<coordina_graph_card_token> words_of(const fixtures::PlanBoard& release)
{
  std::vector<coordina_graph_card_token> tokens;

  for (std::uint32_t card = 0U; card < release.nodes.size(); ++card)
  {
    for (std::uint32_t word = 0U; word < 6U; ++word)
    {
      const auto token = ((card * 7U) + (word * word * 31U)) % 240U;
      tokens.push_back(coordina_graph_card_token{release.nodes[card], 1.0, token, 0U});
    }
  }

  return tokens;
}

inline std::size_t run_questions(const fixtures::PlanBoard& release)
{
  const auto view = release.view();
  const auto tokens = words_of(release);
  const coordina_graph_question_options options{
    .milestone = release.nodes.back(),
    .simulation = {release.seed, forecast_runs, 0U},
    .limit = 5U,
    .reserved = 0U
  };
  std::vector<coordina_graph_question> questions(options.limit);
  coordina_graph_question_output output{questions.data(), questions.size(), 0U, 0.0};
  const auto status = coordina_graph_rank_questions(
    &view,
    tokens.data(),
    tokens.size(),
    nullptr,
    0U,
    &options,
    &output);
  return status == COORDINA_GRAPH_OK ? output.question_count : 0U;
}
}

#endif
