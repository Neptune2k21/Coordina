#ifndef COORDINA_GRAPH_ENGINE_SCOPE_GRAPH_H
#define COORDINA_GRAPH_ENGINE_SCOPE_GRAPH_H

#include "planning/forecast_model.h"

#include <cstddef>
#include <cstdint>
#include <limits>
#include <span>
#include <vector>

namespace coordina::graph
{
using CardMask = std::vector<std::uint8_t>;

// A milestone's scope as a cut sees it: what each card depends on, and which
// cards the milestone still reaches once some are cut. Cards it does not reach
// through open cards (below a completed one) never leave the scope.
class ScopeGraph
{
public:
  static constexpr auto no_card = std::numeric_limits<std::size_t>::max();

  ScopeGraph(const ForecastModel& model, NodeId milestone);

  // no_card when the milestone is not an open card of the scope.
  std::size_t milestone() const noexcept;
  bool is_reachable(std::size_t card) const noexcept;
  // Sets 1 for every card still in the scope once the cut cards are out.
  void activate(std::span<const std::uint8_t> cut, CardMask& active);
  // Marks the card and every card of the scope that depends on it, except
  // the milestone: what cutting the card takes out with it.
  void close_up(std::size_t card, CardMask& cut);

private:
  void reach(std::span<const std::uint8_t> cut, CardMask& seen);

  const ForecastModel* model_;
  std::size_t milestone_ = no_card;
  std::vector<std::uint32_t> dependency_offsets_;
  std::vector<std::uint32_t> dependencies_;
  CardMask reachable_;
  CardMask seen_;
  std::vector<std::uint32_t> stack_;
};
}

#endif
