#include "interop/planning_bridge.h"

#include <algorithm>

namespace coordina::graph::abi
{
namespace
{
constexpr auto null_array = "An array is null but its count is not 0.";
constexpr auto outside_person = "A person is outside person_count.";

// Turns an array of the ABI into the engine's.
template <typename Output, typename Input, typename Convert>
std::vector<Output> build(const Input* items, std::size_t count, const Convert& convert)
{
  std::vector<Output> output;
  output.reserve(count);

  for (std::size_t index = 0U; index < count; ++index)
  {
    output.push_back(convert(items[index]));
  }

  return output;
}
}

coordina_graph_status check_array(const void* items, std::size_t count) noexcept
{
  return is_missing(items, count) ? invalid(null_array) : COORDINA_GRAPH_OK;
}

coordina_graph_status check_board(const coordina_graph_board* board) noexcept
{
  if (board == nullptr)
  {
    return invalid("The board is null.");
  }

  if (is_missing(board->nodes, board->node_count) || is_missing(board->edges, board->edge_count)
    || is_missing(board->work_items, board->work_item_count)
    || is_missing(board->assignees, board->assignee_count)
    || is_missing(board->labels, board->label_count)
    || is_missing(board->milestones, board->milestone_count)
    || is_missing(board->history, board->history_count))
  {
    return invalid(null_array);
  }

  return board->person_count > person_count_limit || board->extra_people > person_count_limit
    ? invalid("There are too many people.")
    : COORDINA_GRAPH_OK;
}

coordina_graph_status check_simulation(const coordina_graph_simulation& simulation) noexcept
{
  return simulation.run_count == 0U || simulation.run_count > run_count_limit
    ? invalid("run_count is outside 1 to 100000.")
    : COORDINA_GRAPH_OK;
}

coordina_graph_status check_people(
  const std::uint32_t* people,
  std::size_t count,
  const coordina_graph_board& board) noexcept
{
  if (is_missing(people, count))
  {
    return invalid(null_array);
  }

  return std::all_of(
           people,
           people + count,
           [&board](std::uint32_t person)
           {
             return person < board.person_count;
           })
    ? COORDINA_GRAPH_OK
    : invalid(outside_person);
}

coordina_graph_status check_people(
  const coordina_graph_assignee* pairs,
  std::size_t count,
  const coordina_graph_board& board) noexcept
{
  if (is_missing(pairs, count))
  {
    return invalid(null_array);
  }

  return std::all_of(
           pairs,
           pairs + count,
           [&board](const coordina_graph_assignee& pair)
           {
             return pair.person < board.person_count;
           })
    ? COORDINA_GRAPH_OK
    : invalid(outside_person);
}

coordina_graph_status to_status(PlanError error) noexcept
{
  if (error == PlanError::none)
  {
    return COORDINA_GRAPH_OK;
  }

  set_error(describe(error));
  return error == PlanError::cycle ? COORDINA_GRAPH_CYCLE : COORDINA_GRAPH_INVALID_ARGUMENT;
}

PlanningBoard build_board(const coordina_graph_board& board)
{
  return PlanningBoard{
    .nodes = build_node_ids(board.nodes, board.node_count),
    .edges = build<Edge>(
      board.edges,
      board.edge_count,
      [](const coordina_graph_edge& edge)
      {
        return Edge{.source = to_node_id(edge.source), .target = to_node_id(edge.target)};
      }),
    .items = build_work_items(board.work_items, board.work_item_count),
    .assignees = build_assignees(board.assignees, board.assignee_count),
    .labels = build<CardLabel>(
      board.labels,
      board.label_count,
      [](const coordina_graph_card_label& label)
      {
        return CardLabel{.node = to_node_id(label.node), .label = label.label};
      }),
    .milestones = build<Milestone>(
      board.milestones,
      board.milestone_count,
      [](const coordina_graph_milestone& milestone)
      {
        return Milestone{
          .node = to_node_id(milestone.node),
          .target_hours = milestone.target_hours,
          .weight = milestone.weight
        };
      }),
    .history = build<FinishedCard>(
      board.history,
      board.history_count,
      [](const coordina_graph_finished_card& card)
      {
        return FinishedCard{
          .node = to_node_id(card.node),
          .hours = card.hours,
          .age_days = card.age_days
        };
      }),
    .growth = ScopeGrowth{.discovered = board.discovered_count, .finished = board.finished_count},
    .person_count = board.person_count,
    .extra_people = board.extra_people
  };
}

SimulationOptions build_simulation(const coordina_graph_simulation& simulation) noexcept
{
  return SimulationOptions{
    .seed = simulation.seed,
    .run_count = simulation.run_count,
    .thread_count = simulation.thread_count
  };
}

std::vector<Assignee> build_assignees(const coordina_graph_assignee* assignees, std::size_t count)
{
  return build<Assignee>(
    assignees,
    count,
    [](const coordina_graph_assignee& assignee)
    {
      return Assignee{.node = to_node_id(assignee.node), .person = assignee.person};
    });
}

std::vector<NodeId> build_node_ids(const coordina_graph_node_id* nodes, std::size_t count)
{
  return build<NodeId>(nodes, count, to_node_id);
}

std::vector<CardToken> build_tokens(const coordina_graph_card_token* tokens, std::size_t count)
{
  return build<CardToken>(
    tokens,
    count,
    [](const coordina_graph_card_token& token)
    {
      return CardToken{
        .node = to_node_id(token.node),
        .token = token.token,
        .weight = token.weight
      };
    });
}
}
