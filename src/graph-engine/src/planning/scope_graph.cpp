#include "planning/scope_graph.h"

#include <algorithm>

namespace coordina::graph
{
double priority_weight(std::uint32_t priority) noexcept
{
  switch (priority)
  {
    case 3U:
      return 4.0;
    case 2U:
      return 2.0;
    default:
      return 1.0;
  }
}

ScopeGraph::ScopeGraph(const PlanModel& model, std::size_t milestone)
    : model_(&model), milestone_(milestone), milestone_card_(model.milestones()[milestone].card),
      kept_(model.card_count(), 0U), seen_(model.card_count(), 0U)
{
  stack_.reserve(model.card_count());

  for (std::size_t card = 0U; card < model.card_count(); ++card)
  {
    auto is_kept = !model.in_scope(milestone, card)
      || (model.item(card).is_in_progress && model.dependency_count(card) == 0U);

    for (std::size_t other = 0U; other < model.milestones().size(); ++other)
    {
      is_kept = is_kept || (other != milestone && model.in_scope(other, card));
    }

    kept_[card] = is_kept ? 1U : 0U;
  }
}

std::size_t ScopeGraph::milestone_card() const noexcept
{
  return milestone_card_;
}

bool ScopeGraph::contains(std::size_t card) const noexcept
{
  return model_->in_scope(milestone_, card);
}

void ScopeGraph::apply(const CardMask& cut, CardMask& dropped, CardMask& active)
{
  std::ranges::fill(seen_, 0U);
  stack_.clear();
  stack_.push_back(static_cast<std::uint32_t>(milestone_card_));
  seen_[milestone_card_] = 1U;

  while (!stack_.empty())
  {
    const auto card = stack_.back();
    stack_.pop_back();

    for (const auto dependency : model_->dependencies(card))
    {
      if (seen_[dependency] == 0U && cut[dependency] == 0U)
      {
        seen_[dependency] = 1U;
        stack_.push_back(dependency);
      }
    }
  }

  for (std::size_t card = 0U; card < seen_.size(); ++card)
  {
    dropped[card] = contains(card) && seen_[card] == 0U ? 1U : 0U;
    active[card] = dropped[card] == 0U || kept_[card] != 0U ? 1U : 0U;
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

    if (next == milestone_card_ || cut[next] != 0U || !contains(next))
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
}
