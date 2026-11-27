#ifndef COORDINA_GRAPH_ENGINE_PLANNING_BRIDGE_H
#define COORDINA_GRAPH_ENGINE_PLANNING_BRIDGE_H

#include "coordina_graph_engine.h"
#include "interop/abi_errors.h"
#include "interop/abi_queries.h"
#include "interop/native_bridge.h"
#include "planning/dependency_hints.h"
#include "planning/forecast.h"
#include "planning/planning_board.h"

#include <cstddef>
#include <cstdint>
#include <vector>

// What the planning exports share: checking their arguments, and turning the
// ABI's structs into the engine's and back.
namespace coordina::graph::abi
{
constexpr std::uint32_t run_count_limit = 100'000U;
// Bounds the arrays sized by people.
constexpr std::uint32_t person_count_limit = 100'000U;

// COORDINA_GRAPH_OK when the board's arrays can be read.
coordina_graph_status check_board(const coordina_graph_board* board) noexcept;
coordina_graph_status check_simulation(const coordina_graph_simulation& simulation) noexcept;
// Whether the array can be read and every person is one of the board's.
coordina_graph_status check_people(
  const std::uint32_t* people,
  std::size_t count,
  const coordina_graph_board& board) noexcept;
coordina_graph_status check_people(
  const coordina_graph_assignee* pairs,
  std::size_t count,
  const coordina_graph_board& board) noexcept;
coordina_graph_status check_array(const void* items, std::size_t count) noexcept;

// What an engine error means to the caller, whose last error it sets.
coordina_graph_status to_status(PlanError error) noexcept;

PlanningBoard build_board(const coordina_graph_board& board);
SimulationOptions build_simulation(const coordina_graph_simulation& simulation) noexcept;
std::vector<Assignee> build_assignees(const coordina_graph_assignee* assignees, std::size_t count);
std::vector<NodeId> build_node_ids(const coordina_graph_node_id* nodes, std::size_t count);
std::vector<CardToken> build_tokens(const coordina_graph_card_token* tokens, std::size_t count);

// Writes every item when the buffer is large enough, and says whether it was.
template <typename Item, typename Output, typename Convert>
bool write_items(
  const std::vector<Item>& items,
  Output* output,
  std::size_t capacity,
  std::size_t& count,
  const Convert& convert)
{
  count = items.size();

  if (!fits(count, output, capacity))
  {
    return false;
  }

  for (std::size_t index = 0U; index < items.size(); ++index)
  {
    output[index] = convert(items[index]);
  }

  return true;
}
}

#endif
