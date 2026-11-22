#include "abi_fixtures.h"
#include "coordina_graph_engine.h"

#include <algorithm>
#include <cassert>
#include <chrono>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <vector>

namespace
{
using namespace coordina::graph::fixtures;

constexpr unsigned long long milestone_id = 1000U;
constexpr std::uint32_t low = 1U;
constexpr std::uint32_t medium = 2U;
constexpr std::uint32_t high = 3U;

struct ScopeResult
{
  coordina_graph_status status = COORDINA_GRAPH_OK;
  Ids cuts;
  Ids dropped;
  std::vector<std::uint32_t> dropped_by;
  coordina_graph_scope_cut output{};
};

// A board with one milestone, worked on by one person unless assignees say
// otherwise, so a scope finishes after the sum of its durations.
struct ScopeBoard
{
  ScopeBoard()
  {
    card(milestone_id, COORDINA_GRAPH_SIZE_XS, medium);
  }

  void card(unsigned long long id, std::uint32_t size, std::uint32_t priority)
  {
    nodes.push_back(node(id));
    auto item = sized_item(id, size, static_cast<std::uint32_t>(items.size()));
    item.priority = priority;
    items.push_back(item);
  }

  void depends(unsigned long long card, unsigned long long dependency)
  {
    edges.push_back(coordina_graph_edge{node(card), node(dependency)});
  }

  coordina_graph_scope_options options(double target_hours) const
  {
    return coordina_graph_scope_options{
      .milestone = node(milestone_id),
      .seed = 131U,
      .target_hours = target_hours,
      .goal = 0.85,
      .person_count = people,
      .run_count = 2000U,
      .screen_run_count = 256U,
      .thread_count = 1U,
      .evaluation_limit = 200U,
      .time_limit_ms = 0U
    };
  }

  ScopeResult propose(const coordina_graph_scope_options& scope, const Ids& keep = {}) const
  {
    std::vector<coordina_graph_node_id> kept;

    for (const auto id : keep)
    {
      kept.push_back(node(id));
    }

    return solve(
      [&](coordina_graph_scope_cut* output)
      {
        return coordina_graph_propose_scope(
          nodes.data(),
          nodes.size(),
          edges.data(),
          edges.size(),
          items.data(),
          items.size(),
          assignees.data(),
          assignees.size(),
          kept.data(),
          kept.size(),
          &scope,
          output);
      });
  }

  ScopeResult evaluate(const coordina_graph_scope_options& scope, const Ids& cuts) const
  {
    std::vector<coordina_graph_node_id> chosen;

    for (const auto id : cuts)
    {
      chosen.push_back(node(id));
    }

    return solve(
      [&](coordina_graph_scope_cut* output)
      {
        return coordina_graph_evaluate_scope(
          nodes.data(),
          nodes.size(),
          edges.data(),
          edges.size(),
          items.data(),
          items.size(),
          assignees.data(),
          assignees.size(),
          chosen.data(),
          chosen.size(),
          &scope,
          output);
      });
  }

  double p50_days_without(unsigned long long dependency) const
  {
    auto kept_edges = edges;
    std::erase_if(
      kept_edges,
      [&](const coordina_graph_edge& edge)
      {
        return edge.source.low == milestone_id && edge.target.low == dependency;
      });
    std::vector<coordina_graph_card_forecast> cards(nodes.size());
    coordina_graph_milestone_forecast output{};
    output.cards = cards.data();
    output.card_capacity = cards.size();
    const coordina_graph_forecast_options forecast{node(milestone_id), 131U, people, 2000U, 1U, 0U};
    const auto status = coordina_graph_forecast(
      nodes.data(),
      nodes.size(),
      kept_edges.data(),
      kept_edges.size(),
      items.data(),
      items.size(),
      assignees.data(),
      assignees.size(),
      &forecast,
      &output);
    assert(status == COORDINA_GRAPH_OK);
    return output.p50_days;
  }

  std::vector<coordina_graph_node_id> nodes;
  std::vector<coordina_graph_edge> edges;
  std::vector<coordina_graph_work_item> items;
  std::vector<coordina_graph_assignee> assignees;
  std::uint32_t people = 1U;

private:
  template <typename Call>
  ScopeResult solve(const Call& call) const
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
};

// The milestone needs two large cards and a small one; cutting either large
// card is enough, and the low-priority one gives up a quarter of the value.
ScopeBoard two_features()
{
  ScopeBoard board;
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
ScopeBoard shared_work()
{
  ScopeBoard board;
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
  // At most 24 + 3 + 1 hours are left once a large card is out.
  const auto board = two_features();
  const auto result = board.propose(board.options(30.0));

  assert(result.status == COORDINA_GRAPH_OK);
  assert(result.cuts == Ids{1U});
  assert(result.dropped == Ids{1U});
  assert(result.output.on_time_before < 0.85);
  assert(result.output.on_time_after == 1.0);
  assert(result.output.p85_days <= 30.0 / 6.0);
}

void takes_out_what_only_the_cut_needs_and_keeps_shared_work()
{
  // Without feature 1 and card 2, at most 3 + 12 + 1 hours are left.
  const auto board = shared_work();
  const auto result = board.propose(board.options(17.0));

  // Card 2 leaves with feature 1 either way; the proposal names the feature.
  assert(result.status == COORDINA_GRAPH_OK);
  assert(result.cuts == Ids{1U});
  assert(result.dropped == (Ids{1U, 2U}));
  assert(result.dropped_by == (std::vector<std::uint32_t>{0U, 0U}));
  assert(result.output.on_time_after == 1.0);

  // The scope left is the one the milestone has without its dependency on
  // feature 1: one person works through it, so the date is the same sum, up
  // to the order the hours add up in.
  const auto chosen = board.evaluate(board.options(17.0), {1U});
  assert(chosen.dropped == (Ids{1U, 2U}));
  assert(std::abs(chosen.output.p50_days - board.p50_days_without(1U)) < 1e-9);
}

void cutting_a_dependency_takes_out_the_cards_waiting_for_it()
{
  const auto board = shared_work();
  const auto result = board.evaluate(board.options(17.0), {4U});

  assert(result.cuts == Ids{4U});
  assert(result.dropped == (Ids{1U, 2U, 3U, 4U}));
  assert(result.output.on_time_after == 1.0);
}

void never_proposes_kept_cards_or_work_in_progress()
{
  auto board = two_features();
  const auto kept = board.propose(board.options(30.0), {1U});

  assert(kept.cuts == Ids{2U});
  assert(kept.output.on_time_after == 1.0);

  board.items[1U].is_in_progress = 1U;
  const auto started = board.propose(board.options(30.0));

  assert(started.cuts == Ids{2U});
}

void never_cuts_what_a_kept_card_needs()
{
  // Keeping feature 1 keeps cards 2 and 4 too. Feature 3 is the only card left
  // to cut, and without it at least 16.25 hours remain, so nothing is worth
  // proposing.
  const auto board = shared_work();
  const auto result = board.propose(board.options(15.0), {1U});

  assert(result.status == COORDINA_GRAPH_OK);
  assert(result.cuts.empty());
  assert(result.dropped.empty());
  assert(result.output.on_time_after == result.output.on_time_before);
}

void proposes_nothing_when_the_milestone_is_on_track()
{
  const auto board = two_features();
  const auto result = board.propose(board.options(100.0));

  assert(result.cuts.empty());
  assert(result.output.on_time_before == 1.0);
  assert(result.output.on_time_after == 1.0);
}

void measures_a_chosen_cut_and_ignores_the_milestone()
{
  const auto board = two_features();
  const auto result = board.evaluate(board.options(30.0), {2U, milestone_id, 9999U});

  assert(result.cuts == Ids{2U});
  assert(result.dropped == Ids{2U});
  assert(result.output.on_time_after == 1.0);

  const auto none = board.evaluate(board.options(30.0), {});
  assert(none.cuts.empty());
  assert(none.output.on_time_after == none.output.on_time_before);
}

// Releases of 40 cards, each a chain of 4 that the milestone needs, shared by
// three people.
ScopeBoard wide_board()
{
  ScopeBoard board;
  board.people = 3U;
  const std::uint32_t sizes[] =
    {COORDINA_GRAPH_SIZE_S, COORDINA_GRAPH_SIZE_M, COORDINA_GRAPH_SIZE_L, COORDINA_GRAPH_SIZE_XS};

  for (unsigned long long id = 1U; id <= 320U; ++id)
  {
    board.card(id, sizes[id % 4U], static_cast<std::uint32_t>(id % 4U));

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
  const auto board = wide_board();
  auto options = board.options(400.0);
  const auto one = board.propose(options);
  options.thread_count = 4U;
  const auto four = board.propose(options);

  assert(!one.cuts.empty());
  assert(one.cuts == four.cuts && one.dropped == four.dropped);
  assert(one.output.on_time_after == four.output.on_time_after);
  assert(one.output.p85_days == four.output.p85_days);
  assert(one.output.on_time_after > one.output.on_time_before);
}

void stops_at_the_evaluation_and_time_limits()
{
  const auto board = wide_board();
  auto options = board.options(200.0);
  // The first measure and one batch of cuts.
  options.evaluation_limit = 2U;
  const auto first = board.propose(options);

  assert(first.status == COORDINA_GRAPH_OK);
  assert(!first.cuts.empty());

  options.evaluation_limit = 100000U;
  options.time_limit_ms = 50U;
  const auto started = std::chrono::steady_clock::now();
  const auto timed = board.propose(options);
  const auto elapsed = std::chrono::steady_clock::now() - started;

  assert(timed.status == COORDINA_GRAPH_OK);
  // The limit stops the search; measuring the scope before and after adds the
  // time of two forecasts.
  assert(elapsed < std::chrono::seconds(2));
}

void rejects_bad_arguments_and_reports_missing_capacity()
{
  const auto board = two_features();
  auto options = board.options(30.0);
  options.goal = 0.0;
  assert(board.propose(options).status == COORDINA_GRAPH_INVALID_ARGUMENT);
  options.goal = 1.5;
  assert(board.propose(options).status == COORDINA_GRAPH_INVALID_ARGUMENT);
  options = board.options(30.0);
  options.screen_run_count = 0U;
  assert(board.propose(options).status == COORDINA_GRAPH_INVALID_ARGUMENT);
  options = board.options(30.0);
  options.milestone = node(9999U);
  assert(board.propose(options).status == COORDINA_GRAPH_INVALID_ARGUMENT);

  options = board.options(30.0);
  coordina_graph_scope_cut output{};
  assert(
    coordina_graph_propose_scope(
      board.nodes.data(),
      board.nodes.size(),
      nullptr,
      0U,
      board.items.data(),
      board.items.size(),
      nullptr,
      0U,
      nullptr,
      1U,
      &options,
      &output)
    == COORDINA_GRAPH_INVALID_ARGUMENT);

  // Without buffers the call says how many cards it would write.
  assert(
    coordina_graph_propose_scope(
      board.nodes.data(),
      board.nodes.size(),
      board.edges.data(),
      board.edges.size(),
      board.items.data(),
      board.items.size(),
      nullptr,
      0U,
      nullptr,
      0U,
      &options,
      &output)
    == COORDINA_GRAPH_INSUFFICIENT_CAPACITY);
  assert(output.cut_count == 1U && output.dropped_count == 1U);
}
}

int main()
{
  cuts_the_least_valuable_card_that_makes_the_date();
  takes_out_what_only_the_cut_needs_and_keeps_shared_work();
  cutting_a_dependency_takes_out_the_cards_waiting_for_it();
  never_proposes_kept_cards_or_work_in_progress();
  never_cuts_what_a_kept_card_needs();
  proposes_nothing_when_the_milestone_is_on_track();
  measures_a_chosen_cut_and_ignores_the_milestone();
  gives_the_same_proposal_with_any_thread_count();
  stops_at_the_evaluation_and_time_limits();
  rejects_bad_arguments_and_reports_missing_capacity();

  return 0;
}
