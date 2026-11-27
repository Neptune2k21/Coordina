#ifndef COORDINA_GRAPH_ENGINE_TESTS_PLAN_BOARD_H
#define COORDINA_GRAPH_ENGINE_TESTS_PLAN_BOARD_H

#include "abi_fixtures.h"
#include "coordina_graph_engine.h"

#include <algorithm>
#include <cassert>
#include <cstddef>
#include <cstdint>
#include <string_view>
#include <utility>
#include <vector>

// A board as the API passes it, and the planning calls the tests make on it.
namespace coordina::graph::fixtures
{
constexpr unsigned long long milestone_id = 1000U;

// Card and person.
using Changes = std::vector<std::pair<unsigned long long, std::uint32_t>>;

struct ForecastResult
{
  const coordina_graph_milestone_forecast& first() const
  {
    return milestones.front();
  }

  const coordina_graph_card_forecast& card(
    unsigned long long id,
    std::uint32_t milestone = 0U) const
  {
    const auto found = std::ranges::find_if(
      cards,
      [&](const auto& forecast)
      {
        return forecast.node.low == id && forecast.milestone == milestone;
      });
    assert(found != cards.end());
    return *found;
  }

  const coordina_graph_planned_card& planned(unsigned long long id) const
  {
    const auto found = std::ranges::find_if(
      plan,
      [&](const auto& planned)
      {
        return planned.node.low == id;
      });
    assert(found != plan.end());
    return *found;
  }

  coordina_graph_status status = COORDINA_GRAPH_OK;
  std::vector<coordina_graph_milestone_forecast> milestones;
  std::vector<coordina_graph_card_forecast> cards;
  std::vector<coordina_graph_planned_card> plan;
};

struct ScopeResult
{
  coordina_graph_status status = COORDINA_GRAPH_OK;
  Ids cuts;
  Ids dropped;
  std::vector<std::uint32_t> dropped_by;
  coordina_graph_scope_cut output{};
};

struct StaffingResult
{
  coordina_graph_status status = COORDINA_GRAPH_OK;
  Changes changes;
  std::vector<coordina_graph_staff_change> details;
  coordina_graph_staffing output{};
};

inline std::vector<coordina_graph_node_id> nodes_from(const Ids& ids)
{
  std::vector<coordina_graph_node_id> nodes;

  for (const auto id : ids)
  {
    nodes.push_back(node(id));
  }

  return nodes;
}

struct PlanBoard
{
  coordina_graph_work_item& card(
    unsigned long long id,
    std::uint32_t size,
    std::uint32_t priority = 0U)
  {
    nodes.push_back(node(id));
    auto item = sized_item(id, size, static_cast<std::uint32_t>(items.size()));
    item.priority = priority;
    items.push_back(item);
    return items.back();
  }

  coordina_graph_work_item& item(unsigned long long id)
  {
    const auto found = std::ranges::find_if(
      items,
      [id](const auto& candidate)
      {
        return candidate.node.low == id;
      });
    assert(found != items.end());
    return *found;
  }

  void milestone(unsigned long long id, double target_hours, double weight = 1.0)
  {
    milestones.push_back(coordina_graph_milestone{node(id), target_hours, weight});
  }

  void depends(unsigned long long card, unsigned long long dependency)
  {
    edges.push_back(coordina_graph_edge{node(card), node(dependency)});
  }

  void assign(unsigned long long card, std::uint32_t person)
  {
    assignees.push_back(coordina_graph_assignee{node(card), person, 0U});
  }

  void label(unsigned long long card, std::uint32_t label)
  {
    labels.push_back(coordina_graph_card_label{node(card), label, 0U});
  }

  // A completed card that took that long.
  void finished(unsigned long long id, std::uint32_t size, double hours, double age_days = 1.0)
  {
    card(id, size).is_completed = 1U;
    history.push_back(coordina_graph_finished_card{node(id), hours, age_days});
  }

  // One person, unless the test says who works on the board.
  PlanBoard alone() const
  {
    auto board = *this;
    board.assignees.clear();
    return board;
  }

  coordina_graph_board view() const
  {
    return coordina_graph_board{
      .nodes = nodes.data(),
      .node_count = nodes.size(),
      .edges = edges.data(),
      .edge_count = edges.size(),
      .work_items = items.data(),
      .work_item_count = items.size(),
      .assignees = assignees.data(),
      .assignee_count = assignees.size(),
      .labels = labels.data(),
      .label_count = labels.size(),
      .milestones = milestones.data(),
      .milestone_count = milestones.size(),
      .history = history.data(),
      .history_count = history.size(),
      .person_count = people,
      .discovered_count = discovered,
      .finished_count = done_in_scope,
      .extra_people = 0U
    };
  }

  ForecastResult forecast(std::uint32_t runs = 2000U, std::uint32_t threads = 1U) const
  {
    const auto board = view();
    const coordina_graph_simulation simulation{seed, runs, threads};
    ForecastResult result;
    result.milestones.resize(milestones.size());
    result.cards.resize(milestones.size() * nodes.size());
    result.plan.resize(nodes.size());
    coordina_graph_forecast_output output{
      .milestones = result.milestones.data(),
      .milestone_capacity = result.milestones.size(),
      .milestone_count = 0U,
      .cards = result.cards.data(),
      .card_capacity = result.cards.size(),
      .card_count = 0U,
      .plan = result.plan.data(),
      .plan_capacity = result.plan.size(),
      .plan_count = 0U
    };
    result.status = coordina_graph_forecast(&board, &simulation, &output);
    result.milestones.resize(output.milestone_count);
    result.cards.resize(output.card_count);
    result.plan.resize(output.plan_count);
    return result;
  }

  coordina_graph_scope_options scope_options() const
  {
    return coordina_graph_scope_options{
      .milestone = node(milestone_id),
      .simulation = {seed, 2000U, 1U},
      .goal = 0.85,
      .screen_run_count = 256U,
      .evaluation_limit = 200U,
      .time_limit_ms = 0U,
      .reserved = 0U
    };
  }

  ScopeResult propose_scope(const coordina_graph_scope_options& options, const Ids& keep = {}) const
  {
    const auto board = view();
    const auto kept = nodes_from(keep);
    return solve_scope(
      [&](coordina_graph_scope_cut* output)
      {
        return coordina_graph_propose_scope(&board, kept.data(), kept.size(), &options, output);
      });
  }

  ScopeResult evaluate_scope(const Ids& cuts) const
  {
    const auto board = view();
    const auto options = scope_options();
    const auto chosen = nodes_from(cuts);
    return solve_scope(
      [&](coordina_graph_scope_cut* output)
      {
        return coordina_graph_evaluate_scope(
          &board,
          chosen.data(),
          chosen.size(),
          &options,
          output);
      });
  }

  coordina_graph_staffing_options staffing_options(bool allow_reassignment = false) const
  {
    return coordina_graph_staffing_options{
      .milestone = node(milestone_id),
      .simulation = {seed, 2000U, 1U},
      .screen_run_count = 8U,
      .allow_reassignment = allow_reassignment ? 1U : 0U,
      .evaluation_limit = 2000U,
      .time_limit_ms = 0U
    };
  }

  StaffingResult propose_staffing(const coordina_graph_staffing_options& options) const
  {
    const auto board = view();
    return solve_staffing(
      [&](coordina_graph_staffing* output)
      {
        return coordina_graph_propose_staffing(
          &board,
          receivers.data(),
          receivers.size(),
          skills.data(),
          skills.size(),
          &options,
          output);
      });
  }

  StaffingResult evaluate_staffing(const Changes& chosen) const
  {
    const auto board = view();
    const auto options = staffing_options();
    std::vector<coordina_graph_assignee> changes;

    for (const auto& [card, person] : chosen)
    {
      changes.push_back(coordina_graph_assignee{node(card), person, 0U});
    }

    return solve_staffing(
      [&](coordina_graph_staffing* output)
      {
        return coordina_graph_evaluate_staffing(
          &board,
          changes.data(),
          changes.size(),
          &options,
          output);
      });
  }

  // The board once the changes are applied.
  PlanBoard with(const Changes& changes) const
  {
    auto board = *this;

    for (const auto& [card, person] : changes)
    {
      std::erase_if(
        board.assignees,
        [id = card](const auto& assignee)
        {
          return assignee.node.low == id;
        });
      board.assign(card, person);
    }

    return board;
  }

  // The board once the milestone no longer depends on those cards.
  PlanBoard without(const Ids& dropped) const
  {
    auto board = *this;
    std::erase_if(
      board.edges,
      [&](const auto& edge)
      {
        return edge.source.low == milestone_id
          && std::ranges::find(dropped, edge.target.low) != dropped.end();
      });
    return board;
  }

  std::vector<coordina_graph_node_id> nodes;
  std::vector<coordina_graph_edge> edges;
  std::vector<coordina_graph_work_item> items;
  std::vector<coordina_graph_assignee> assignees;
  std::vector<coordina_graph_card_label> labels;
  std::vector<coordina_graph_milestone> milestones;
  std::vector<coordina_graph_finished_card> history;
  std::vector<std::uint32_t> receivers;
  std::vector<coordina_graph_assignee> skills;
  std::uint32_t people = 0U;
  std::uint32_t discovered = 0U;
  std::uint32_t done_in_scope = 0U;
  std::uint64_t seed = 131U;

private:
  template <typename Call>
  ScopeResult solve_scope(const Call& call) const
  {
    std::vector<coordina_graph_node_id> cuts(nodes.size());
    std::vector<coordina_graph_dropped_card> dropped(nodes.size());
    ScopeResult result;
    result.output.cuts = cuts.data();
    result.output.cut_capacity = cuts.size();
    result.output.dropped = dropped.data();
    result.output.dropped_capacity = dropped.size();
    result.status = call(&result.output);

    if (result.status == COORDINA_GRAPH_OK)
    {
      cuts.resize(result.output.cut_count);
      result.cuts = ids_of(cuts);

      for (std::size_t index = 0U; index < result.output.dropped_count; ++index)
      {
        result.dropped.push_back(dropped[index].node.low);
        result.dropped_by.push_back(dropped[index].cut);
      }

      std::ranges::sort(result.dropped);
    }

    result.output.cuts = nullptr;
    result.output.dropped = nullptr;
    return result;
  }

  template <typename Call>
  StaffingResult solve_staffing(const Call& call) const
  {
    std::vector<coordina_graph_staff_change> changes(nodes.size());
    StaffingResult result;
    result.output.changes = changes.data();
    result.output.change_capacity = changes.size();
    result.status = call(&result.output);

    if (result.status == COORDINA_GRAPH_OK)
    {
      changes.resize(result.output.change_count);
      result.details = changes;

      for (const auto& change : changes)
      {
        result.changes.emplace_back(change.node.low, change.person);
      }
    }

    result.output.changes = nullptr;
    return result;
  }
};

inline bool last_error_says(std::string_view text)
{
  return std::string_view(coordina_graph_last_error()).find(text) != std::string_view::npos;
}
}

#endif
