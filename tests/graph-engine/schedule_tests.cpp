#include "planning/board_index.h"
#include "planning/planned_board.h"
#include "planning/ready_set.h"
#include "planning/schedule.h"

#include <algorithm>
#include <cassert>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <limits>
#include <vector>

namespace
{
using namespace coordina::graph;

NodeId native(unsigned long long value)
{
  return NodeId{.high = 0U, .low = value};
}

// Durations are exponentials of sums of logarithms: equal up to the last bits.
bool lasts(double hours, double expected)
{
  return std::abs(hours - expected) < 1e-9;
}

struct Board : PlanningBoard
{
  void card(unsigned long long id, CardSize size, bool is_done = false)
  {
    WorkItem item;
    item.node = native(id);
    item.is_completed = is_done;
    item.due_days = std::numeric_limits<std::int32_t>::max();
    item.position = static_cast<std::uint32_t>(items.size());
    item.size = size;
    nodes.push_back(item.node);
    items.push_back(item);
  }

  void depends(unsigned long long card, unsigned long long dependency)
  {
    edges.push_back(Edge{.source = native(card), .target = native(dependency)});
  }

  void assign(unsigned long long card, std::uint32_t person)
  {
    assignees.push_back(Assignee{.node = native(card), .person = person});
    person_count = std::max(person_count, person + 1U);
  }

  void milestone(unsigned long long id, double target_hours = 100.0)
  {
    milestones.push_back(Milestone{.node = native(id), .target_hours = target_hours});
  }

  PlannedBoard planned() const
  {
    return {*this, SimulationOptions{.seed = 1U, .run_count = 10U, .thread_count = 1U}};
  }
};

// The schedule when everything takes its median time.
struct Planned
{
  explicit Planned(const Board& board) : planned(board.planned()), scheduler(planned.model())
  {
    assert(planned.error() == PlanError::none);
    scheduler.run(planned.model().planned_hours());
    schedule = scheduler.schedule();
  }

  const ScheduledCard& card(unsigned long long id) const
  {
    for (const auto& scheduled : schedule.cards)
    {
      if (scheduled.node.low == id)
      {
        return scheduled;
      }
    }

    assert(false && "card not scheduled");
    return schedule.cards.front();
  }

  std::vector<unsigned long long> chain() const
  {
    std::vector<std::size_t> cards;
    std::vector<unsigned long long> ids;
    ids.reserve(planned.model().card_count());
    scheduler.driving_chain(planned.model().milestones().front().card, cards);

    for (const auto index : cards)
    {
      ids.push_back(planned.model().nodes()[index].low);
    }

    return ids;
  }

  PlannedBoard planned;
  Scheduler scheduler;
  Schedule schedule;
};

// Launch (XS) needs the payment API (M) and the store listing (S).
Board launch()
{
  Board board;
  board.card(1U, CardSize::m);
  board.card(2U, CardSize::s);
  board.card(3U, CardSize::xs);
  board.depends(3U, 1U);
  board.depends(3U, 2U);
  board.milestone(3U);
  return board;
}

void schedules_one_card_at_a_time_per_person()
{
  // Alone, Grace does everything in a row: 6 h, 2 h and 0.5 h.
  auto alone = launch();
  alone.assign(1U, 0U);
  alone.assign(2U, 0U);
  const Planned serial(alone);
  assert(lasts(serial.scheduler.finish_of(0U), 8.5));
  assert(serial.chain().size() == 3U && serial.chain().back() == 3U);

  // With Alan on the listing, both start at once and launch follows the API.
  auto pair = launch();
  pair.assign(1U, 0U);
  pair.assign(2U, 1U);
  const Planned parallel(pair);
  assert(parallel.card(2U).person == 1U && lasts(parallel.card(2U).finish_hours, 2.0));
  assert(lasts(parallel.card(3U).start_hours, 6.0));
  assert(lasts(parallel.scheduler.finish_of(0U), 6.5));
  assert((parallel.chain() == std::vector<unsigned long long>{1U, 3U}));
}

void starts_the_longest_chain_first()
{
  // Two short cards come first on the board, but the long chain sets the date.
  Board board;
  board.card(1U, CardSize::s);
  board.card(2U, CardSize::s);
  board.card(3U, CardSize::l);
  board.card(4U, CardSize::l);
  board.card(5U, CardSize::xs);
  board.depends(4U, 3U);

  for (const auto card : {1U, 2U, 4U})
  {
    board.depends(5U, card);
  }

  board.milestone(5U);
  const Planned planned(board);
  assert(lasts(planned.card(3U).start_hours, 0.0));
}

void lets_priority_break_ties()
{
  // Three days each, all on Ada and all as urgent: the important one is first
  // whatever its place on the board, and nobody's queue hides it.
  for (const auto is_assigned : {true, false})
  {
    Board board;
    board.card(1U, CardSize::l);
    board.items.back().priority = 1U;
    board.card(2U, CardSize::l);
    board.items.back().priority = 3U;
    board.card(3U, CardSize::l);
    board.items.back().priority = 2U;
    board.card(4U, CardSize::xs);

    for (const auto card : {1U, 2U, 3U})
    {
      board.depends(4U, card);

      if (is_assigned)
      {
        board.assign(card, 0U);
      }
    }

    board.milestone(4U);
    const Planned planned(board);
    assert(lasts(planned.card(2U).start_hours, 0.0));
    assert(planned.card(3U).start_hours < planned.card(1U).start_hours);
  }
}

void keeps_the_bottleneck_for_its_own_cards()
{
  // Ada has three cards of her own, Grace one; the free card goes to Grace.
  Board board;
  board.card(1U, CardSize::m);
  board.card(2U, CardSize::m);
  board.card(3U, CardSize::m);
  board.card(4U, CardSize::xs);
  board.card(5U, CardSize::l);
  board.card(6U, CardSize::xs);

  for (const auto card : {1U, 2U, 3U})
  {
    board.assign(card, 0U);
  }

  board.assign(4U, 1U);

  for (const auto card : {1U, 2U, 3U, 4U, 5U})
  {
    board.depends(6U, card);
  }

  board.milestone(6U);
  const Planned planned(board);
  assert(planned.card(5U).person == 1U);
}

void continues_what_is_started()
{
  // Nothing needs card 1 as much as the six cards behind card 2, but it is in
  // progress.
  Board board;
  board.card(1U, CardSize::m);
  board.items.back().is_in_progress = true;
  board.card(2U, CardSize::m);
  board.card(100U, CardSize::xs);
  board.depends(100U, 1U);
  board.assign(1U, 0U);
  board.assign(2U, 0U);

  for (unsigned long long id = 3U; id <= 8U; ++id)
  {
    board.card(id, CardSize::m);
    board.depends(id, 2U);
    board.depends(100U, id);
    board.assign(id, 0U);
  }

  board.milestone(100U);
  const Planned planned(board);
  assert(lasts(planned.card(1U).start_hours, 0.0));
  assert(planned.card(2U).start_hours == planned.card(1U).finish_hours);
}

void counts_what_is_left_of_a_started_card()
{
  Board board;
  board.card(1U, CardSize::m);
  board.items.back().is_in_progress = true;
  board.items.back().elapsed_hours = 4.0;
  board.milestone(1U);
  const auto left = Planned(board).scheduler.finish_of(0U);
  assert(left > 0.0 && left < 6.0);

  // Long past its range, what is left grows with what it already took.
  board.items.back().elapsed_hours = 60.0;
  assert(Planned(board).scheduler.finish_of(0U) > left);
}

void waits_for_the_work_in_progress_outside_the_scopes()
{
  auto board = launch();
  board.card(9U, CardSize::l);
  board.items.back().is_in_progress = true;
  board.assign(1U, 0U);
  board.assign(2U, 0U);
  board.assign(9U, 0U);
  const Planned planned(board);
  // Ada finishes the card she has started first: 18 h, then 8.5 h.
  assert(lasts(planned.scheduler.finish_of(0U), 26.5));

  // A card in progress that waits for another keeps nobody busy.
  board.card(10U, CardSize::s);
  board.depends(9U, 10U);
  assert(lasts(Planned(board).scheduler.finish_of(0U), 8.5));
}

void shares_the_team_between_milestones()
{
  // Two independent milestones of 6 h each, due in that order; one person.
  Board board;
  board.card(1U, CardSize::m);
  board.card(2U, CardSize::m);
  board.assign(1U, 0U);
  board.assign(2U, 0U);
  board.milestone(1U, 50.0);
  board.milestone(2U, 10.0);
  const Planned planned(board);
  assert(lasts(planned.scheduler.finish_of(1U), 6.0));
  assert(lasts(planned.scheduler.finish_of(0U), 12.0));
}

void scopes_a_milestone_to_its_open_dependencies()
{
  // 1 depends on 2 (done), which depends on 3: nobody waits for 3 any more.
  Board board;
  board.card(1U, CardSize::s);
  board.card(2U, CardSize::xl, true);
  board.card(3U, CardSize::none);
  board.card(4U, CardSize::xxl);
  board.depends(1U, 2U);
  board.depends(2U, 3U);
  board.milestone(1U);
  const Planned planned(board);
  assert(planned.planned.model().card_count() == 1U);
  assert(lasts(planned.scheduler.finish_of(0U), 2.0));

  // A done milestone is done.
  auto done = board;
  done.items.front().is_completed = true;
  assert(lasts(Planned(done).scheduler.finish_of(0U), 0.0));
}

void uses_one_person_nobody_in_particular_without_assignees()
{
  const Planned planned(launch());
  assert(planned.card(1U).person == planned.planned.model().nobody());
  assert(lasts(planned.scheduler.finish_of(0U), 8.5));
}

void rejects_boards_it_cannot_plan()
{
  const auto board = launch();
  assert(board.planned().error() == PlanError::none);

  auto unknown = board;
  unknown.milestone(99U);
  assert(unknown.planned().error() == PlanError::unknown_milestone);

  auto outsider = board;
  outsider.assign(1U, 3U);
  outsider.person_count = 1U;
  assert(outsider.planned().error() == PlanError::unknown_person);

  auto infinite = board;
  infinite.milestones.front().target_hours = std::numeric_limits<double>::infinity();
  assert(infinite.planned().error() == PlanError::bad_number);

  auto weightless = board;
  weightless.milestones.front().weight = 0.0;
  assert(weightless.planned().error() == PlanError::bad_number);

  auto negative = board;
  negative.items.front().elapsed_hours = -1.0;
  assert(negative.planned().error() == PlanError::bad_number);

  // A loop through open cards blocks them all; through a done card it does not.
  auto loop = board;
  loop.depends(1U, 3U);
  assert(loop.planned().error() == PlanError::cycle);

  auto done_loop = loop;
  done_loop.items.front().is_completed = true;
  assert(done_loop.planned().error() == PlanError::none);
}

void finds_the_smallest_ready_card_across_words()
{
  ReadySet set(9000U);
  assert(set.empty() && set.min() == ReadySet::none);

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
  set.clear();
  assert(set.empty() && set.min() == ReadySet::none);
  set.insert(1U);
  assert(set.min() == 1U);
}
}

int main()
{
  schedules_one_card_at_a_time_per_person();
  starts_the_longest_chain_first();
  lets_priority_break_ties();
  keeps_the_bottleneck_for_its_own_cards();
  continues_what_is_started();
  counts_what_is_left_of_a_started_card();
  waits_for_the_work_in_progress_outside_the_scopes();
  shares_the_team_between_milestones();
  scopes_a_milestone_to_its_open_dependencies();
  uses_one_person_nobody_in_particular_without_assignees();
  rejects_boards_it_cannot_plan();
  finds_the_smallest_ready_card_across_words();

  return 0;
}
