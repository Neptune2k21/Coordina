#ifndef COORDINA_GRAPH_ENGINE_DEPENDENCY_HINTS_H
#define COORDINA_GRAPH_ENGINE_DEPENDENCY_HINTS_H

#include "planning/planning_board.h"

#include <cstddef>
#include <cstdint>
#include <vector>

namespace coordina::graph
{
// A word of a card's text, as a number, and how much it counts.
struct CardToken
{
  NodeId node{};
  std::uint32_t token = 0U;
  double weight = 1.0;
};

// A dependency the board probably misses: card waits for dependency.
struct DependencyHint
{
  NodeId card{};
  NodeId dependency{};
  double probability = 0.0;
};

// Looks for dependencies between open cards that nobody has entered. A pair is
// more likely when the cards talk about the same thing, share labels, were
// created together, and come in the order this board usually links its labels
// and its cards in. Those signals are weighed by a logistic regression whose
// prior is a sensible guess and which learns from the dependencies the board
// already has, so a board without any still gets suggestions. Pairs that would
// close a loop, or that follow from the others, are left out.
//
// source, when it is a card of the board, limits the hints to what it may wait
// for. The most likely come first.
std::vector<DependencyHint> suggest_dependencies(
  const PlanningBoard& board,
  const std::vector<CardToken>& tokens,
  const NodeId* source,
  std::size_t limit);
}

#endif
