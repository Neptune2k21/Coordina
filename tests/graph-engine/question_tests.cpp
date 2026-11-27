#include "plan_board.h"

#include <algorithm>
#include <array>
#include <cassert>
#include <cstddef>
#include <cstdint>
#include <limits>
#include <vector>

namespace
{
using namespace coordina::graph::fixtures;

constexpr std::uint32_t ada = 0U;
constexpr std::uint32_t grace = 1U;

struct Questions
{
  const coordina_graph_question* find(std::uint32_t kind, unsigned long long id) const
  {
    const auto found = std::ranges::find_if(
      questions,
      [&](const auto& question)
      {
        return question.kind == kind && question.node.low == id;
      });
    return found == questions.end() ? nullptr : &*found;
  }

  coordina_graph_status status = COORDINA_GRAPH_OK;
  std::vector<coordina_graph_question> questions;
  double margin_days = 0.0;
};

Questions rank_questions(
  const PlanBoard& board,
  const std::vector<coordina_graph_card_token>& tokens = {},
  const std::vector<coordina_graph_dependency_hint>& hints = {},
  std::uint32_t limit = 20U)
{
  const auto view = board.view();
  const coordina_graph_question_options options{
    .milestone = node(milestone_id),
    .simulation = {board.seed, 2000U, 1U},
    .limit = limit,
    .reserved = 0U
  };
  Questions result;
  result.questions.resize(limit);
  coordina_graph_question_output output{result.questions.data(), result.questions.size(), 0U, 0.0};
  result.status = coordina_graph_rank_questions(
    &view,
    tokens.data(),
    tokens.size(),
    hints.data(),
    hints.size(),
    &options,
    &output);
  result.questions.resize(output.question_count);
  result.margin_days = output.margin_days;
  return result;
}

std::vector<coordina_graph_dependency_hint> suggest(
  const PlanBoard& board,
  const std::vector<coordina_graph_card_token>& tokens,
  const coordina_graph_node_id* source = nullptr)
{
  const auto view = board.view();
  std::vector<coordina_graph_dependency_hint> hints(50U);
  coordina_graph_hint_output output{hints.data(), hints.size(), 0U};
  const auto status = coordina_graph_suggest_dependencies(
    &view,
    tokens.data(),
    tokens.size(),
    source,
    static_cast<std::uint32_t>(hints.size()),
    &output);
  assert(status == COORDINA_GRAPH_OK);
  hints.resize(output.hint_count);
  return hints;
}

// Two unsized cards: one the date depends on, one that has days to spare.
PlanBoard unsized_board()
{
  PlanBoard board;
  board.people = 2U;
  board.card(milestone_id, COORDINA_GRAPH_SIZE_XS);
  board.card(1U, COORDINA_GRAPH_SIZE_NONE);
  board.card(2U, COORDINA_GRAPH_SIZE_XXL);
  board.card(3U, COORDINA_GRAPH_SIZE_NONE);
  board.depends(milestone_id, 1U);
  board.depends(1U, 2U);
  board.depends(milestone_id, 3U);
  board.assign(1U, ada);
  board.assign(2U, ada);
  board.assign(3U, grace);
  board.milestone(milestone_id, 100.0);
  return board;
}

void asks_about_the_cards_the_date_depends_on()
{
  const auto result = rank_questions(unsized_board());
  assert(result.status == COORDINA_GRAPH_OK && result.margin_days > 0.0);

  const auto* critical = result.find(COORDINA_GRAPH_QUESTION_SIZE, 1U);
  const auto* spare = result.find(COORDINA_GRAPH_QUESTION_SIZE, 3U);
  assert(critical != nullptr && critical->margin_days > 0.0);
  assert(spare == nullptr || spare->margin_days < critical->margin_days);
  // The XXL card before it makes most of the doubt.
  assert(result.find(COORDINA_GRAPH_QUESTION_SPLIT, 2U) != nullptr);

  for (std::size_t index = 0U; index < result.questions.size(); ++index)
  {
    assert(result.questions[index].margin_days <= result.margin_days);
    assert(
      index == 0U
      || result.questions[index].margin_days <= result.questions[index - 1U].margin_days);
  }

  // Never more than asked for.
  assert(rank_questions(unsized_board(), {}, {}, 1U).questions.size() == 1U);
}

void asks_what_is_left_of_a_card_that_drags_on()
{
  auto board = unsized_board();
  board.item(2U).size = COORDINA_GRAPH_SIZE_M;
  board.item(2U).is_in_progress = 1U;
  board.item(2U).elapsed_hours = 30.0;
  assert(rank_questions(board).find(COORDINA_GRAPH_QUESTION_PROGRESS, 2U) != nullptr);

  board.item(2U).elapsed_hours = 1.0;
  assert(rank_questions(board).find(COORDINA_GRAPH_QUESTION_PROGRESS, 2U) == nullptr);
}

void weighs_the_dependencies_an_assistant_suspects()
{
  // Grace's card may have to wait for Ada's long chain: a day of work would
  // become eleven.
  auto board = unsized_board();
  board.item(1U).size = COORDINA_GRAPH_SIZE_S;
  board.item(3U).size = COORDINA_GRAPH_SIZE_XL;
  const std::vector<coordina_graph_dependency_hint> likely{{node(3U), node(2U), 0.5}};
  const auto asked = rank_questions(board, {}, likely);
  const auto* question = asked.find(COORDINA_GRAPH_QUESTION_DEPENDENCY, 3U);
  assert(question != nullptr && question->other.low == 2U);
  assert(question->shift_days > 2.0 && question->probability == 0.5);

  // A sure thing is not worth asking about, nor one that closes a loop.
  const auto is_asked = [&](unsigned long long card, unsigned long long other, double probability)
  {
    const auto ranked = rank_questions(board, {}, {{node(card), node(other), probability}});
    const auto* found = ranked.find(COORDINA_GRAPH_QUESTION_DEPENDENCY, card);
    return found != nullptr && found->other.low == other;
  };
  assert(is_asked(3U, 2U, 0.5));
  assert(!is_asked(3U, 2U, 1.0));
  assert(!is_asked(2U, 1U, 0.5));

  const std::vector<coordina_graph_dependency_hint> impossible{
    {node(3U), node(2U), std::numeric_limits<double>::quiet_NaN()}
  };
  assert(rank_questions(board, {}, impossible).status == COORDINA_GRAPH_INVALID_ARGUMENT);
  assert(last_error_says("probability"));
}

// A checkout feature and a profile feature, each designed then built.
struct Story
{
  Story()
  {
    board.people = 1U;
    const std::array titles{
      std::vector{1U, 2U},
      std::vector{1U, 2U, 3U},
      std::vector{4U, 5U},
      std::vector{4U, 5U, 3U},
      std::vector{6U, 7U}
    };

    for (std::size_t index = 0U; index < titles.size(); ++index)
    {
      board.card(index + 1U, COORDINA_GRAPH_SIZE_M);
      board.label(index + 1U, index % 2U == 0U ? 1U : 2U);

      for (const auto token : titles.at(index))
      {
        tokens.push_back(coordina_graph_card_token{node(index + 1U), 1.0, token, 0U});
      }
    }
  }

  PlanBoard board;
  std::vector<coordina_graph_card_token> tokens;
};

void guesses_the_dependencies_nobody_entered()
{
  const Story story;
  const auto hints = suggest(story.board, story.tokens);
  assert(!hints.empty());

  // Building follows designing the same thing, in both features.
  const auto probability = [&](unsigned long long card, unsigned long long dependency)
  {
    const auto found = std::ranges::find_if(
      hints,
      [&](const auto& hint)
      {
        return hint.card.low == card && hint.dependency.low == dependency;
      });
    return found == hints.end() ? 0.0 : found->probability;
  };
  assert(probability(2U, 1U) > 0.3 && probability(4U, 3U) > 0.3);
  assert(probability(2U, 1U) > probability(1U, 2U));
  assert(probability(2U, 1U) > 2.0 * probability(2U, 5U));

  // Limited to what one card may wait for.
  const auto source = node(2U);

  for (const auto& hint : suggest(story.board, story.tokens, &source))
  {
    assert(hint.card.low == 2U);
  }
}

void leaves_out_what_is_known_or_impossible()
{
  Story story;
  story.board.depends(2U, 1U);
  story.board.depends(4U, 2U);
  story.board.item(5U).is_completed = 1U;

  for (const auto& hint : suggest(story.board, story.tokens))
  {
    const auto is = [&hint](unsigned long long card, unsigned long long dependency)
    {
      return hint.card.low == card && hint.dependency.low == dependency;
    };
    // Known, following from others, or closing a loop.
    assert(!is(2U, 1U) && !is(4U, 1U) && !is(1U, 4U));
    assert(hint.card.low != 5U && hint.dependency.low != 5U);
  }
}

void learns_how_a_board_links_its_cards()
{
  // Nothing but labels tells these cards apart. Label 2 always waits for
  // label 1 on this board.
  PlanBoard board;
  board.people = 1U;

  for (unsigned long long id = 1U; id <= 24U; ++id)
  {
    board.card(id, COORDINA_GRAPH_SIZE_M).created_order = 0U;
    board.label(id, id % 2U == 1U ? 1U : 2U);
  }

  const auto card = node(24U);
  const auto before = suggest(board, {}, &card);

  for (unsigned long long id = 2U; id <= 20U; id += 2U)
  {
    board.depends(id, id - 1U);
  }

  const auto best = [](const std::vector<coordina_graph_dependency_hint>& hints)
  {
    return hints.empty() ? 0.0 : hints.front().probability;
  };
  const auto after = suggest(board, {}, &card);
  assert(best(after) > best(before));
  assert(after.front().dependency.low % 2U == 1U);
}

void asks_nothing_about_a_done_milestone()
{
  auto board = unsized_board();
  board.item(milestone_id).is_completed = 1U;
  assert(rank_questions(board).questions.empty());
}
}

int main()
{
  asks_about_the_cards_the_date_depends_on();
  asks_what_is_left_of_a_card_that_drags_on();
  weighs_the_dependencies_an_assistant_suspects();
  guesses_the_dependencies_nobody_entered();
  leaves_out_what_is_known_or_impossible();
  learns_how_a_board_links_its_cards();
  asks_nothing_about_a_done_milestone();

  return 0;
}
