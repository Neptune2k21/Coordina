#include "plan_board.h"

#include "planning/board_index.h"
#include "planning/card_size.h"
#include "planning/duration_model.h"
#include "planning/planning_board.h"

#include <array>
#include <cassert>
#include <cmath>
#include <cstdint>
#include <vector>

namespace
{
using namespace coordina::graph;
using fixtures::PlanBoard;

PlanningBoard history_of(std::uint32_t cards, double ratio, double age_days = 1.0)
{
  PlanningBoard board;
  board.person_count = 2U;

  for (std::uint32_t card = 0U; card < cards; ++card)
  {
    const NodeId node{.high = 0U, .low = card + 1U};
    WorkItem item;
    item.node = node;
    item.is_completed = true;
    item.size = card % 2U == 0U ? CardSize::m : CardSize::s;
    board.nodes.push_back(node);
    board.items.push_back(item);
    board.assignees.push_back(Assignee{.node = node, .person = card % 2U});
    board.labels.push_back(CardLabel{.node = node, .label = card % 3U});
    // Slightly more or less than the ratio, so the noise is not zero.
    const auto wobble = card % 4U < 2U ? 1.1 : 1.0 / 1.1;
    board.history.push_back(
      FinishedCard{
        .node = node,
        .hours = size_range(item.size).likely_hours * ratio * wobble,
        .age_days = age_days
      });
  }

  return board;
}

double team_ratio(const PlanningBoard& board)
{
  const BoardIndex index(board);
  return std::exp(DurationModel(board, index, 1U).team_ratio().mean);
}

double team_spread(const PlanningBoard& board)
{
  const BoardIndex index(board);
  return DurationModel(board, index, 1U).team_ratio().sigma;
}

void means_what_sizes_say_without_history()
{
  const auto board = history_of(0U, 1.0);
  assert(team_ratio(board) == 1.0);
  assert(team_spread(board) > 0.2);
}

void learns_how_slow_a_team_is_gradually()
{
  const auto few = team_ratio(history_of(3U, 2.0));
  const auto some = team_ratio(history_of(12U, 2.0));
  const auto many = team_ratio(history_of(200U, 2.0));

  assert(few > 1.0 && few < some && some < many);
  // With 200 cards of evidence the ratio is the one observed, but for what
  // the size, person and label effects share of it.
  assert(many > 1.5 && many < 2.05);
  // And the more cards, the surer.
  assert(team_spread(history_of(200U, 2.0)) < team_spread(history_of(12U, 2.0)));
  assert(team_spread(history_of(12U, 2.0)) < team_spread(history_of(3U, 2.0)));
}

void predicts_a_card_from_all_that_is_known_of_it()
{
  const auto board = history_of(200U, 2.0);
  const BoardIndex index(board);
  const DurationModel model(board, index, 1U);
  std::vector<EffectWeight> effects;
  model.append_effects(index, 0U, effects);

  // Team, size, person and label together give the observed ratio back.
  assert(std::abs(std::exp(model.mean_shift(CardSize::m, effects)) - 2.0) < 0.1);
}

void forgets_old_cards()
{
  assert(team_ratio(history_of(20U, 2.0, 400.0)) < team_ratio(history_of(20U, 2.0, 1.0)));
}

void bounds_the_pull_of_outliers()
{
  auto board = history_of(40U, 1.0);
  const auto before = team_ratio(board);
  // A card forgotten in progress for months.
  board.history.back().hours = 2000.0;
  const auto after = team_ratio(board);

  assert(after > before && after < before * 1.1);
}

void ignores_cards_moved_rather_than_worked_on()
{
  auto board = history_of(20U, 1.0);
  const auto before = team_ratio(board);

  for (auto& card : board.history)
  {
    card.hours = 0.01;
  }

  assert(team_ratio(board) == 1.0);
  assert(before > 0.8);
}

void learns_from_cards_still_in_progress()
{
  // The completed cards were quick, but four others drag on: looking only at
  // what is done would call the team fast.
  auto board = history_of(8U, 1.0);
  const auto without = team_ratio(board);

  for (std::uint32_t card = 0U; card < 4U; ++card)
  {
    const NodeId node{.high = 0U, .low = 100U + card};
    WorkItem item;
    item.node = node;
    item.is_in_progress = true;
    item.size = CardSize::m;
    item.elapsed_hours = 20.0;
    board.nodes.push_back(node);
    board.items.push_back(item);
  }

  assert(team_ratio(board) > without * 1.1);
}

void learns_how_often_cards_run_away()
{
  auto board = history_of(100U, 1.0);
  const BoardIndex index(board);
  const auto usual = DurationModel(board, index, 1U).overruns().share;

  for (std::size_t card = 0U; card < board.history.size(); card += 10U)
  {
    board.history[card].hours *= 8.0;
  }

  const DurationModel model(board, index, 1U);
  assert(model.overruns().share > usual * 3.0);
  // A card far past its range has more likely run away than nearly ended.
  assert(model.overrun_chance(0.0, 0.5) < 0.5);
  assert(model.overrun_chance(6.0, 0.5) > 0.95);
}

void gives_unsized_cards_the_sizes_of_the_board()
{
  PlanningBoard board;

  for (std::uint32_t card = 0U; card < 100U; ++card)
  {
    const NodeId node{.high = 0U, .low = card + 1U};
    WorkItem item;
    item.node = node;
    item.size = CardSize::xl;
    board.nodes.push_back(node);
    board.items.push_back(item);
  }

  const BoardIndex index(board);
  const DurationModel model(board, index, 1U);
  assert(model.size_mix().at(static_cast<std::size_t>(CardSize::xl)) > 0.9);
  assert(model.size_at(0.5) == CardSize::xl);
  // XXL is never guessed from nothing, and the mix covers the whole range.
  assert(model.size_at(1.0) == CardSize::xl);
  assert(model.size_at(0.0) == CardSize::xs);
}

void draws_the_shared_effects_once_per_run()
{
  const auto board = history_of(0U, 1.0);
  const BoardIndex index(board);
  const DurationModel model(board, index, 1U);
  auto draw = model.make_draw();
  auto again = model.make_draw();
  model.draw(3U, draw);
  model.draw(3U, again);

  assert(draw.by_size == again.by_size && draw.noise == again.noise);
  model.draw(4U, again);
  assert(draw.by_size != again.by_size);
}

void reports_the_calibration_through_the_abi()
{
  PlanBoard board;
  board.people = 1U;

  for (unsigned long long id = 1U; id <= 60U; ++id)
  {
    board.finished(
      id,
      COORDINA_GRAPH_SIZE_M,
      id % 3U == 0U ? 12.0 : 9.0,
      61.0 - static_cast<double>(id));
    board.label(id, static_cast<std::uint32_t>(id % 2U));
  }

  std::array<coordina_graph_duration_ratio, 6U> sizes{};
  std::array<coordina_graph_duration_ratio, 32U> labels{};
  coordina_graph_calibration output{};
  output.sizes = sizes.data();
  output.size_capacity = sizes.size();
  output.labels = labels.data();
  output.label_capacity = labels.size();
  const auto view = board.view();

  assert(coordina_graph_calibrate(&view, &output) == COORDINA_GRAPH_OK);
  assert(output.team.card_count == 60U && output.team.median > 1.2);
  assert(output.team.low < output.team.median && output.team.median < output.team.high);
  assert(output.size_count == 6U && sizes[2U].key == COORDINA_GRAPH_SIZE_M);
  assert(sizes[2U].card_count == 60U && sizes[2U].median > 1.4);
  assert(output.label_count == 2U && labels[0U].card_count == 30U);
  assert(output.replayed_count == 55U);
  assert(output.within_p50 > 0.0 && output.within_p85 >= output.within_p50);

  // A card moved through the lists in seconds was not worked on.
  board.finished(61U, COORDINA_GRAPH_SIZE_XL, 0.01);
  const auto with_moved = board.view();
  assert(coordina_graph_calibrate(&with_moved, &output) == COORDINA_GRAPH_OK);
  assert(output.team.card_count == 60U && sizes[4U].card_count == 0U);

  output.size_capacity = 2U;
  assert(coordina_graph_calibrate(&view, &output) == COORDINA_GRAPH_INSUFFICIENT_CAPACITY);
  assert(output.size_count == 6U);
}
}

int main()
{
  means_what_sizes_say_without_history();
  learns_how_slow_a_team_is_gradually();
  predicts_a_card_from_all_that_is_known_of_it();
  forgets_old_cards();
  bounds_the_pull_of_outliers();
  ignores_cards_moved_rather_than_worked_on();
  learns_from_cards_still_in_progress();
  learns_how_often_cards_run_away();
  gives_unsized_cards_the_sizes_of_the_board();
  draws_the_shared_effects_once_per_run();
  reports_the_calibration_through_the_abi();

  return 0;
}
