#include "planning/board_index.h"

#include <algorithm>
#include <cmath>

namespace coordina::graph
{
IndexRows::IndexRows(
  std::size_t row_count,
  std::vector<std::pair<std::uint32_t, std::uint32_t>> pairs)
    : offsets_(row_count + 1U, 0U)
{
  std::ranges::sort(pairs);
  const auto duplicates = std::ranges::unique(pairs);
  pairs.erase(duplicates.begin(), duplicates.end());
  values_.reserve(pairs.size());

  for (const auto& [row, value] : pairs)
  {
    ++offsets_[row + 1U];
    values_.push_back(value);
  }

  for (std::size_t row = 0U; row < row_count; ++row)
  {
    offsets_[row + 1U] += offsets_[row];
  }
}

std::span<const std::uint32_t> IndexRows::row(std::size_t index) const noexcept
{
  return std::span(values_).subspan(offsets_[index], offsets_[index + 1U] - offsets_[index]);
}

BoardIndex::BoardIndex(const PlanningBoard& board) : error_(check(board))
{
  index_.reserve(board.nodes.size());

  for (std::uint32_t card = 0U; card < board.nodes.size(); ++card)
  {
    index_.emplace(board.nodes[card], card);
  }

  items_.resize(board.nodes.size());

  for (std::size_t card = 0U; card < board.nodes.size(); ++card)
  {
    items_[card].node = board.nodes[card];
    items_[card].position = std::numeric_limits<std::uint32_t>::max();
  }

  for (const auto& item : board.items)
  {
    if (const auto card = find(item.node); card != unknown)
    {
      items_[card] = item;
    }
  }

  std::vector<std::pair<std::uint32_t, std::uint32_t>> pairs;
  pairs.reserve(board.edges.size());

  for (const auto& edge : board.edges)
  {
    const auto card = find(edge.source);
    const auto dependency = find(edge.target);

    if (card != unknown && dependency != unknown)
    {
      pairs.emplace_back(card, dependency);
    }
  }

  dependencies_ = IndexRows(size(), std::move(pairs));
  pairs = {};

  for (const auto& assignee : board.assignees)
  {
    if (const auto card = find(assignee.node); card != unknown)
    {
      pairs.emplace_back(card, assignee.person);
    }
  }

  people_ = IndexRows(size(), std::move(pairs));
  pairs = {};

  for (const auto& label : board.labels)
  {
    if (const auto card = find(label.node); card != unknown)
    {
      pairs.emplace_back(card, label.label);
    }
  }

  labels_ = IndexRows(size(), std::move(pairs));

  if (error_ == PlanError::none
    && std::ranges::any_of(
      board.milestones,
      [this](const Milestone& milestone)
      {
        return find(milestone.node) == unknown;
      }))
  {
    error_ = PlanError::unknown_milestone;
  }
}

PlanError BoardIndex::error() const noexcept
{
  return error_;
}

std::size_t BoardIndex::size() const noexcept
{
  return items_.size();
}

std::uint32_t BoardIndex::find(NodeId node) const
{
  const auto found = index_.find(node);
  return found == index_.end() ? unknown : found->second;
}

const WorkItem& BoardIndex::item(std::size_t card) const noexcept
{
  return items_[card];
}

std::span<const std::uint32_t> BoardIndex::dependencies(std::size_t card) const noexcept
{
  return dependencies_.row(card);
}

std::span<const std::uint32_t> BoardIndex::people(std::size_t card) const noexcept
{
  return people_.row(card);
}

std::span<const std::uint32_t> BoardIndex::labels(std::size_t card) const noexcept
{
  return labels_.row(card);
}

PlanError BoardIndex::check(const PlanningBoard& board) noexcept
{
  for (const auto& assignee : board.assignees)
  {
    if (assignee.person >= board.person_count)
    {
      return PlanError::unknown_person;
    }
  }

  for (const auto& milestone : board.milestones)
  {
    if (!std::isfinite(milestone.target_hours) || !std::isfinite(milestone.weight)
      || milestone.weight <= 0.0)
    {
      return PlanError::bad_number;
    }
  }

  for (const auto& item : board.items)
  {
    if (!std::isfinite(item.elapsed_hours) || item.elapsed_hours < 0.0)
    {
      return PlanError::bad_number;
    }
  }

  for (const auto& card : board.history)
  {
    if (!std::isfinite(card.hours) || card.hours <= 0.0 || !std::isfinite(card.age_days)
      || card.age_days < 0.0)
    {
      return PlanError::bad_number;
    }
  }

  return PlanError::none;
}
}
