#include "plan_board.h"

#include <algorithm>
#include <cassert>
#include <cstddef>
#include <cstdint>
#include <vector>

namespace
{
using namespace coordina::graph::fixtures;

constexpr std::uint32_t ada = 0U;
constexpr std::uint32_t grace = 1U;

struct Actions
{
  coordina_graph_status status = COORDINA_GRAPH_OK;
  std::vector<coordina_graph_action> actions;
};

Actions propose_actions(
  const PlanBoard& board,
  std::uint32_t limit = 6U,
  std::uint32_t threads = 1U)
{
  const auto view = board.view();
  const coordina_graph_action_options options{
    .milestone = node(milestone_id),
    .simulation = {board.seed, 2000U, threads},
    .screen_run_count = 128U,
    .limit = limit
  };
  Actions result;
  result.actions.resize(limit);
  coordina_graph_action_output output{result.actions.data(), result.actions.size(), 0U};
  result.status = coordina_graph_propose_actions(
    &view,
    board.receivers.data(),
    board.receivers.size(),
    &options,
    &output);
  result.actions.resize(output.action_count);
  return result;
}

// Ada has a chain of three large cards; Grace a small one and time to spare.
PlanBoard late_board()
{
  PlanBoard board;
  board.people = 2U;
  board.receivers = {ada, grace};
  board.card(milestone_id, COORDINA_GRAPH_SIZE_XS);
  board.card(1U, COORDINA_GRAPH_SIZE_L, 1U);
  board.card(2U, COORDINA_GRAPH_SIZE_L, 2U);
  board.card(3U, COORDINA_GRAPH_SIZE_L, 3U);
  board.card(4U, COORDINA_GRAPH_SIZE_S, 2U);

  for (unsigned long long id = 1U; id <= 4U; ++id)
  {
    board.depends(milestone_id, id);
    board.assign(id, id <= 3U ? ada : grace);
  }

  board.milestone(milestone_id, 50.0);
  return board;
}

void ranks_what_helps_most_in_one_currency()
{
  const auto board = late_board();
  const auto result = propose_actions(board);

  assert(result.status == COORDINA_GRAPH_OK && !result.actions.empty());
  auto previous = 1.0;

  for (const auto& action : result.actions)
  {
    const auto net = action.on_time_after - (0.02 * action.value_lost);
    assert(net <= previous);
    previous = net;
    assert(action.on_time_after >= action.on_time_before);
    assert(action.kind != COORDINA_GRAPH_ACTION_ADD_PERSON);
  }

  // Both kinds answer: take a large card out, or give one to Grace.
  const auto has = [&](std::uint32_t kind)
  {
    return std::ranges::any_of(
      result.actions,
      [kind](const auto& action)
      {
        return action.kind == kind;
      });
  };
  assert(has(COORDINA_GRAPH_ACTION_CUT_CARD) && has(COORDINA_GRAPH_ACTION_ASSIGN_CARD));
}

void gives_up_the_least_for_the_same_gain()
{
  // Three cards that bring the date in about as much: giving one to Grace
  // costs nothing, and of the cuts the low-priority card goes first.
  const auto board = late_board();
  const auto result = propose_actions(board, 12U);
  auto first_cut = result.actions.end();

  for (auto action = result.actions.begin(); action != result.actions.end(); ++action)
  {
    if (action->kind == COORDINA_GRAPH_ACTION_ASSIGN_CARD)
    {
      assert(action->value_lost == 0.0);
    }
    else if (first_cut == result.actions.end())
    {
      first_cut = action;
    }
  }

  assert(result.actions.front().kind == COORDINA_GRAPH_ACTION_ASSIGN_CARD);
  assert(first_cut != result.actions.end());
  assert(first_cut->node.low == 1U && first_cut->value_lost == 1.0);
}

void announces_what_the_board_shows_once_done()
{
  const auto board = late_board();

  for (const auto& action : propose_actions(board).actions)
  {
    const auto applied = action.kind == COORDINA_GRAPH_ACTION_CUT_CARD
      ? board.without({action.node.low}).forecast().first()
      : board.with({{action.node.low, action.person}}).forecast().first();
    assert(action.on_time_after == applied.on_time);
    assert(action.p85_days_after == applied.p85_days);
  }
}

void proposes_more_people_only_for_unassigned_work()
{
  auto board = late_board();

  for (unsigned long long id = 10U; id < 16U; ++id)
  {
    board.card(id, COORDINA_GRAPH_SIZE_M);
    board.depends(milestone_id, id);
  }

  const auto result = propose_actions(board, 30U);
  assert(
    std::ranges::any_of(
      result.actions,
      [](const auto& action)
      {
        return action.kind == COORDINA_GRAPH_ACTION_ADD_PERSON;
      }));
}

void never_touches_work_in_progress()
{
  auto board = late_board();

  for (unsigned long long id = 1U; id <= 3U; ++id)
  {
    board.item(id).is_in_progress = 1U;
  }

  for (const auto& action : propose_actions(board).actions)
  {
    assert(action.node.low == 4U);
  }
}

void gives_the_same_actions_with_any_thread_count()
{
  auto board = late_board();

  for (unsigned long long id = 100U; id < 180U; ++id)
  {
    board.card(id, 1U + static_cast<std::uint32_t>(id % 5U), static_cast<std::uint32_t>(id % 4U));
    board.depends(milestone_id, id);
    board.assign(id, static_cast<std::uint32_t>(id % 2U));
  }

  board.milestones.front().target_hours = 500.0;
  const auto one = propose_actions(board, 6U, 1U);
  const auto four = propose_actions(board, 6U, 4U);
  assert(one.actions.size() == four.actions.size() && !one.actions.empty());

  for (std::size_t index = 0U; index < one.actions.size(); ++index)
  {
    assert(same_node(one.actions[index].node, four.actions[index].node));
    assert(one.actions[index].on_time_after == four.actions[index].on_time_after);
  }
}

void rejects_bad_arguments()
{
  const auto board = late_board();
  const auto view = board.view();
  coordina_graph_action_options options{
    .milestone = node(milestone_id),
    .simulation = {1U, 100U, 1U},
    .screen_run_count = 16U,
    .limit = 3U
  };
  coordina_graph_action_output output{};
  const auto propose = [&]
  {
    return coordina_graph_propose_actions(
      &view,
      board.receivers.data(),
      board.receivers.size(),
      &options,
      &output);
  };
  assert(propose() == COORDINA_GRAPH_INSUFFICIENT_CAPACITY && output.action_count > 0U);
  options.limit = 0U;
  assert(propose() == COORDINA_GRAPH_INVALID_ARGUMENT);
  options.limit = 3U;
  options.screen_run_count = 0U;
  assert(propose() == COORDINA_GRAPH_INVALID_ARGUMENT);
  options.screen_run_count = 16U;
  options.milestone = node(1U);
  assert(propose() == COORDINA_GRAPH_INVALID_ARGUMENT && last_error_says("milestone"));

  auto outsider = board;
  outsider.receivers = {9U};
  assert(propose_actions(outsider).status == COORDINA_GRAPH_INVALID_ARGUMENT);

  // A done milestone needs nothing.
  auto done = board;
  done.item(milestone_id).is_completed = 1U;
  assert(propose_actions(done).actions.empty());
}
}

int main()
{
  ranks_what_helps_most_in_one_currency();
  gives_up_the_least_for_the_same_gain();
  announces_what_the_board_shows_once_done();
  proposes_more_people_only_for_unassigned_work();
  never_touches_work_in_progress();
  gives_the_same_actions_with_any_thread_count();
  rejects_bad_arguments();

  return 0;
}
