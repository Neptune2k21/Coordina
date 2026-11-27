#ifndef COORDINA_GRAPH_ENGINE_SCOPE_GRAPH_H
#define COORDINA_GRAPH_ENGINE_SCOPE_GRAPH_H

#include "planning/plan_model.h"

#include <cstddef>
#include <cstdint>
#include <vector>

namespace coordina::graph
{
using CardMask = std::vector<std::uint8_t>;

// What giving a card up costs: a high-priority card is worth twice a medium
// one, and four times a low or unprioritised one. Its size says what it costs
// to do, not what it is worth.
double priority_weight(std::uint32_t priority) noexcept;

// A milestone's scope as a cut sees it: which cards the milestone still
// reaches once some are cut, and which ones nobody needs any more.
class ScopeGraph
{
public:
  // The milestone is an open one of the model.
  ScopeGraph(const PlanModel& model, std::size_t milestone);

  std::size_t milestone_card() const noexcept;
  bool contains(std::size_t card) const noexcept;
  // dropped: the cards of the scope the milestone does without once the cut
  // cards are out. active: the cards still to do, which leaves out the dropped
  // ones except those another milestone needs, or someone is working on.
  void apply(const CardMask& cut, CardMask& dropped, CardMask& active);
  // Marks the card and every card of the scope that depends on it, except
  // the milestone: what cutting the card takes out with it.
  void close_up(std::size_t card, CardMask& cut);

private:
  const PlanModel* model_;
  std::size_t milestone_;
  std::size_t milestone_card_;
  // Cards that stay to be done whatever this milestone needs.
  CardMask kept_;
  CardMask seen_;
  std::vector<std::uint32_t> stack_;
};
}

#endif
