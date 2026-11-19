// Tests of coordina_graph_forecast: the milestone's scope, the schedule it
// simulates, and the Monte Carlo forecast through the C ABI.

#include "coordina_graph_engine.h"
#include "planning/forecast.h"

#include <cassert>
#include <cstddef>
#include <cstdint>
#include <limits>
#include <vector>

namespace
{
coordina_graph_node_id node(unsigned long long value)
{
  return coordina_graph_node_id{0U, value};
}

bool same_node(coordina_graph_node_id left, coordina_graph_node_id right)
{
  return left.high == right.high && left.low == right.low;
}

coordina_graph_work_item sized_item(
  unsigned long long id,
  std::uint32_t size,
  std::uint32_t position)
{
  coordina_graph_work_item item{};
  item.node = node(id);
  item.due_days = std::numeric_limits<std::int32_t>::max();
  item.position = position;
  item.size = size;
  return item;
}

using coordina::graph::Assignee;
using coordina::graph::ForecastError;
using coordina::graph::MilestoneWork;

coordina::graph::NodeId native(unsigned long long value)
{
  return coordina::graph::NodeId{.high = 0U, .low = value};
}

struct MilestoneBoard
{
  std::vector<coordina::graph::NodeId> nodes;
  std::vector<coordina::graph::Edge> edges;
  std::vector<coordina::graph::WorkItem> items;

  // An open card; the position keeps the plan_work order the list order.
  void card(unsigned long long id, coordina::graph::CardSize size, bool is_completed = false)
  {
    nodes.push_back(native(id));
    items.push_back(
      coordina::graph::WorkItem{
        .node = native(id),
        .is_completed = is_completed,
        .due_days = std::numeric_limits<std::int32_t>::max(),
        .position = static_cast<std::uint32_t>(items.size()),
        .size = size
      });
  }

  void depends(unsigned long long card, unsigned long long dependency)
  {
    edges.push_back(coordina::graph::Edge{.source = native(card), .target = native(dependency)});
  }

  MilestoneWork work(
    unsigned long long milestone,
    std::uint32_t person_count,
    const std::vector<Assignee>& assignees = {}) const
  {
    return {
      coordina::graph::GraphSnapshot(nodes, edges),
      items,
      assignees,
      native(milestone),
      person_count
    };
  }
};

const coordina::graph::ScheduledCard& scheduled(
  const coordina::graph::Schedule& schedule,
  unsigned long long id)
{
  for (const auto& card : schedule.cards)
  {
    if (card.node.low == id)
    {
      return card;
    }
  }

  assert(false && "card not scheduled");
  return schedule.cards.front();
}

std::vector<unsigned long long> chain_of(const coordina::graph::Schedule& schedule)
{
  std::vector<unsigned long long> chain;
  chain.reserve(schedule.driving_chain.size());

  for (const auto index : schedule.driving_chain)
  {
    chain.push_back(schedule.cards[index].node.low);
  }

  return chain;
}

void schedules_one_card_at_a_time_per_person()
{
  using coordina::graph::CardSize;

  // Launch (XS) needs the payment API (M) and the store listing (S).
  MilestoneBoard board;
  board.card(1U, CardSize::m);
  board.card(2U, CardSize::s);
  board.card(3U, CardSize::xs);
  board.depends(3U, 1U);
  board.depends(3U, 2U);

  // Alone, Grace does M, then S, then XS: 6 h, 8 h, 8.5 h.
  const auto alone = board.work(3U, 1U, {Assignee{native(1U), 0U}, Assignee{native(2U), 0U}});
  assert(alone.error() == ForecastError::none);
  const auto serial = alone.schedule(alone.likely_hours());

  assert(scheduled(serial, 1U).start_hours == 0.0 && scheduled(serial, 1U).finish_hours == 6.0);
  assert(scheduled(serial, 2U).start_hours == 6.0 && scheduled(serial, 2U).finish_hours == 8.0);
  assert(scheduled(serial, 3U).start_hours == 8.0 && scheduled(serial, 3U).finish_hours == 8.5);
  assert(serial.finish_hours == 8.5);
  // Launch waited for the listing, which waited for Grace to finish the API.
  assert((chain_of(serial) == std::vector<unsigned long long>{1U, 2U, 3U}));

  // With Alan on the listing, both start at once and launch follows the API.
  const auto pair = board.work(3U, 2U, {Assignee{native(1U), 0U}, Assignee{native(2U), 1U}});
  const auto parallel = pair.schedule(pair.likely_hours());

  assert(scheduled(parallel, 2U).person == 1U && scheduled(parallel, 2U).finish_hours == 2.0);
  assert(scheduled(parallel, 3U).start_hours == 6.0 && scheduled(parallel, 3U).person == 0U);
  assert(parallel.finish_hours == 6.5);
  assert((chain_of(parallel) == std::vector<unsigned long long>{1U, 3U}));
}

void gives_unassigned_cards_to_the_first_free_person()
{
  using coordina::graph::CardSize;

  // Launch (XS) needs three unassigned M cards; two people share them.
  MilestoneBoard board;
  board.card(1U, CardSize::m);
  board.card(2U, CardSize::m);
  board.card(3U, CardSize::m);
  board.card(4U, CardSize::xs);
  board.depends(4U, 1U);
  board.depends(4U, 2U);
  board.depends(4U, 3U);

  const auto work = board.work(4U, 2U);
  const auto schedule = work.schedule(work.likely_hours());

  assert(scheduled(schedule, 1U).person == 0U && scheduled(schedule, 1U).start_hours == 0.0);
  assert(scheduled(schedule, 2U).person == 1U && scheduled(schedule, 2U).start_hours == 0.0);
  // Both are free at 6 h; the first of them takes the third card.
  assert(scheduled(schedule, 3U).person == 0U && scheduled(schedule, 3U).start_hours == 6.0);
  assert(scheduled(schedule, 4U).start_hours == 12.0);
  assert(schedule.finish_hours == 12.5);
  assert((chain_of(schedule) == std::vector<unsigned long long>{1U, 3U, 4U}));

  // A card waits for its own assignee even when someone else is free.
  const auto assigned = board.work(
    4U,
    2U,
    {Assignee{native(1U), 1U}, Assignee{native(2U), 1U}, Assignee{native(3U), 1U}});
  const auto waiting = assigned.schedule(assigned.likely_hours());

  assert(scheduled(waiting, 2U).person == 1U && scheduled(waiting, 2U).start_hours == 6.0);
  assert(scheduled(waiting, 3U).person == 1U && scheduled(waiting, 3U).start_hours == 12.0);
  assert(scheduled(waiting, 4U).person == 0U && scheduled(waiting, 4U).start_hours == 18.0);
  assert(waiting.finish_hours == 18.5);
  assert((chain_of(waiting) == std::vector<unsigned long long>{1U, 2U, 3U, 4U}));
}

void scopes_a_milestone_to_its_open_dependencies()
{
  using coordina::graph::CardSize;

  // 1 depends on 2 (done), which depends on 3; 4 is another milestone's work.
  MilestoneBoard board;
  board.card(1U, CardSize::s);
  board.card(2U, CardSize::xl, true);
  board.card(3U, CardSize::none);
  board.card(4U, CardSize::xxl);
  board.depends(1U, 2U);
  board.depends(2U, 3U);

  const auto work = board.work(1U, 2U);
  assert(work.nodes().size() == 2U);
  assert(work.unsized_count() == 1U);

  // The done card no longer holds 1 back, so 1 and 3 (planned as M) run at
  // once.
  const auto schedule = work.schedule(work.likely_hours());
  assert(scheduled(schedule, 1U).start_hours == 0.0);
  assert(scheduled(schedule, 3U).start_hours == 0.0);
  assert(schedule.finish_hours == 6.0);

  // A milestone without dependencies covers only itself.
  assert(board.work(4U, 1U).nodes().size() == 1U);
}

void rejects_forecasts_it_cannot_make()
{
  using coordina::graph::CardSize;

  MilestoneBoard board;
  board.card(1U, CardSize::s);
  board.card(2U, CardSize::s);
  board.card(3U, CardSize::s, true);
  board.depends(1U, 2U);

  assert(board.work(9U, 1U).error() == ForecastError::unknown_milestone);
  assert(board.work(1U, 0U).error() == ForecastError::unknown_person);
  assert(board.work(1U, 1U, {Assignee{native(2U), 1U}}).error() == ForecastError::unknown_person);

  // A loop through open cards blocks them all; through a done card it does not.
  auto loop = board;
  loop.depends(2U, 1U);
  assert(loop.work(1U, 1U).error() == ForecastError::cycle);

  auto done_loop = board;
  done_loop.depends(2U, 3U);
  done_loop.depends(3U, 1U);
  assert(done_loop.work(1U, 1U).error() == ForecastError::none);
}

struct ForecastBoard
{
  std::vector<coordina_graph_node_id> nodes;
  std::vector<coordina_graph_edge> edges;
  std::vector<coordina_graph_work_item> items;
  std::vector<coordina_graph_assignee> assignees;
};

// A release that needs two chains of work and a review, shared by two people.
ForecastBoard release_board()
{
  ForecastBoard board;
  const std::uint32_t sizes[] = {
    COORDINA_GRAPH_SIZE_M,
    COORDINA_GRAPH_SIZE_L,
    COORDINA_GRAPH_SIZE_S,
    COORDINA_GRAPH_SIZE_XL,
    COORDINA_GRAPH_SIZE_NONE,
    COORDINA_GRAPH_SIZE_XS
  };

  for (std::uint32_t index = 0U; index < 6U; ++index)
  {
    board.nodes.push_back(node(index + 1U));
    board.items.push_back(sized_item(index + 1U, sizes[index], index));
  }

  board.edges = {
    coordina_graph_edge{node(2U), node(1U)},
    coordina_graph_edge{node(4U), node(3U)},
    coordina_graph_edge{node(5U), node(2U)},
    coordina_graph_edge{node(5U), node(4U)},
    coordina_graph_edge{node(6U), node(5U)}
  };
  board.assignees = {
    coordina_graph_assignee{node(1U), 0U, 0U},
    coordina_graph_assignee{node(3U), 1U, 0U},
    coordina_graph_assignee{node(4U), 1U, 0U}
  };
  return board;
}

coordina_graph_status run_forecast(
  const ForecastBoard& board,
  const coordina_graph_forecast_options& options,
  std::vector<coordina_graph_card_criticality>& cards,
  coordina_graph_milestone_forecast& output)
{
  output = coordina_graph_milestone_forecast{};
  output.cards = cards.data();
  output.card_capacity = cards.size();

  return coordina_graph_forecast(
    board.nodes.data(),
    board.nodes.size(),
    board.edges.data(),
    board.edges.size(),
    board.items.data(),
    board.items.size(),
    board.assignees.data(),
    board.assignees.size(),
    &options,
    &output);
}

void forecasts_a_reproducible_range()
{
  const auto board = release_board();
  const coordina_graph_forecast_options options{node(6U), 42U, 2U, 2000U};
  std::vector<coordina_graph_card_criticality> cards(6U);
  coordina_graph_milestone_forecast output{};

  assert(run_forecast(board, options, cards, output) == COORDINA_GRAPH_OK);
  assert(output.card_count == 6U);
  assert(output.unsized_count == 1U);
  assert(output.p50_days > 0.0);
  assert(output.p50_days <= output.p85_days);

  // Every run ends with the milestone, since it waits for all the rest.
  assert(cards[0U].node.low == 6U && cards[0U].criticality == 1.0);

  for (std::size_t index = 0U; index < output.card_count; ++index)
  {
    assert(cards[index].criticality >= 0.0 && cards[index].criticality <= 1.0);
    assert(index == 0U || cards[index - 1U].criticality >= cards[index].criticality);
  }

  // The same seed gives the same forecast, card for card.
  std::vector<coordina_graph_card_criticality> again(6U);
  coordina_graph_milestone_forecast repeated{};
  assert(run_forecast(board, options, again, repeated) == COORDINA_GRAPH_OK);
  assert(repeated.p50_days == output.p50_days && repeated.p85_days == output.p85_days);

  for (std::size_t index = 0U; index < output.card_count; ++index)
  {
    assert(same_node(again[index].node, cards[index].node));
    assert(again[index].criticality == cards[index].criticality);
  }

  // Alone, one person does everything in a row: between the sum of the low
  // and the sum of the high durations (unsized counts as M).
  auto alone = board;
  alone.assignees.clear();
  const coordina_graph_forecast_options solo{node(6U), 7U, 1U, 2000U};
  assert(run_forecast(alone, solo, cards, output) == COORDINA_GRAPH_OK);

  constexpr auto day = coordina::graph::focus_hours_per_day;
  const auto low = (3.0 + 12.0 + 1.0 + 24.0 + 3.0 + 0.25) / day;
  const auto high = (12.0 + 24.0 + 3.0 + 48.0 + 12.0 + 1.0) / day;
  assert(output.p50_days > low && output.p85_days < high);

  // In a row, every card drives the date in every run.
  for (std::size_t index = 0U; index < output.card_count; ++index)
  {
    assert(cards[index].criticality == 1.0);
  }
}

void forecast_reports_missing_capacity_and_bad_arguments()
{
  const auto board = release_board();
  const coordina_graph_forecast_options options{node(6U), 1U, 2U, 10U};
  std::vector<coordina_graph_card_criticality> small(2U);
  coordina_graph_milestone_forecast output{};

  assert(run_forecast(board, options, small, output) == COORDINA_GRAPH_INSUFFICIENT_CAPACITY);
  assert(output.card_count == 6U);
  assert(small[0U].node.low == 0U);

  std::vector<coordina_graph_card_criticality> cards(6U);
  auto unknown = options;
  unknown.milestone = node(99U);
  assert(run_forecast(board, unknown, cards, output) == COORDINA_GRAPH_INVALID_ARGUMENT);

  auto no_runs = options;
  no_runs.run_count = 0U;
  assert(run_forecast(board, no_runs, cards, output) == COORDINA_GRAPH_INVALID_ARGUMENT);

  auto small_team = options;
  small_team.person_count = 1U;
  assert(run_forecast(board, small_team, cards, output) == COORDINA_GRAPH_INVALID_ARGUMENT);

  auto loop = board;
  loop.edges.push_back(coordina_graph_edge{node(1U), node(6U)});
  assert(run_forecast(loop, options, cards, output) == COORDINA_GRAPH_CYCLE);

  assert(
    coordina_graph_forecast(
      board.nodes.data(),
      board.nodes.size(),
      board.edges.data(),
      board.edges.size(),
      board.items.data(),
      board.items.size(),
      nullptr,
      1U,
      &options,
      &output)
    == COORDINA_GRAPH_INVALID_ARGUMENT);
}
}

int main()
{
  schedules_one_card_at_a_time_per_person();
  gives_unassigned_cards_to_the_first_free_person();
  scopes_a_milestone_to_its_open_dependencies();
  rejects_forecasts_it_cannot_make();
  forecasts_a_reproducible_range();
  forecast_reports_missing_capacity_and_bad_arguments();

  return 0;
}
