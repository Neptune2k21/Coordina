#include "plan_board.h"

#include <cassert>
#include <chrono>
#include <cstddef>
#include <cstdint>

namespace
{
using namespace coordina::graph::fixtures;

constexpr std::uint32_t low = 1U;
constexpr std::uint32_t medium = 2U;
constexpr std::uint32_t high = 3U;

// Worked on by one person nobody in particular, so a scope ends after the sum
// of its durations.
PlanBoard empty_board(double target_hours)
{
  PlanBoard board;
  board.card(milestone_id, COORDINA_GRAPH_SIZE_XS, medium);
  board.milestone(milestone_id, target_hours);
  return board;
}

// The milestone needs two large cards and a small one; cutting either large
// card is enough, and the low-priority one gives up a quarter of the value.
PlanBoard two_features(double target_hours)
{
  auto board = empty_board(target_hours);
  board.card(1U, COORDINA_GRAPH_SIZE_L, low);
  board.card(2U, COORDINA_GRAPH_SIZE_L, high);
  board.card(3U, COORDINA_GRAPH_SIZE_S, medium);
  board.depends(milestone_id, 1U);
  board.depends(milestone_id, 2U);
  board.depends(milestone_id, 3U);
  return board;
}

// Feature 1 needs card 2, which nothing else needs, and card 4, which feature
// 3 needs too.
PlanBoard shared_work(double target_hours)
{
  auto board = empty_board(target_hours);
  board.card(1U, COORDINA_GRAPH_SIZE_S, low);
  board.card(2U, COORDINA_GRAPH_SIZE_L, low);
  board.card(3U, COORDINA_GRAPH_SIZE_S, high);
  board.card(4U, COORDINA_GRAPH_SIZE_M, medium);
  board.depends(milestone_id, 1U);
  board.depends(milestone_id, 3U);
  board.depends(1U, 2U);
  board.depends(1U, 4U);
  board.depends(3U, 4U);
  return board;
}

void cuts_the_least_valuable_card_that_makes_the_date()
{
  // About 20 hours are left once a large card is out, 38 with it.
  const auto board = two_features(36.0);
  const auto result = board.propose_scope(board.scope_options());

  assert(result.status == COORDINA_GRAPH_OK);
  assert(result.cuts == Ids{1U});
  assert(result.dropped == Ids{1U});
  assert(result.output.on_time_before < 0.85);
  assert(result.output.on_time_after >= 0.85);
  assert(result.output.p85_days <= 36.0 / 6.0);
}

void weighs_cards_by_priority_rather_than_size()
{
  // Nine cards of the same priority, three of each size. Cutting one large
  // card frees as much time as cutting many small ones, and gives up less.
  auto board = empty_board(55.0);

  for (unsigned long long id = 1U; id <= 9U; ++id)
  {
    const std::uint32_t sizes[] =
      {COORDINA_GRAPH_SIZE_S, COORDINA_GRAPH_SIZE_M, COORDINA_GRAPH_SIZE_L};
    board.card(id, sizes[id % 3U], medium);
    board.depends(milestone_id, id);
  }

  const auto result = board.propose_scope(board.scope_options());
  assert(!result.cuts.empty() && result.cuts.size() <= 3U);

  for (const auto cut : result.cuts)
  {
    assert(board.item(cut).size == COORDINA_GRAPH_SIZE_L);
  }
}

void takes_out_what_only_the_cut_needs_and_keeps_shared_work()
{
  const auto board = shared_work(20.0);
  const auto result = board.propose_scope(board.scope_options());

  // Card 2 leaves with feature 1 either way; the proposal names the feature.
  assert(result.status == COORDINA_GRAPH_OK);
  assert(result.cuts == Ids{1U});
  assert(result.dropped == (Ids{1U, 2U}));
  assert(result.dropped_by == (std::vector<std::uint32_t>{0U, 0U}));

  // What it announces is the forecast without the dependency on feature 1.
  const auto chosen = board.evaluate_scope({1U});
  const auto applied = board.without({1U}).forecast().first();
  assert(chosen.dropped == (Ids{1U, 2U}));
  assert(chosen.output.p50_days == applied.p50_days);
  assert(chosen.output.p85_days == applied.p85_days);
  assert(chosen.output.on_time_after == applied.on_time);
  assert(result.output.p85_days == applied.p85_days);
}

void cutting_a_dependency_takes_out_the_cards_waiting_for_it()
{
  const auto result = shared_work(20.0).evaluate_scope({4U});

  assert(result.cuts == Ids{4U});
  assert(result.dropped == (Ids{1U, 2U, 3U, 4U}));
  assert(result.output.on_time_after > 0.95);
}

void keeps_the_work_another_milestone_needs()
{
  // Card 1 leaves the first milestone, but the second one still needs it.
  auto board = two_features(36.0);
  board.card(2000U, COORDINA_GRAPH_SIZE_XS);
  board.depends(2000U, 1U);
  board.milestone(2000U, 200.0);
  const auto result = board.evaluate_scope({1U});
  const auto applied = board.without({1U}).forecast();

  assert(result.dropped == Ids{1U});
  assert(result.output.p85_days == applied.first().p85_days);
  // It is done after the first milestone, which it no longer holds back.
  assert(applied.milestones[1U].p50_days > applied.first().p50_days);
  assert(applied.first().p50_days < board.forecast().first().p50_days);
}

void never_proposes_kept_cards_or_work_in_progress()
{
  auto board = two_features(36.0);
  const auto kept = board.propose_scope(board.scope_options(), {1U});
  assert(kept.cuts == Ids{2U});

  board.item(1U).is_in_progress = 1U;
  const auto started = board.propose_scope(board.scope_options());
  assert(started.cuts == Ids{2U});
}

void never_cuts_what_a_kept_card_needs()
{
  // Keeping feature 1 keeps cards 2 and 4 too. Feature 3 is the only card
  // left to cut, and more than 26 hours remain without it.
  const auto board = shared_work(6.0);
  const auto result = board.propose_scope(board.scope_options(), {1U});

  assert(result.status == COORDINA_GRAPH_OK);
  assert(result.cuts.empty() && result.dropped.empty());
  assert(result.output.on_time_after == result.output.on_time_before);
}

void proposes_nothing_when_the_milestone_is_on_track()
{
  const auto board = two_features(400.0);
  const auto result = board.propose_scope(board.scope_options());

  assert(result.cuts.empty());
  assert(result.output.on_time_before > 0.99);
  assert(result.output.on_time_after == result.output.on_time_before);
}

void measures_a_chosen_cut_and_ignores_the_milestone()
{
  const auto board = two_features(36.0);
  const auto result = board.evaluate_scope({2U, milestone_id, 9999U});
  assert(result.cuts == Ids{2U});
  assert(result.output.on_time_after > result.output.on_time_before);

  const auto none = board.evaluate_scope({});
  assert(none.cuts.empty());
  assert(none.output.on_time_after == none.output.on_time_before);

  // A done milestone has nothing left to cut.
  auto done = board;
  done.item(milestone_id).is_completed = 1U;
  const auto nothing = done.evaluate_scope({1U});
  assert(nothing.status == COORDINA_GRAPH_OK && nothing.cuts.empty());
}

// Features of 4 chained cards that the milestone needs, shared by three people.
PlanBoard wide_board(double target_hours)
{
  auto board = empty_board(target_hours);
  board.people = 3U;
  const std::uint32_t sizes[] =
    {COORDINA_GRAPH_SIZE_S, COORDINA_GRAPH_SIZE_M, COORDINA_GRAPH_SIZE_L, COORDINA_GRAPH_SIZE_XS};

  for (unsigned long long id = 1U; id <= 320U; ++id)
  {
    board.card(id, sizes[id % 4U], static_cast<std::uint32_t>(id % 4U));

    if (id <= 3U)
    {
      board.assign(id, static_cast<std::uint32_t>(id - 1U));
    }

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

void gives_the_same_proposal_with_any_thread_count()
{
  const auto board = wide_board(600.0);
  auto options = board.scope_options();
  const auto one = board.propose_scope(options);
  options.simulation.thread_count = 4U;
  const auto four = board.propose_scope(options);

  assert(!one.cuts.empty());
  assert(one.cuts == four.cuts && one.dropped == four.dropped);
  assert(one.output.on_time_after == four.output.on_time_after);
  assert(one.output.p85_days == four.output.p85_days);
  assert(one.output.on_time_after > one.output.on_time_before);
}

void stops_at_the_evaluation_and_time_limits()
{
  const auto board = wide_board(500.0);
  auto options = board.scope_options();
  // The first measure and one batch of cuts.
  options.evaluation_limit = 2U;
  const auto first = board.propose_scope(options);
  assert(first.status == COORDINA_GRAPH_OK && !first.cuts.empty());

  options.evaluation_limit = 100000U;
  options.time_limit_ms = 50U;
  const auto started = std::chrono::steady_clock::now();
  const auto timed = board.propose_scope(options);
  const auto elapsed = std::chrono::steady_clock::now() - started;

  // The limit stops the search; forecasting the scope before and after adds
  // the time of two forecasts.
  assert(timed.status == COORDINA_GRAPH_OK);
  assert(elapsed < std::chrono::seconds(4));
}

void rejects_bad_arguments_and_reports_missing_capacity()
{
  const auto board = two_features(36.0);
  auto options = board.scope_options();
  options.goal = 0.0;
  assert(board.propose_scope(options).status == COORDINA_GRAPH_INVALID_ARGUMENT);
  options.goal = 1.5;
  assert(board.propose_scope(options).status == COORDINA_GRAPH_INVALID_ARGUMENT);
  options = board.scope_options();
  options.screen_run_count = 0U;
  assert(board.propose_scope(options).status == COORDINA_GRAPH_INVALID_ARGUMENT);
  options = board.scope_options();
  options.milestone = node(9999U);
  assert(board.propose_scope(options).status == COORDINA_GRAPH_INVALID_ARGUMENT);
  assert(last_error_says("milestone"));

  // A card that is not one of the board's milestones.
  options.milestone = node(1U);
  assert(board.propose_scope(options).status == COORDINA_GRAPH_INVALID_ARGUMENT);

  options = board.scope_options();
  options.screen_run_count = 4000000000U;
  assert(board.propose_scope(options).status == COORDINA_GRAPH_INVALID_ARGUMENT);
  assert(last_error_says("screen_run_count"));

  options = board.scope_options();
  const auto view = board.view();
  coordina_graph_scope_cut output{};
  assert(
    coordina_graph_propose_scope(&view, nullptr, 1U, &options, &output)
    == COORDINA_GRAPH_INVALID_ARGUMENT);

  auto unscreened = options;
  unscreened.screen_run_count = 0U;
  assert(
    coordina_graph_evaluate_scope(&view, nullptr, 0U, &unscreened, &output)
    == COORDINA_GRAPH_INVALID_ARGUMENT);

  // Without buffers the call says how many cards it would write.
  assert(
    coordina_graph_propose_scope(&view, nullptr, 0U, &options, &output)
    == COORDINA_GRAPH_INSUFFICIENT_CAPACITY);
  assert(output.cut_count == 1U && output.dropped_count == 1U);
}
}

int main()
{
  cuts_the_least_valuable_card_that_makes_the_date();
  weighs_cards_by_priority_rather_than_size();
  takes_out_what_only_the_cut_needs_and_keeps_shared_work();
  cutting_a_dependency_takes_out_the_cards_waiting_for_it();
  keeps_the_work_another_milestone_needs();
  never_proposes_kept_cards_or_work_in_progress();
  never_cuts_what_a_kept_card_needs();
  proposes_nothing_when_the_milestone_is_on_track();
  measures_a_chosen_cut_and_ignores_the_milestone();
  gives_the_same_proposal_with_any_thread_count();
  stops_at_the_evaluation_and_time_limits();
  rejects_bad_arguments_and_reports_missing_capacity();

  return 0;
}
