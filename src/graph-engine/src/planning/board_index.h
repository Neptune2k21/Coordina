#ifndef COORDINA_GRAPH_ENGINE_BOARD_INDEX_H
#define COORDINA_GRAPH_ENGINE_BOARD_INDEX_H

#include "planning/planning_board.h"

#include <cstddef>
#include <cstdint>
#include <limits>
#include <span>
#include <unordered_map>
#include <utility>
#include <vector>

namespace coordina::graph
{
// Rows of indexes packed one after the other.
class IndexRows
{
public:
  IndexRows() = default;
  // Sorts the (row, value) pairs and drops the duplicates.
  IndexRows(std::size_t row_count, std::vector<std::pair<std::uint32_t, std::uint32_t>> pairs);

  std::span<const std::uint32_t> row(std::size_t index) const noexcept;

private:
  std::vector<std::uint32_t> offsets_;
  std::vector<std::uint32_t> values_;
};

// A board numbered from 0 in node order, with every relation as rows of
// indexes: hashing happens once, here.
class BoardIndex
{
public:
  static constexpr auto unknown = std::numeric_limits<std::uint32_t>::max();

  explicit BoardIndex(const PlanningBoard& board);

  PlanError error() const noexcept;
  std::size_t size() const noexcept;
  std::uint32_t find(NodeId node) const;
  // A node without a work item is an open, unsized card.
  const WorkItem& item(std::size_t card) const noexcept;
  std::span<const std::uint32_t> dependencies(std::size_t card) const noexcept;
  std::span<const std::uint32_t> people(std::size_t card) const noexcept;
  std::span<const std::uint32_t> labels(std::size_t card) const noexcept;

private:
  static PlanError check(const PlanningBoard& board) noexcept;

  std::unordered_map<NodeId, std::uint32_t, NodeHash> index_;
  std::vector<WorkItem> items_;
  IndexRows dependencies_;
  IndexRows people_;
  IndexRows labels_;
  PlanError error_ = PlanError::none;
};
}

#endif
