#include "planning/scope_search.h"

#include <algorithm>

namespace coordina::graph
{
namespace
{
constexpr auto no_card = ScopeGraph::no_card;
constexpr std::size_t swap_tries = 2U;

// The cards of a ranking, lowest first; ties go to the lower card.
std::vector<std::size_t> cards_of(
  std::vector<std::pair<double, std::size_t>>& ranking,
  std::size_t limit)
{
  std::ranges::sort(ranking);
  std::vector<std::size_t> cards;
  cards.reserve(std::min(limit, ranking.size()));

  for (std::size_t index = 0U; index < ranking.size() && index < limit; ++index)
  {
    cards.push_back(ranking[index].second);
  }

  return cards;
}
}

std::size_t ScopeSearch::index_of(NodeId node) const
{
  const auto found = index_.find(node);
  return found == index_.end() ? no_card : found->second;
}

// Whether the time left is less than reserve.
bool ScopeSearch::timed_out(Clock::duration reserve) const
{
  return options_->time_limit.count() > 0
    && Clock::now() - started_ + reserve >= options_->time_limit;
}

// The search keeps time for measuring the cut on every run, and for one
// repair.
bool ScopeSearch::exhausted() const
{
  return evaluations_ >= options_->evaluation_limit || timed_out(2 * full_run_cost_);
}

// A card can be cut when nothing its cut takes out has to stay.
void ScopeSearch::find_cuttable()
{
  for (std::size_t card = 0U; card < model_->card_count(); ++card)
  {
    if (card == graph_.milestone() || !graph_.is_reachable(card) || blocked_[card] != 0U)
    {
      continue;
    }

    std::ranges::fill(trial_cut_, 0U);
    graph_.close_up(card, trial_cut_);
    auto keeps_all = true;

    for (std::size_t other = 0U; other < trial_cut_.size() && keeps_all; ++other)
    {
      keeps_all = trial_cut_[other] == 0U || blocked_[other] == 0U;
    }

    if (keeps_all)
    {
      cuttable_.push_back(card);
    }
  }
}

void ScopeSearch::rebuild()
{
  std::ranges::fill(cut_, 0U);

  for (const auto card : chosen_)
  {
    graph_.close_up(card, cut_);
  }

  graph_.activate(cut_, active_);
}

// Rebuilds the cut from the chosen cards and simulates the scope left.
ScopeSample ScopeSearch::measure(std::size_t runs)
{
  rebuild();
  ++evaluations_;
  return simulator_.run(active_, runs, &contribution_);
}

// What adding the card to the cut gives up, and the hours it saves by the
// contributions of the last measure.
ScopeSearch::Margin ScopeSearch::margin_of(std::size_t card)
{
  trial_cut_ = cut_;
  graph_.close_up(card, trial_cut_);
  graph_.activate(trial_cut_, trial_active_);
  Margin margin;

  for (std::size_t other = 0U; other < active_.size(); ++other)
  {
    if (active_[other] != 0U && trial_active_[other] == 0U)
    {
      margin.value += values_[other];
      margin.gain += contribution_[other];
    }
  }

  return margin;
}

double ScopeSearch::value_of_cut() const
{
  double value = 0.0;

  for (std::size_t card = 0U; card < active_.size(); ++card)
  {
    value += active_[card] == 0U ? values_[card] : 0.0;
  }

  return value;
}

// The value the card's cut gives up on its own.
double ScopeSearch::value_of(std::size_t card)
{
  std::ranges::fill(trial_cut_, 0U);
  graph_.close_up(card, trial_cut_);
  graph_.activate(trial_cut_, trial_active_);
  double value = 0.0;

  for (std::size_t other = 0U; other < trial_active_.size(); ++other)
  {
    value += trial_active_[other] == 0U ? values_[other] : 0.0;
  }

  return value;
}

// Measures again after each batch, since cards that waited behind the ones
// cut may now set the date.
void ScopeSearch::grow(ScopeSample& screen)
{
  while (screen.on_time < options_->goal && !exhausted() && grow_batch(screen))
  {
    screen = measure(options_->screen_run_count);
  }
}

// Cuts the cards that give up least value per hour saved: enough of them that
// their savings would bring the goal's finish down to the target. Returns
// whether it cut any.
bool ScopeSearch::grow_batch(const ScopeSample& sample)
{
  Ranking candidates;
  candidates.reserve(cuttable_.size());

  for (const auto card : cuttable_)
  {
    if (cut_[card] == 0U)
    {
      if (const auto margin = margin_of(card); margin.gain > 0.0)
      {
        candidates.emplace_back(margin.value / margin.gain, card);
      }
    }
  }

  const auto needed = sample.goal_hours - options_->target_hours;
  const auto chosen_before = chosen_.size();
  auto saved = 0.0;

  for (const auto card : cards_of(candidates, candidates.size()))
  {
    if (saved >= needed)
    {
      break;
    }

    if (cut_[card] != 0U)
    {
      continue;
    }

    if (const auto margin = margin_of(card); margin.gain > 0.0)
    {
      chosen_.push_back(card);
      cut_ = trial_cut_;
      active_ = trial_active_;
      saved += margin.gain;
    }
  }

  return chosen_.size() > chosen_before;
}

// Gives back the cuts the goal no longer needs, the most valuable first.
void ScopeSearch::shrink(double floor)
{
  for (const auto card : by_value())
  {
    if (exhausted())
    {
      return;
    }

    const auto position = std::ranges::find(chosen_, card) - chosen_.begin();
    chosen_.erase(chosen_.begin() + position);

    if (measure(options_->screen_run_count).on_time < floor)
    {
      chosen_.insert(chosen_.begin() + position, card);
      rebuild();
    }
  }
}

// Trades each cut, the most valuable first, for a cheaper one that still
// reaches the floor.
void ScopeSearch::swap(double floor)
{
  for (const auto card : by_value())
  {
    if (exhausted())
    {
      return;
    }

    const auto cost = value_of_cut();
    const auto position = std::ranges::find(chosen_, card) - chosen_.begin();
    chosen_.erase(chosen_.begin() + position);
    measure(options_->screen_run_count);
    auto replaced = false;

    for (const auto other : cheapest_cuts(cost - value_of_cut()))
    {
      if (exhausted())
      {
        break;
      }

      chosen_.insert(chosen_.begin() + position, other);

      if (measure(options_->screen_run_count).on_time >= floor && value_of_cut() < cost)
      {
        replaced = true;
        break;
      }

      chosen_.erase(chosen_.begin() + position);
    }

    if (!replaced)
    {
      chosen_.insert(chosen_.begin() + position, card);
      rebuild();
    }
  }
}

// The chosen cards, from the one whose cut gives up most value.
std::vector<std::size_t> ScopeSearch::by_value()
{
  Ranking ranking;
  ranking.reserve(chosen_.size());

  for (const auto card : chosen_)
  {
    ranking.emplace_back(-value_of(card), card);
  }

  return cards_of(ranking, ranking.size());
}

// The cards outside the cut that give up less than the budget, best ratio of
// value to hours saved first.
std::vector<std::size_t> ScopeSearch::cheapest_cuts(double budget)
{
  Ranking ranking;

  for (const auto card : cuttable_)
  {
    if (cut_[card] != 0U)
    {
      continue;
    }

    if (const auto margin = margin_of(card); margin.gain > 0.0 && margin.value < budget)
    {
      ranking.emplace_back(margin.value / margin.gain, card);
    }
  }

  return cards_of(ranking, swap_tries);
}
}
