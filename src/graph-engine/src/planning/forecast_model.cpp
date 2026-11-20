#include "planning/forecast_model.h"

#include <algorithm>
#include <unordered_map>
#include <unordered_set>

namespace coordina::graph
{
namespace
{
using IndexByNode = std::unordered_map<NodeId, std::uint32_t, NodeHash>;

std::vector<NodeId> neutral_ranking(
  const GraphSnapshot& graph,
  const std::vector<WorkItem>& work_items)
{
  auto neutral_items = work_items;

  for (auto& item : neutral_items)
  {
    item.assignment = Assignment::unassigned;
    item.is_context_match = false;
  }

  const auto recommendations = plan_work(
    graph,
    neutral_items,
    PlanningOptions{
      .max_size = CardSize::none,
      .prefer_assigned = false,
      .include_unassigned = true
    });
  std::vector<NodeId> ranking;
  ranking.reserve(graph.nodes().size());

  for (const auto& recommendation : recommendations)
  {
    ranking.push_back(recommendation.node);
  }

  const std::unordered_set<NodeId, NodeHash> ranked(ranking.begin(), ranking.end());

  for (const auto node : graph.nodes())
  {
    if (!ranked.contains(node))
    {
      ranking.push_back(node);
    }
  }

  return ranking;
}

std::unordered_set<NodeId, NodeHash> scope_of(const GraphSnapshot& graph, NodeId milestone)
{
  std::unordered_set<NodeId, NodeHash> scope{milestone};
  std::vector<NodeId> pending{milestone};

  while (!pending.empty())
  {
    const auto node = pending.back();
    pending.pop_back();

    for (const auto dependency : graph.outgoing(node))
    {
      if (scope.insert(dependency).second)
      {
        pending.push_back(dependency);
      }
    }
  }

  return scope;
}

IndexByNode index_by_node(const std::vector<NodeId>& nodes)
{
  IndexByNode index;
  index.reserve(nodes.size());

  for (std::uint32_t card = 0U; card < nodes.size(); ++card)
  {
    index.emplace(nodes[card], card);
  }

  return index;
}

// Turns per-card counts into offsets: the entries of card i end up in
// [offsets[i], offsets[i + 1]).
void to_offsets(std::vector<std::uint32_t>& offsets)
{
  std::uint32_t total = 0U;

  for (auto& offset : offsets)
  {
    const auto count = offset;
    offset = total;
    total += count;
  }
}
}

ForecastModel::ForecastModel(
  const GraphSnapshot& graph,
  const std::vector<WorkItem>& work_items,
  const std::vector<Assignee>& assignees,
  NodeId milestone,
  std::uint32_t person_count)
    : person_count_(person_count)
{
  if (!graph.contains(milestone))
  {
    error_ = ForecastError::unknown_milestone;
    return;
  }

  if (person_count == 0U
    || std::ranges::any_of(
      assignees,
      [person_count](const auto& assignee)
      {
        return assignee.person >= person_count;
      }))
  {
    error_ = ForecastError::unknown_person;
    return;
  }

  collect_scope(graph, work_items, milestone);
  link_dependents(graph);
  link_people(assignees);

  if (!is_acyclic())
  {
    error_ = ForecastError::cycle;
  }
}

ForecastError ForecastModel::error() const noexcept
{
  return error_;
}

std::size_t ForecastModel::card_count() const noexcept
{
  return nodes_.size();
}

std::uint32_t ForecastModel::person_count() const noexcept
{
  return person_count_;
}

std::uint32_t ForecastModel::unsized_count() const noexcept
{
  return unsized_count_;
}

const std::vector<NodeId>& ForecastModel::nodes() const noexcept
{
  return nodes_;
}

std::uint32_t ForecastModel::dependency_count(std::size_t card) const noexcept
{
  return dependency_counts_[card];
}

std::span<const std::uint32_t> ForecastModel::dependents(std::size_t card) const noexcept
{
  return std::span(dependents_)
    .subspan(dependent_offsets_[card], dependent_offsets_[card + 1U] - dependent_offsets_[card]);
}

std::span<const std::uint32_t> ForecastModel::people(std::size_t card) const noexcept
{
  return std::span(people_).subspan(
    people_offsets_[card],
    people_offsets_[card + 1U] - people_offsets_[card]);
}

const Triangular& ForecastModel::duration(std::size_t card) const noexcept
{
  return durations_[card];
}

std::vector<double> ForecastModel::likely_hours() const
{
  std::vector<double> hours;
  hours.reserve(durations_.size());

  for (const auto& duration : durations_)
  {
    hours.push_back(duration.likely());
  }

  return hours;
}

void ForecastModel::collect_scope(
  const GraphSnapshot& graph,
  const std::vector<WorkItem>& work_items,
  NodeId milestone)
{
  const auto scope = scope_of(graph, milestone);
  std::unordered_map<NodeId, const WorkItem*, NodeHash> items_by_node;
  items_by_node.reserve(work_items.size());

  for (const auto& item : work_items)
  {
    items_by_node.emplace(item.node, &item);
  }

  nodes_.reserve(scope.size());
  durations_.reserve(scope.size());

  for (const auto node : neutral_ranking(graph, work_items))
  {
    const auto found = items_by_node.find(node);
    const auto* item = found == items_by_node.end() ? nullptr : found->second;

    if (!scope.contains(node) || (item != nullptr && item->is_completed))
    {
      continue;
    }

    const auto size = item == nullptr ? CardSize::none : item->size;
    unsized_count_ += size == CardSize::none ? 1U : 0U;
    nodes_.push_back(node);
    durations_.emplace_back(planned_range(size));
  }
}

void ForecastModel::link_dependents(const GraphSnapshot& graph)
{
  const auto index = index_by_node(nodes_);
  std::vector<std::pair<std::uint32_t, std::uint32_t>> links;
  dependency_counts_.assign(nodes_.size(), 0U);
  dependent_offsets_.assign(nodes_.size() + 1U, 0U);

  for (std::uint32_t card = 0U; card < nodes_.size(); ++card)
  {
    for (const auto dependency : graph.outgoing(nodes_[card]))
    {
      if (const auto found = index.find(dependency); found != index.end())
      {
        links.emplace_back(found->second, card);
        ++dependency_counts_[card];
        ++dependent_offsets_[found->second];
      }
    }
  }

  to_offsets(dependent_offsets_);
  dependents_.resize(links.size());
  auto cursor = dependent_offsets_;

  for (const auto& [dependency, dependent] : links)
  {
    dependents_[cursor[dependency]++] = dependent;
  }
}

void ForecastModel::link_people(const std::vector<Assignee>& assignees)
{
  const auto index = index_by_node(nodes_);
  std::vector<std::pair<std::uint32_t, std::uint32_t>> links;
  people_offsets_.assign(nodes_.size() + 1U, 0U);

  for (const auto& assignee : assignees)
  {
    if (const auto found = index.find(assignee.node); found != index.end())
    {
      links.emplace_back(found->second, assignee.person);
    }
  }

  std::ranges::sort(links);
  const auto duplicates = std::ranges::unique(links);
  links.erase(duplicates.begin(), duplicates.end());

  for (const auto& [card, person] : links)
  {
    ++people_offsets_[card];
  }

  to_offsets(people_offsets_);
  people_.reserve(links.size());

  for (const auto& [card, person] : links)
  {
    people_.push_back(person);
  }
}

bool ForecastModel::is_acyclic() const
{
  auto remaining = dependency_counts_;
  std::vector<std::uint32_t> available;
  available.reserve(nodes_.size());

  for (std::uint32_t card = 0U; card < nodes_.size(); ++card)
  {
    if (remaining[card] == 0U)
    {
      available.push_back(card);
    }
  }

  std::size_t visited = 0U;

  while (!available.empty())
  {
    const auto card = available.back();
    available.pop_back();
    ++visited;

    for (const auto dependent : dependents(card))
    {
      if (--remaining[dependent] == 0U)
      {
        available.push_back(dependent);
      }
    }
  }

  return visited == nodes_.size();
}
}
