#include "planning/scope_graph.h"

#include <algorithm>

namespace coordina::graph
{
ScopeGraph::ScopeGraph(const ForecastModel& model, NodeId milestone)
    : model_(&model), dependency_offsets_(model.card_count() + 1U, 0U),
      reachable_(model.card_count(), 0U), seen_(model.card_count(), 0U)
{
  const auto count = model.card_count();

  for (std::size_t card = 0U; card < count; ++card)
  {
    if (model.nodes()[card] == milestone)
    {
      milestone_ = card;
    }

    for (const auto dependent : model.dependents(card))
    {
      ++dependency_offsets_[dependent + 1U];
    }
  }

  for (std::size_t card = 0U; card < count; ++card)
  {
    dependency_offsets_[card + 1U] += dependency_offsets_[card];
  }

  dependencies_.resize(dependency_offsets_.back());
  auto cursor = dependency_offsets_;

  for (std::size_t card = 0U; card < count; ++card)
  {
    for (const auto dependent : model.dependents(card))
    {
      dependencies_[cursor[dependent]++] = static_cast<std::uint32_t>(card);
    }
  }

  stack_.reserve(count);

  if (milestone_ != no_card)
  {
    reach(CardMask(count, 0U), reachable_);
  }
}

std::size_t ScopeGraph::milestone() const noexcept
{
  return milestone_;
}

bool ScopeGraph::is_reachable(std::size_t card) const noexcept
{
  return reachable_[card] != 0U;
}

void ScopeGraph::activate(std::span<const std::uint8_t> cut, CardMask& active)
{
  reach(cut, seen_);

  for (std::size_t card = 0U; card < active.size(); ++card)
  {
    active[card] = reachable_[card] == 0U || seen_[card] != 0U ? 1U : 0U;
  }
}

void ScopeGraph::close_up(std::size_t card, CardMask& cut)
{
  stack_.clear();
  stack_.push_back(static_cast<std::uint32_t>(card));

  while (!stack_.empty())
  {
    const auto next = stack_.back();
    stack_.pop_back();

    if (next == milestone_ || cut[next] != 0U)
    {
      continue;
    }

    cut[next] = 1U;

    for (const auto dependent : model_->dependents(next))
    {
      stack_.push_back(dependent);
    }
  }
}

void ScopeGraph::reach(std::span<const std::uint8_t> cut, CardMask& seen)
{
  std::ranges::fill(seen, 0U);
  stack_.clear();
  stack_.push_back(static_cast<std::uint32_t>(milestone_));
  seen[milestone_] = 1U;

  while (!stack_.empty())
  {
    const auto card = stack_.back();
    stack_.pop_back();

    for (auto index = dependency_offsets_[card]; index < dependency_offsets_[card + 1U]; ++index)
    {
      const auto dependency = dependencies_[index];

      if (seen[dependency] == 0U && cut[dependency] == 0U)
      {
        seen[dependency] = 1U;
        stack_.push_back(dependency);
      }
    }
  }
}
}
