#include "planning/forecast.h"

#include "planning/work_planner.h"

#include <algorithm>
#include <cmath>
#include <unordered_map>
#include <unordered_set>

namespace coordina::graph
{
namespace
{
// splitmix64: small, fast and fully specified, so a seed gives the same
// numbers with every compiler and standard library.
std::uint64_t next_random(std::uint64_t& state) noexcept
{
  state += 0x9e3779b97f4a7c15ULL;
  auto value = state;
  value = (value ^ (value >> 30U)) * 0xbf58476d1ce4e5b9ULL;
  value = (value ^ (value >> 27U)) * 0x94d049bb133111ebULL;
  return value ^ (value >> 31U);
}

// Uniform in [0, 1), from the top 53 bits.
double next_uniform(std::uint64_t& state) noexcept
{
  constexpr auto scale = 1.0 / static_cast<double>(1ULL << 53U);
  return static_cast<double>(next_random(state) >> 11U) * scale;
}

// The triangular distribution from low to high, peaking at likely.
double triangular(const SizeRange& range, double uniform) noexcept
{
  const auto span = range.high_hours - range.low_hours;

  if (!(span > 0.0))
  {
    return range.likely_hours;
  }

  const auto below = range.likely_hours - range.low_hours;
  const auto above = range.high_hours - range.likely_hours;

  return uniform < below / span ? range.low_hours + std::sqrt(uniform * span * below)
                                : range.high_hours - std::sqrt((1.0 - uniform) * span * above);
}

// The value below which percent of the sorted samples fall (nearest rank).
double percentile(const std::vector<double>& sorted, std::size_t percent) noexcept
{
  if (sorted.empty())
  {
    return 0.0;
  }

  const auto rank = ((sorted.size() * percent) + 99U) / 100U;
  return sorted[rank == 0U ? 0U : rank - 1U];
}

// The cards in the order plan_work ranks them for nobody in particular, so
// the forecast does not depend on who asks.
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

  // Nodes without a work item come last, in graph order.
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

}

MilestoneWork::MilestoneWork(
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

  collect_scope(graph, work_items, milestone);
  link_cards(graph);
  assign_people(assignees, person_count);

  if (error_ == ForecastError::none && !is_acyclic())
  {
    error_ = ForecastError::cycle;
  }
}

ForecastError MilestoneWork::error() const noexcept
{
  return error_;
}

const std::vector<NodeId>& MilestoneWork::nodes() const noexcept
{
  return nodes_;
}

std::uint32_t MilestoneWork::unsized_count() const noexcept
{
  return unsized_count_;
}

std::vector<double> MilestoneWork::likely_hours() const
{
  std::vector<double> hours;
  hours.reserve(cards_.size());

  for (const auto& card : cards_)
  {
    hours.push_back(card.range.likely_hours);
  }

  return hours;
}

std::vector<double> MilestoneWork::draw_hours(std::uint64_t& random_state) const
{
  std::vector<double> hours;
  hours.reserve(cards_.size());

  for (const auto& card : cards_)
  {
    hours.push_back(triangular(card.range, next_uniform(random_state)));
  }

  return hours;
}

Schedule MilestoneWork::schedule(const std::vector<double>& hours) const
{
  if (error_ != ForecastError::none || hours.size() != cards_.size())
  {
    return {};
  }

  return simulate_schedule(cards_, person_count_, hours);
}

void MilestoneWork::collect_scope(
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
    cards_.push_back(
      MilestoneCard{
        .node = node,
        .range = planned_range(size),
        .people = {},
        .dependents = {},
        .dependency_count = 0U
      });
    nodes_.push_back(node);
  }
}

void MilestoneWork::link_cards(const GraphSnapshot& graph)
{
  std::unordered_map<NodeId, std::size_t, NodeHash> index_of;
  index_of.reserve(nodes_.size());

  for (std::size_t index = 0U; index < nodes_.size(); ++index)
  {
    index_of.emplace(nodes_[index], index);
  }

  for (std::size_t index = 0U; index < nodes_.size(); ++index)
  {
    for (const auto dependency : graph.outgoing(nodes_[index]))
    {
      const auto found = index_of.find(dependency);

      if (found != index_of.end())
      {
        ++cards_[index].dependency_count;
        cards_[found->second].dependents.push_back(index);
      }
    }
  }
}

void MilestoneWork::assign_people(
  const std::vector<Assignee>& assignees,
  std::uint32_t person_count)
{
  if (person_count == 0U)
  {
    error_ = ForecastError::unknown_person;
    return;
  }

  std::unordered_map<NodeId, std::size_t, NodeHash> index_of;
  index_of.reserve(nodes_.size());

  for (std::size_t index = 0U; index < nodes_.size(); ++index)
  {
    index_of.emplace(nodes_[index], index);
  }

  for (const auto& assignee : assignees)
  {
    if (assignee.person >= person_count)
    {
      error_ = ForecastError::unknown_person;
      return;
    }

    const auto found = index_of.find(assignee.node);

    if (found != index_of.end())
    {
      cards_[found->second].people.push_back(assignee.person);
    }
  }

  for (auto& card : cards_)
  {
    std::ranges::sort(card.people);
    const auto duplicates = std::ranges::unique(card.people);
    card.people.erase(duplicates.begin(), duplicates.end());
  }
}

bool MilestoneWork::is_acyclic() const
{
  std::vector<std::size_t> remaining;
  std::vector<std::size_t> available;
  remaining.reserve(cards_.size());

  for (std::size_t index = 0U; index < cards_.size(); ++index)
  {
    remaining.push_back(cards_[index].dependency_count);

    if (remaining.back() == 0U)
    {
      available.push_back(index);
    }
  }

  std::size_t visited = 0U;

  while (!available.empty())
  {
    const auto card = available.back();
    available.pop_back();
    ++visited;

    for (const auto dependent : cards_[card].dependents)
    {
      if (--remaining[dependent] == 0U)
      {
        available.push_back(dependent);
      }
    }
  }

  return visited == cards_.size();
}

Forecast forecast(const MilestoneWork& work, std::uint32_t run_count, std::uint64_t seed)
{
  const auto card_count = work.nodes().size();
  std::vector<std::uint32_t> driving_runs(card_count, 0U);
  std::vector<double> finishes;
  finishes.reserve(run_count);
  auto random_state = seed;

  for (std::uint32_t run = 0U; run < run_count; ++run)
  {
    const auto schedule = work.schedule(work.draw_hours(random_state));
    finishes.push_back(schedule.finish_hours);

    for (const auto card : schedule.driving_chain)
    {
      ++driving_runs[card];
    }
  }

  std::ranges::sort(finishes);
  Forecast result{
    .p50_days = percentile(finishes, 50U) / focus_hours_per_day,
    .p85_days = percentile(finishes, 85U) / focus_hours_per_day,
    .unsized_count = work.unsized_count(),
    .cards = {}
  };
  result.cards.reserve(card_count);

  for (std::size_t card = 0U; card < card_count; ++card)
  {
    result.cards.push_back(
      CardCriticality{
        .node = work.nodes()[card],
        .criticality = run_count == 0U
          ? 0.0
          : static_cast<double>(driving_runs[card]) / static_cast<double>(run_count)
      });
  }

  // Ties keep the scheduling order.
  std::ranges::stable_sort(
    result.cards,
    [](const auto& left, const auto& right)
    {
      return left.criticality > right.criticality;
    });

  return result;
}
}
