#include "plan_board.h"

#include <algorithm>
#include <cassert>
#include <chrono>
#include <cstddef>
#include <cstdint>
#include <set>
#include <utility>
#include <vector>

namespace
{
using namespace coordina::graph::fixtures;

constexpr std::uint32_t ada = 0U;
constexpr std::uint32_t grace = 1U;
constexpr std::uint32_t linus = 2U;

std::set<std::uint32_t> people_of(const Changes& changes)
{
  std::set<std::uint32_t> people;

  for (const auto& change : changes)
  {
    people.insert(change.second);
  }

  return people;
}

PlanBoard empty_board(std::uint32_t people)
{
  PlanBoard board;
  board.people = people;
  board.card(milestone_id, COORDINA_GRAPH_SIZE_XS);
  board.milestone(milestone_id, 120.0);
  return board;
}

// Four independent days of work that nobody is assigned to, so one person
// nobody in particular does them all.
PlanBoard unstaffed()
{
  auto board = empty_board(2U);
  board.receivers = {ada, grace};

  for (unsigned long long id = 1U; id <= 4U; ++id)
  {
    board.card(id, COORDINA_GRAPH_SIZE_M);
    board.depends(milestone_id, id);
  }

  return board;
}

// Ada has a three-day chain to the milestone and a day of side work, which
// she picks up between the two halves of the chain. Grace has a small card
// and time to spare.
PlanBoard overloaded()
{
  auto board = empty_board(2U);
  board.receivers = {ada, grace};
  board.card(1U, COORDINA_GRAPH_SIZE_L);
  board.card(2U, COORDINA_GRAPH_SIZE_L);
  board.card(3U, COORDINA_GRAPH_SIZE_M);
  board.card(4U, COORDINA_GRAPH_SIZE_S);
  board.depends(2U, 1U);
  board.depends(milestone_id, 2U);
  board.depends(milestone_id, 3U);
  board.depends(milestone_id, 4U);
  board.assign(1U, ada);
  board.assign(2U, ada);
  board.assign(3U, ada);
  board.assign(4U, grace);
  return board;
}

void spreads_unassigned_work_over_the_receivers()
{
  const auto board = unstaffed();
  const auto result = board.propose_staffing(board.staffing_options());

  // One card each brings both into the team, and they share the rest.
  assert(result.status == COORDINA_GRAPH_OK);
  assert(result.changes.size() == 2U);
  assert((people_of(result.changes) == std::set<std::uint32_t>{ada, grace}));
  assert(result.output.p85_days_after < result.output.p85_days_before * 0.7);
}

void takes_work_off_the_critical_person()
{
  const auto board = overloaded();
  const auto result = board.propose_staffing(board.staffing_options(true));
  // Grace takes the side work, or the second half of the chain while Ada does
  // the side work: either way the chain no longer waits.
  const auto by_hand = board.evaluate_staffing({{3U, grace}});

  assert(result.status == COORDINA_GRAPH_OK);
  assert(result.changes.size() == 1U && result.changes.front().second == grace);
  assert(result.output.p50_days_after <= by_hand.output.p50_days_after * 1.02);
  assert(result.output.p85_days_after <= by_hand.output.p85_days_after * 1.02);
  assert(result.output.p85_days_after < result.output.p85_days_before);
  assert(result.details.front().start_days_after < result.details.front().start_days_before);
}

void keeps_assignees_unless_allowed_and_never_moves_work_in_progress()
{
  const auto kept = overloaded();
  assert(kept.propose_staffing(kept.staffing_options()).changes.empty());

  // Ada has started all her cards.
  auto started = overloaded();

  for (unsigned long long card = 1U; card <= 3U; ++card)
  {
    started.item(card).is_in_progress = 1U;
  }

  for (const auto& [card, person] :
    started.propose_staffing(started.staffing_options(true)).changes)
  {
    assert(card > 3U);
  }
}

void gives_cards_only_to_receivers()
{
  auto board = unstaffed();
  board.people = 3U;
  board.receivers = {linus};
  const auto result = board.propose_staffing(board.staffing_options());

  // Linus alone does the work nobody in particular did: no date moves.
  assert(result.changes.empty());

  board.receivers = {grace, linus};
  const auto two = board.propose_staffing(board.staffing_options());
  assert((people_of(two.changes) == std::set<std::uint32_t>{grace, linus}));
}

void prefers_someone_who_has_done_cards_like_it()
{
  auto board = overloaded();
  board.people = 3U;
  board.receivers = {ada, grace, linus};
  board.skills.push_back(coordina_graph_assignee{node(2U), linus, 0U});
  board.skills.push_back(coordina_graph_assignee{node(3U), linus, 0U});
  const auto result = board.propose_staffing(board.staffing_options(true));

  // Grace and Linus would ship as soon; Linus has done cards like these.
  assert(result.changes.size() == 1U && result.changes.front().second == linus);
}

void dates_after_are_the_forecast_once_applied()
{
  for (const auto& board : {unstaffed(), overloaded()})
  {
    const auto proposal = board.propose_staffing(board.staffing_options(true));
    assert(!proposal.changes.empty());

    const auto before = board.forecast().first();
    const auto after = board.with(proposal.changes).forecast().first();
    assert(proposal.output.p50_days_before == before.p50_days);
    assert(proposal.output.p85_days_before == before.p85_days);
    assert(proposal.output.p50_days_after == after.p50_days);
    assert(proposal.output.p85_days_after == after.p85_days);

    const auto measured = board.evaluate_staffing(proposal.changes);
    assert(measured.changes == proposal.changes);
    assert(measured.output.p85_days_after == after.p85_days);
  }
}

void measures_chosen_changes_and_ignores_those_that_change_nothing()
{
  const auto board = overloaded();
  const auto result = board.evaluate_staffing({{3U, grace}, {1U, ada}, {9999U, grace}});

  assert(result.status == COORDINA_GRAPH_OK);
  assert(result.changes == (Changes{{3U, grace}}));
  assert(result.output.p85_days_after < result.output.p85_days_before);

  const auto none = board.evaluate_staffing({});
  assert(none.changes.empty());
  assert(none.output.p85_days_after == none.output.p85_days_before);
}

// Six cards all on Ada, three of three days and three of one: the best split
// over three people gives each a long card and a short one, four days.
PlanBoard balanced_by_hand()
{
  auto board = empty_board(3U);
  board.receivers = {ada, grace, linus};

  for (unsigned long long id = 1U; id <= 6U; ++id)
  {
    board.card(id, id <= 3U ? COORDINA_GRAPH_SIZE_L : COORDINA_GRAPH_SIZE_M);
    board.depends(milestone_id, id);
    board.assign(id, ada);
  }

  return board;
}

void comes_within_ten_percent_of_the_best_split()
{
  const auto board = balanced_by_hand();
  const auto result = board.propose_staffing(board.staffing_options(true));
  const auto best = board.evaluate_staffing({{2U, grace}, {5U, grace}, {3U, linus}, {6U, linus}});

  assert(result.status == COORDINA_GRAPH_OK);
  assert(result.output.p85_days_after <= best.output.p85_days_after * 1.1);
  assert(result.output.p50_days_after <= best.output.p50_days_after * 1.1);
}

void spares_the_other_milestones()
{
  // Ada and Grace each have a milestone; Ada's is the one to plan, and
  // Grace's is due the day her work ends.
  auto board = empty_board(2U);
  board.receivers = {ada, grace};

  for (unsigned long long id = 1U; id <= 6U; ++id)
  {
    board.card(id, COORDINA_GRAPH_SIZE_M);
    board.depends(id <= 4U ? milestone_id : 2000U, id);
    board.assign(id, id <= 4U ? ada : grace);
  }

  board.card(2000U, COORDINA_GRAPH_SIZE_XS);
  board.assign(2000U, grace);
  // Being late there costs twenty times as much.
  board.milestone(2000U, 13.0, 20.0);
  const auto kept = board.propose_staffing(board.staffing_options(true));
  assert(kept.status == COORDINA_GRAPH_OK);
  assert(
    kept.output.others_late_days_after <= kept.output.others_late_days_before
      + kept.output.p85_days_before - kept.output.p85_days_after + 1e-9);

  // With time to spare there, Grace helps.
  board.milestones.back().target_hours = 400.0;
  const auto helped = board.propose_staffing(board.staffing_options(true));
  assert(!helped.changes.empty());
  assert(helped.output.p85_days_after < helped.output.p85_days_before * 0.8);
}

// Forty chains of four cards that the milestone needs, all on two of five
// people, with three receivers free.
PlanBoard wide_board()
{
  auto board = empty_board(5U);
  board.receivers = {0U, 1U, 2U, 3U, 4U};
  const std::uint32_t sizes[] =
    {COORDINA_GRAPH_SIZE_S, COORDINA_GRAPH_SIZE_M, COORDINA_GRAPH_SIZE_L, COORDINA_GRAPH_SIZE_XS};

  for (unsigned long long id = 1U; id <= 160U; ++id)
  {
    board.card(id, sizes[id % 4U]);
    board.assign(id, static_cast<std::uint32_t>(id % 2U));

    if (id % 4U == 1U)
    {
      board.depends(milestone_id, id);
    }
    else
    {
      board.depends(id - 1U, id);
    }
  }

  return board;
}

void gives_the_same_plan_with_any_thread_count()
{
  const auto board = wide_board();
  auto options = board.staffing_options(true);
  const auto one = board.propose_staffing(options);
  options.simulation.thread_count = 4U;
  const auto four = board.propose_staffing(options);

  assert(!one.changes.empty());
  assert(one.changes == four.changes);
  assert(one.output.p85_days_after == four.output.p85_days_after);
  assert(one.output.p85_days_after < one.output.p85_days_before);
}

void stops_at_the_evaluation_and_time_limits()
{
  const auto board = wide_board();
  auto options = board.staffing_options(true);
  options.evaluation_limit = 4U;
  assert(board.propose_staffing(options).status == COORDINA_GRAPH_OK);

  options.evaluation_limit = 100000000U;
  options.time_limit_ms = 50U;
  const auto started = std::chrono::steady_clock::now();
  const auto timed = board.propose_staffing(options);
  const auto elapsed = std::chrono::steady_clock::now() - started;

  assert(timed.status == COORDINA_GRAPH_OK);
  assert(elapsed < std::chrono::seconds(2));
}

void rejects_bad_arguments_and_reports_missing_capacity()
{
  auto board = unstaffed();
  auto options = board.staffing_options();
  options.simulation.run_count = 0U;
  assert(board.propose_staffing(options).status == COORDINA_GRAPH_INVALID_ARGUMENT);
  options = board.staffing_options();
  options.screen_run_count = 0U;
  assert(board.propose_staffing(options).status == COORDINA_GRAPH_INVALID_ARGUMENT);
  options = board.staffing_options();
  options.milestone = node(9999U);
  assert(board.propose_staffing(options).status == COORDINA_GRAPH_INVALID_ARGUMENT);
  options = board.staffing_options();
  assert(board.evaluate_staffing({{1U, 2U}}).status == COORDINA_GRAPH_INVALID_ARGUMENT);

  auto outsider = board;
  outsider.receivers = {2U};
  assert(outsider.propose_staffing(options).status == COORDINA_GRAPH_INVALID_ARGUMENT);
  outsider = board;
  outsider.assign(1U, 2U);
  assert(outsider.propose_staffing(options).status == COORDINA_GRAPH_INVALID_ARGUMENT);
  outsider = board;
  outsider.skills.push_back(coordina_graph_assignee{node(1U), 2U, 0U});
  assert(outsider.propose_staffing(options).status == COORDINA_GRAPH_INVALID_ARGUMENT);

  const auto view = board.view();
  coordina_graph_staffing output{};
  assert(
    coordina_graph_propose_staffing(&view, nullptr, 1U, nullptr, 0U, &options, &output)
    == COORDINA_GRAPH_INVALID_ARGUMENT);

  // Without a buffer the call says how many changes it would write.
  assert(
    coordina_graph_propose_staffing(
      &view,
      board.receivers.data(),
      board.receivers.size(),
      nullptr,
      0U,
      &options,
      &output)
    == COORDINA_GRAPH_INSUFFICIENT_CAPACITY);
  assert(output.change_count == 2U);
}
}

int main()
{
  spreads_unassigned_work_over_the_receivers();
  takes_work_off_the_critical_person();
  keeps_assignees_unless_allowed_and_never_moves_work_in_progress();
  gives_cards_only_to_receivers();
  prefers_someone_who_has_done_cards_like_it();
  dates_after_are_the_forecast_once_applied();
  measures_chosen_changes_and_ignores_those_that_change_nothing();
  comes_within_ten_percent_of_the_best_split();
  spares_the_other_milestones();
  gives_the_same_plan_with_any_thread_count();
  stops_at_the_evaluation_and_time_limits();
  rejects_bad_arguments_and_reports_missing_capacity();

  return 0;
}
