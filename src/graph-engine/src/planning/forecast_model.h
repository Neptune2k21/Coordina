#ifndef COORDINA_GRAPH_ENGINE_FORECAST_MODEL_H
#define COORDINA_GRAPH_ENGINE_FORECAST_MODEL_H

#include "core/graph_snapshot.h"
#include "planning/sampling.h"
#include "planning/work_planner.h"

#include <cstddef>
#include <cstdint>
#include <span>
#include <vector>

namespace coordina::graph
{
struct Assignee
{
  NodeId node{};
  std::uint32_t person = 0U;
};

enum class ForecastError : std::uint8_t
{
  none = 0U,
  unknown_milestone = 1U,
  unknown_person = 2U,
  cycle = 3U
};

// The open cards of a milestone's scope: the milestone and everything it
// depends on, directly or not. A card's index is its place in the order people
// pick cards up (plan_work, ranked for nobody in particular), and every
// relation is a flat index array the scheduler reads without hashing.
class ForecastModel
{
public:
  ForecastModel(
    const GraphSnapshot& graph,
    const std::vector<WorkItem>& work_items,
    const std::vector<Assignee>& assignees,
    NodeId milestone,
    std::uint32_t person_count);

  ForecastError error() const noexcept;
  std::size_t card_count() const noexcept;
  std::uint32_t person_count() const noexcept;
  std::uint32_t unsized_count() const noexcept;
  const std::vector<NodeId>& nodes() const noexcept;
  std::uint32_t dependency_count(std::size_t card) const noexcept;
  std::span<const std::uint32_t> dependents(std::size_t card) const noexcept;
  // Sorted; empty means anyone may take the card.
  std::span<const std::uint32_t> people(std::size_t card) const noexcept;
  const Triangular& duration(std::size_t card) const noexcept;
  std::vector<double> likely_hours() const;

private:
  void collect_scope(
    const GraphSnapshot& graph,
    const std::vector<WorkItem>& work_items,
    NodeId milestone);
  void link_dependents(const GraphSnapshot& graph);
  void link_people(const std::vector<Assignee>& assignees);
  bool is_acyclic() const;

  std::vector<NodeId> nodes_;
  std::vector<Triangular> durations_;
  std::vector<std::uint32_t> dependency_counts_;
  std::vector<std::uint32_t> dependent_offsets_;
  std::vector<std::uint32_t> dependents_;
  std::vector<std::uint32_t> people_offsets_;
  std::vector<std::uint32_t> people_;
  std::uint32_t person_count_ = 0U;
  std::uint32_t unsized_count_ = 0U;
  ForecastError error_ = ForecastError::none;
};
}

#endif
