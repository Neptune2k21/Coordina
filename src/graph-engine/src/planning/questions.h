#ifndef COORDINA_GRAPH_ENGINE_QUESTIONS_H
#define COORDINA_GRAPH_ENGINE_QUESTIONS_H

#include "planning/dependency_hints.h"
#include "planning/forecast.h"
#include "planning/planning_board.h"

#include <cstdint>
#include <vector>

namespace coordina::graph
{
enum class QuestionKind : std::uint8_t
{
  // How big is this card?
  size_card = 1U,
  // Can this card be split?
  split_card = 2U,
  // This card takes longer than expected: what is left?
  check_progress = 3U,
  // Does this card wait for that one?
  dependency = 4U
};

struct QuestionOptions
{
  NodeId milestone{};
  SimulationOptions simulation;
  std::uint32_t limit = 5U;
};

struct Question
{
  QuestionKind kind = QuestionKind::size_card;
  NodeId node{};
  // What node may wait for, for a dependency.
  NodeId other{};
  // How likely the dependency is; 1 for the other kinds.
  double probability = 1.0;
  // How many working days the answer is expected to take off the margin
  // between the 50% and 85% dates.
  double margin_days = 0.0;
  // How many days a dependency moves the 50% date when there is one; 0 for the
  // other kinds.
  double shift_days = 0.0;
};

struct QuestionList
{
  PlanError error = PlanError::none;
  // The days between the 50% and 85% dates as things are.
  double margin_days = 0.0;
  std::vector<Question> questions;
};

// Ranks what to ask the team so a milestone's date gets surer fastest. The
// value of an answer is the share of the date's variance it is expected to
// remove: for a card, the part its own uncertainty explains; for a possible
// dependency, how far it moves the date and how unsure it is. Most boards are
// half filled in, and asking for the few answers that matter beats asking for
// all of them.
//
// hints are possible dependencies from elsewhere, such as an assistant that
// has read the specifications; they replace the engine's own guess for the
// same pair. tokens are the cards' words, for the engine's guesses.
QuestionList rank_questions(
  const PlanningBoard& board,
  const std::vector<CardToken>& tokens,
  const std::vector<DependencyHint>& hints,
  const QuestionOptions& options);
}

#endif
