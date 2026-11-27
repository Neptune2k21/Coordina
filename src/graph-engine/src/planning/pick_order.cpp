#include "planning/pick_order.h"

#include <algorithm>
#include <cstdint>
#include <limits>
#include <numeric>
#include <ranges>

namespace coordina::graph
{
namespace
{
constexpr auto no_deadline = std::numeric_limits<double>::infinity();

// Dependencies before what depends on them; shorter than the cards when they
// depend on each other in a loop.
std::vector<std::uint32_t> dependency_order(const PlanModel& model)
{
  std::vector<std::uint32_t> remaining;
  std::vector<std::uint32_t> order;
  remaining.reserve(model.card_count());
  order.reserve(model.card_count());

  for (std::uint32_t card = 0U; card < model.card_count(); ++card)
  {
    remaining.push_back(model.dependency_count(card));

    if (remaining.back() == 0U)
    {
      order.push_back(card);
    }
  }

  for (std::size_t next = 0U; next < order.size(); ++next)
  {
    for (const auto dependent : model.dependents(order[next]))
    {
      if (--remaining[dependent] == 0U)
      {
        order.push_back(dependent);
      }
    }
  }

  return order;
}

// The latest each card can end, counting back through what depends on it.
void pull_back(
  const PlanModel& model,
  const std::vector<std::uint32_t>& order,
  std::vector<double>& ends)
{
  const auto& hours = model.planned_hours();

  for (const auto card : std::views::reverse(order))
  {
    for (const auto dependent : model.dependents(card))
    {
      ends[card] = std::min(ends[card], ends[dependent] - hours[dependent]);
    }
  }
}

bool node_less(NodeId left, NodeId right) noexcept
{
  return left.high != right.high ? left.high < right.high : left.low < right.low;
}

// Which of two cards that can end as late waits for the other: the one not
// started, then the less important one, then the lower on the board.
bool waits_for(const WorkItem& waiting, const WorkItem& first) noexcept
{
  if (waiting.is_in_progress != first.is_in_progress)
  {
    return first.is_in_progress;
  }

  if (waiting.priority != first.priority)
  {
    return waiting.priority < first.priority;
  }

  if (waiting.position != first.position)
  {
    return waiting.position > first.position;
  }

  return node_less(first.node, waiting.node);
}

// Cards only one person can do end one after the other.
void pull_back_people(const PlanModel& model, std::vector<double>& ends)
{
  const auto& hours = model.planned_hours();
  std::vector<std::vector<std::uint32_t>> own(model.person_count());

  for (std::uint32_t card = 0U; card < model.card_count(); ++card)
  {
    if (model.people(card).size() == 1U)
    {
      own[model.people(card).front()].push_back(card);
    }
  }

  for (auto& cards : own)
  {
    std::ranges::sort(
      cards,
      [&](std::uint32_t left, std::uint32_t right)
      {
        if (ends[left] < ends[right] || ends[right] < ends[left])
        {
          return ends[left] > ends[right];
        }

        return waits_for(model.item(left), model.item(right));
      });
    auto latest = no_deadline;

    for (const auto card : cards)
    {
      ends[card] = std::min(ends[card], latest);
      latest = ends[card] - hours[card];
    }
  }
}
}

std::vector<std::size_t> pick_order(const PlanModel& model)
{
  const auto dependencies = dependency_order(model);

  if (dependencies.size() != model.card_count())
  {
    return {};
  }

  std::vector<double> ends(model.card_count(), no_deadline);

  for (const auto& milestone : model.milestones())
  {
    if (milestone.card != PlanMilestone::done)
    {
      ends[milestone.card] = std::min(ends[milestone.card], milestone.target_hours);
    }
  }

  pull_back(model, dependencies, ends);
  pull_back_people(model, ends);
  pull_back(model, dependencies, ends);

  std::vector<double> starts;
  starts.reserve(ends.size());

  for (std::size_t card = 0U; card < ends.size(); ++card)
  {
    starts.push_back(ends[card] - model.planned_hours()[card]);
  }

  std::vector<std::size_t> order(model.card_count());
  std::iota(order.begin(), order.end(), std::size_t{0U});
  std::ranges::sort(
    order,
    [&](std::size_t left, std::size_t right)
    {
      const auto& one = model.item(left);
      const auto& other = model.item(right);

      if (one.is_in_progress != other.is_in_progress)
      {
        return one.is_in_progress;
      }

      if (starts[left] < starts[right] || starts[right] < starts[left])
      {
        return starts[left] < starts[right];
      }

      return waits_for(other, one);
    });

  return order;
}
}
