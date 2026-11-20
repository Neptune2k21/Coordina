#include "planning/forecast_model.h"
#include "planning/ready_set.h"
#include "planning/schedule.h"

#include <cassert>
#include <cstddef>
#include <cstdint>
#include <limits>
#include <vector>

namespace
{
using coordina::graph::Assignee;
using coordina::graph::ForecastError;
using coordina::graph::ForecastModel;

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

  ForecastModel work(
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

coordina::graph::Schedule likely_schedule(const ForecastModel& model)
{
  const auto hours = model.likely_hours();
  coordina::graph::Scheduler scheduler(model);
  scheduler.run(hours);
  return scheduler.schedule();
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
  const auto serial = likely_schedule(alone);

  assert(scheduled(serial, 1U).start_hours == 0.0 && scheduled(serial, 1U).finish_hours == 6.0);
  assert(scheduled(serial, 2U).start_hours == 6.0 && scheduled(serial, 2U).finish_hours == 8.0);
  assert(scheduled(serial, 3U).start_hours == 8.0 && scheduled(serial, 3U).finish_hours == 8.5);
  assert(serial.finish_hours == 8.5);
  // Launch waited for the listing, which waited for Grace to finish the API.
  assert((chain_of(serial) == std::vector<unsigned long long>{1U, 2U, 3U}));

  // With Alan on the listing, both start at once and launch follows the API.
  const auto pair = board.work(3U, 2U, {Assignee{native(1U), 0U}, Assignee{native(2U), 1U}});
  const auto parallel = likely_schedule(pair);

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
  const auto schedule = likely_schedule(work);

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
  const auto waiting = likely_schedule(assigned);

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
  const auto schedule = likely_schedule(work);
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

void finds_the_smallest_ready_card_across_words()
{
  coordina::graph::ReadySet set(9000U);
  assert(set.empty() && set.min() == coordina::graph::ReadySet::none);

  for (const std::size_t index : {8999U, 4096U, 64U, 63U, 5000U})
  {
    set.insert(index);
  }

  set.insert(63U);
  assert(set.min() == 63U);
  set.erase(63U);
  assert(set.min() == 64U);
  set.erase(64U);
  set.erase(64U);
  assert(set.min() == 4096U);
  set.erase(4096U);
  set.erase(5000U);
  assert(set.min() == 8999U);
  set.erase(8999U);
  assert(set.empty() && set.min() == coordina::graph::ReadySet::none);
}
}

int main()
{
  schedules_one_card_at_a_time_per_person();
  gives_unassigned_cards_to_the_first_free_person();
  scopes_a_milestone_to_its_open_dependencies();
  rejects_forecasts_it_cannot_make();
  finds_the_smallest_ready_card_across_words();

  return 0;
}
