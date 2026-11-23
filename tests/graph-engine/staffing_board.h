#ifndef COORDINA_GRAPH_ENGINE_TESTS_STAFFING_BOARD_H
#define COORDINA_GRAPH_ENGINE_TESTS_STAFFING_BOARD_H

#include "abi_fixtures.h"
#include "coordina_graph_engine.h"

#include <algorithm>
#include <cassert>
#include <cstddef>
#include <cstdint>
#include <iterator>
#include <set>
#include <utility>
#include <vector>

// A board to plan who does what on, and the calls the staffing tests make.
namespace coordina::graph::fixtures
{
constexpr unsigned long long milestone_id = 1000U;

using Changes = std::vector<std::pair<unsigned long long, std::uint32_t>>;

struct StaffingResult
{
  coordina_graph_status status = COORDINA_GRAPH_OK;
  Changes changes;
  std::vector<coordina_graph_staff_change> details;
  coordina_graph_staffing output{};
};

// A board with one milestone. People are numbered as the API numbers them;
// receivers are who the search may give cards to.
struct StaffingBoard
{
  StaffingBoard()
  {
    card(milestone_id, COORDINA_GRAPH_SIZE_XS);
  }

  void card(unsigned long long id, std::uint32_t size)
  {
    nodes.push_back(node(id));
    items.push_back(sized_item(id, size, static_cast<std::uint32_t>(items.size())));
  }

  void depends(unsigned long long card, unsigned long long dependency)
  {
    edges.push_back(coordina_graph_edge{node(card), node(dependency)});
  }

  void assign(unsigned long long card, std::uint32_t person)
  {
    assignees.push_back(coordina_graph_assignee{node(card), person, 0U});
  }

  void skilled(unsigned long long card, std::uint32_t person)
  {
    skills.push_back(coordina_graph_assignee{node(card), person, 0U});
  }

  coordina_graph_staffing_options options(bool allow_reassignment = false) const
  {
    return coordina_graph_staffing_options{
      .milestone = node(milestone_id),
      .seed = 133U,
      .person_count = people,
      .run_count = 2000U,
      .screen_run_count = 8U,
      .thread_count = 1U,
      .allow_reassignment = allow_reassignment ? 1U : 0U,
      .evaluation_limit = 2000U,
      .time_limit_ms = 0U,
      .reserved = 0U
    };
  }

  StaffingResult propose(const coordina_graph_staffing_options& staffing) const
  {
    return solve(
      [&](coordina_graph_staffing* output)
      {
        return coordina_graph_propose_staffing(
          nodes.data(),
          nodes.size(),
          edges.data(),
          edges.size(),
          items.data(),
          items.size(),
          assignees.data(),
          assignees.size(),
          receivers.data(),
          receivers.size(),
          skills.data(),
          skills.size(),
          &staffing,
          output);
      });
  }

  StaffingResult evaluate(
    const coordina_graph_staffing_options& staffing,
    const Changes& chosen) const
  {
    std::vector<coordina_graph_assignee> changes;

    for (const auto& [card, person] : chosen)
    {
      changes.push_back(coordina_graph_assignee{node(card), person, 0U});
    }

    return solve(
      [&](coordina_graph_staffing* output)
      {
        return coordina_graph_evaluate_staffing(
          nodes.data(),
          nodes.size(),
          edges.data(),
          edges.size(),
          items.data(),
          items.size(),
          assignees.data(),
          assignees.size(),
          changes.data(),
          changes.size(),
          &staffing,
          output);
      });
  }

  // The forecast the milestone shows once the changes are applied: each
  // changed card has one assignee, and the team is renumbered from the
  // assignees left, as the API numbers it.
  coordina_graph_milestone_forecast forecast_after(const Changes& changes) const
  {
    std::vector<coordina_graph_assignee> applied;

    for (const auto& assignee : assignees)
    {
      if (std::ranges::none_of(
            changes,
            [&](const auto& change)
            {
              return change.first == assignee.node.low;
            }))
      {
        applied.push_back(assignee);
      }
    }

    for (const auto& [card, person] : changes)
    {
      applied.push_back(coordina_graph_assignee{node(card), person, 0U});
    }

    std::set<std::uint32_t> team;

    for (const auto& assignee : applied)
    {
      team.insert(assignee.person);
    }

    for (auto& assignee : applied)
    {
      assignee.person =
        static_cast<std::uint32_t>(std::distance(team.begin(), team.find(assignee.person)));
    }

    std::vector<coordina_graph_card_forecast> cards(nodes.size());
    coordina_graph_milestone_forecast output{};
    output.cards = cards.data();
    output.card_capacity = cards.size();
    const coordina_graph_forecast_options forecast{
      node(milestone_id),
      133U,
      std::max<std::uint32_t>(1U, static_cast<std::uint32_t>(team.size())),
      2000U,
      1U,
      0U
    };
    const auto status = coordina_graph_forecast(
      nodes.data(),
      nodes.size(),
      edges.data(),
      edges.size(),
      items.data(),
      items.size(),
      applied.data(),
      applied.size(),
      &forecast,
      &output);
    assert(status == COORDINA_GRAPH_OK);
    output.cards = nullptr;
    return output;
  }

  std::vector<coordina_graph_node_id> nodes;
  std::vector<coordina_graph_edge> edges;
  std::vector<coordina_graph_work_item> items;
  std::vector<coordina_graph_assignee> assignees;
  std::vector<std::uint32_t> receivers;
  std::vector<coordina_graph_assignee> skills;
  std::uint32_t people = 0U;

private:
  template <typename Call>
  StaffingResult solve(const Call& call) const
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
}

#endif
