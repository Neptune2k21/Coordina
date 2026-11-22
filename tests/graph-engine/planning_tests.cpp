#include "abi_fixtures.h"
#include "coordina_graph_engine.h"
#include "planning/card_size.h"

#include <cassert>
#include <cstddef>
#include <cstdint>
#include <utility>
#include <vector>

namespace
{
using namespace coordina::graph::fixtures;

std::vector<coordina_graph_work_recommendation> plan(
  const std::vector<coordina_graph_work_item>& items,
  const coordina_graph_planning_options* planning_options,
  const std::vector<coordina_graph_edge>& edges = {})
{
  auto nodes = nodes_of(items);

  for (const auto& edge : edges)
  {
    nodes.push_back(edge.target);
  }

  const Graph graph(std::move(nodes), edges);
  std::vector<coordina_graph_work_recommendation> output(graph.nodes.size());
  output.resize(graph.call(
    coordina_graph_plan_work,
    items.data(),
    items.size(),
    planning_options,
    output.data(),
    output.size()));
  return output;
}

Ids weighted_path(
  const std::vector<coordina_graph_work_item>& items,
  const std::vector<coordina_graph_edge>& edges)
{
  return Graph(nodes_of(items), edges)
    .ids(coordina_graph_critical_path, items.data(), items.size());
}

void has_no_critical_path_without_dependencies()
{
  const Graph graph(2U, {});

  assert(graph.ids(coordina_graph_critical_path, nullptr, 0U).empty());

  const auto plan_output = plan({work_item(1U, 0U), work_item(2U, 1U)}, nullptr);
  assert(plan_output[0U].is_critical_path == 0U);
  assert(plan_output[1U].is_critical_path == 0U);
}

void ranks_actionable_work_by_impact()
{
  const Graph graph(4U, {{2U, 1U}, {3U, 2U}});
  const coordina_graph_work_item work_items[] = {
    coordina_graph_work_item{node(1U), 0U, 1U, 7, 0U, 0U, 0U, 0U, 0U, 0U, 0U},
    coordina_graph_work_item{node(2U), 0U, 3U, 0, 3U, 1U, 1U, 0U, 0U, 0U, 0U},
    coordina_graph_work_item{node(3U), 0U, 3U, -1, 2U, 0U, 2U, 0U, 0U, 0U, 0U},
    coordina_graph_work_item{node(4U), 0U, 3U, 0, 0U, 0U, 3U, 0U, 0U, 0U, 0U}
  };
  coordina_graph_work_recommendation output[4U] = {};

  const auto count = graph.call(coordina_graph_plan_work, work_items, 4U, nullptr, output, 4U);

  assert(count == 4U);
  assert(output[0U].node.low == 1U);
  assert(output[0U].blocker_count == 0U);
  assert(output[0U].is_critical_path == 1U);
  assert(output[0U].unlock_score > output[1U].unlock_score);
  assert(output[1U].node.low == 4U);
  assert(output[1U].blocker_count == 0U);
  assert(output[2U].blocker_count > 0U);
}

void prefers_work_that_fits_the_focus_block()
{
  auto quick = work_item(1U, 2U);
  quick.size = COORDINA_GRAPH_SIZE_XS;
  auto unknown = work_item(2U, 0U);
  auto long_task = work_item(3U, 1U);
  long_task.size = COORDINA_GRAPH_SIZE_M;
  const auto focus = options(COORDINA_GRAPH_SIZE_S);

  const auto output = plan({quick, unknown, long_task}, &focus);

  assert(output.size() == 3U);
  assert(output[0U].node.low == 1U);
  assert(output[0U].fits_max_size == 1U);
  assert(output[0U].effort_score > 0);
  assert(output[1U].node.low == 2U);
  assert(output[1U].fits_max_size == 0U);
  assert(output[1U].effort_score == 0);
  assert(output[2U].node.low == 3U);
  assert(output[2U].fits_max_size == 0U);
  assert(output[2U].effort_score < 0);
}

void ignores_effort_without_a_time_limit()
{
  auto quick = work_item(1U, 1U);
  quick.size = COORDINA_GRAPH_SIZE_XS;
  auto long_task = work_item(2U, 0U);
  long_task.size = COORDINA_GRAPH_SIZE_XL;
  const auto unrestricted = options(COORDINA_GRAPH_SIZE_NONE);

  const auto output = plan({quick, long_task}, &unrestricted);

  assert(output[0U].node.low == 2U);
  assert(output[0U].fits_max_size == 1U);
  assert(output[1U].fits_max_size == 1U);
  assert(output[0U].effort_score == 0);
}

void orders_size_ranges_from_xs_to_xxl()
{
  using coordina::graph::CardSize;
  using coordina::graph::size_range;

  const CardSize sizes[] =
    {CardSize::xs, CardSize::s, CardSize::m, CardSize::l, CardSize::xl, CardSize::xxl};
  auto previous = size_range(CardSize::none);

  assert(previous.likely_hours == 0.0);

  for (const auto size : sizes)
  {
    const auto range = size_range(size);

    assert(range.low_hours > 0.0);
    assert(range.low_hours <= range.likely_hours && range.likely_hours <= range.high_hours);
    assert(range.likely_hours > previous.likely_hours);
    assert(range.high_hours >= previous.high_hours);
    // Bigger cards are less certain: the spread never narrows.
    assert(range.high_hours / range.low_hours >= 1.5);
    assert(range.should_split == (size == CardSize::xxl));
    previous = range;
  }
}

void reads_unknown_sizes_as_none()
{
  using coordina::graph::CardSize;
  using coordina::graph::fits_size_limit;
  using coordina::graph::to_card_size;

  assert(to_card_size(COORDINA_GRAPH_SIZE_XXL) == CardSize::xxl);
  assert(to_card_size(7U) == CardSize::none);
  assert(fits_size_limit(CardSize::xxl, CardSize::none));
  assert(fits_size_limit(CardSize::s, CardSize::s));
  assert(!fits_size_limit(CardSize::m, CardSize::s));
  assert(!fits_size_limit(CardSize::none, CardSize::m));
}

void ranks_requester_work_before_others()
{
  auto mine = work_item(1U, 2U);
  mine.assignment = 1U;
  auto unassigned = work_item(2U, 1U);
  auto theirs = work_item(3U, 0U);
  theirs.assignment = 2U;
  const auto preferred = options(0U, 1U, 1U);
  const auto owned_only = options(0U, 1U, 0U);
  const auto neutral = options(0U, 0U, 1U);

  const auto ranked = plan({mine, unassigned, theirs}, &preferred);
  assert(ranked[0U].node.low == 1U);
  assert(ranked[0U].is_assignment_match == 1U);
  assert(ranked[0U].assignment_score > 0);
  assert(ranked[1U].node.low == 2U);
  assert(ranked[2U].node.low == 3U);
  assert(ranked[2U].assignment_score < 0);

  const auto strict = plan({mine, unassigned, theirs}, &owned_only);
  assert(strict[0U].node.low == 1U);
  assert(strict[1U].node.low == 3U);
  assert(strict[2U].node.low == 2U);

  const auto by_position = plan({mine, unassigned, theirs}, &neutral);
  assert(by_position[0U].node.low == 3U);
  assert(by_position[2U].node.low == 1U);
}

void finishes_started_work_first()
{
  auto todo = work_item(1U, 0U);
  auto started = work_item(2U, 1U);
  started.is_in_progress = 1U;

  const auto output = plan({todo, started}, nullptr);

  assert(output[0U].node.low == 2U);
  assert(output[0U].is_in_progress == 1U);
  assert(output[0U].momentum_score > 0U);
  assert(output[1U].momentum_score == 0U);
}

void prefers_matching_context()
{
  auto other = work_item(1U, 0U);
  auto matching = work_item(2U, 1U);
  matching.is_context_match = 1U;

  const auto output = plan({other, matching}, nullptr);

  assert(output[0U].node.low == 2U);
  assert(output[0U].is_context_match == 1U);
  assert(output[0U].context_score > 0U);
}

void breaks_full_ties_by_node_id()
{
  const auto output = plan({work_item(9U, 0U), work_item(3U, 0U)}, nullptr);

  assert(output[0U].node.low == 3U);
  assert(output[1U].node.low == 9U);
}

void keeps_preference_order_below_the_score_floor()
{
  auto theirs = work_item(1U, 0U);
  theirs.size = COORDINA_GRAPH_SIZE_XS;
  theirs.assignment = 2U;
  auto long_mine = work_item(2U, 1U);
  long_mine.size = COORDINA_GRAPH_SIZE_L;
  long_mine.assignment = 1U;
  const auto focus = options(COORDINA_GRAPH_SIZE_S);

  const auto output = plan({theirs, long_mine}, &focus);

  assert(output[0U].score == 1U);
  assert(output[1U].score == 1U);
  assert(output[0U].node.low == 2U);
}

void clamps_heavily_blocked_work()
{
  std::vector<coordina_graph_edge> edges;

  for (unsigned long long id = 100U; id < 5100U; ++id)
  {
    edges.push_back(coordina_graph_edge{node(1U), node(id)});
  }

  const auto output = plan({work_item(1U, 0U), work_item(2U, 1U)}, nullptr, edges);

  assert(output.size() == 2U);
  assert(output[0U].node.low == 2U);
  assert(output[1U].blocker_count == 5000U);
  assert(output[1U].score == 1U);
}

void weighs_the_critical_path_by_size()
{
  // Three XS cards in a row take 1.5 h; an M after an L takes 24 h.
  const std::vector<coordina_graph_work_item> items = {
    sized_item(1U, COORDINA_GRAPH_SIZE_XS, 0U),
    sized_item(2U, COORDINA_GRAPH_SIZE_XS, 1U),
    sized_item(3U, COORDINA_GRAPH_SIZE_XS, 2U),
    sized_item(4U, COORDINA_GRAPH_SIZE_L, 3U),
    sized_item(5U, COORDINA_GRAPH_SIZE_M, 4U)
  };
  const std::vector<coordina_graph_edge> edges = {
    coordina_graph_edge{node(2U), node(1U)},
    coordina_graph_edge{node(3U), node(2U)},
    coordina_graph_edge{node(5U), node(4U)}
  };

  assert(weighted_path(items, edges) == (Ids{4U, 5U}));

  // Unsized cards count as M: two of them (12 h) beat three S (6 h).
  const std::vector<coordina_graph_work_item> unsized = {
    sized_item(1U, COORDINA_GRAPH_SIZE_S, 0U),
    sized_item(2U, COORDINA_GRAPH_SIZE_S, 1U),
    sized_item(3U, COORDINA_GRAPH_SIZE_S, 2U),
    sized_item(4U, COORDINA_GRAPH_SIZE_NONE, 3U),
    sized_item(5U, COORDINA_GRAPH_SIZE_NONE, 4U)
  };

  assert(weighted_path(unsized, edges) == (Ids{4U, 5U}));
}

void leaves_completed_cards_out_of_the_critical_path()
{
  // 3 depends on 2, which depends on 1; 5 depends on 4.
  const std::vector<coordina_graph_edge> edges = {
    coordina_graph_edge{node(2U), node(1U)},
    coordina_graph_edge{node(3U), node(2U)},
    coordina_graph_edge{node(5U), node(4U)}
  };
  const std::vector<coordina_graph_work_item> first_done = {
    sized_item(1U, COORDINA_GRAPH_SIZE_XXL, 0U, true),
    sized_item(2U, COORDINA_GRAPH_SIZE_M, 1U),
    sized_item(3U, COORDINA_GRAPH_SIZE_M, 2U),
    sized_item(4U, COORDINA_GRAPH_SIZE_S, 3U),
    sized_item(5U, COORDINA_GRAPH_SIZE_S, 4U)
  };

  assert(weighted_path(first_done, edges) == (Ids{2U, 3U}));

  // A completed card in the middle cuts the chain: 3 no longer waits for 1.
  auto middle_done = first_done;
  middle_done[0U].is_completed = 0U;
  middle_done[1U].is_completed = 1U;

  assert(weighted_path(middle_done, edges) == (Ids{4U, 5U}));

  // A lone card has no path, however long it is.
  const std::vector<coordina_graph_work_item> lone = {
    sized_item(1U, COORDINA_GRAPH_SIZE_XXL, 0U),
    sized_item(2U, COORDINA_GRAPH_SIZE_XS, 1U, true),
    sized_item(3U, COORDINA_GRAPH_SIZE_XS, 2U),
    sized_item(4U, COORDINA_GRAPH_SIZE_XS, 3U),
    sized_item(5U, COORDINA_GRAPH_SIZE_XS, 4U, true)
  };

  assert(weighted_path(lone, edges).empty());
}
}

int main()
{
  has_no_critical_path_without_dependencies();
  ranks_actionable_work_by_impact();
  prefers_work_that_fits_the_focus_block();
  ignores_effort_without_a_time_limit();
  orders_size_ranges_from_xs_to_xxl();
  reads_unknown_sizes_as_none();
  ranks_requester_work_before_others();
  finishes_started_work_first();
  prefers_matching_context();
  breaks_full_ties_by_node_id();
  keeps_preference_order_below_the_score_floor();
  clamps_heavily_blocked_work();
  weighs_the_critical_path_by_size();
  leaves_completed_cards_out_of_the_critical_path();

  return 0;
}
