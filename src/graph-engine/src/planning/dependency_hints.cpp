#include "planning/dependency_hints.h"

#include "numeric/cholesky.h"
#include "planning/board_index.h"
#include "planning/sampling.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <span>
#include <unordered_map>
#include <unordered_set>
#include <utility>

namespace coordina::graph
{
namespace
{
// What tells that a card waits for another, in this order: the same words, the
// same labels, created before, created together, how the labels are usually
// linked, and whether the other is started.
constexpr std::size_t signal_count = 6U;
using Signals = std::array<double, signal_count>;

// Most pairs of cards are unrelated: without any signal the odds are low. The
// intercept is not learned, since the pairs learned from are not a fair sample.
constexpr double base_logit = -3.5;
constexpr Signals prior_weights{5.0, 1.0, 0.7, 0.8, 1.0, 0.5};
// How much evidence it takes to move a weight by about half.
constexpr double prior_precision = 4.0;
constexpr int learning_iterations = 6;
constexpr std::size_t learned_pair_limit = 400U;
constexpr std::size_t negatives_per_positive = 3U;

// Cards this many apart in creation order are hardly from the same thought.
constexpr double together_span = 5.0;
constexpr double direction_limit = 2.0;
constexpr double least_probability = 0.05;

double logistic(double logit) noexcept
{
  return 1.0 / (1.0 + std::exp(-logit));
}

// Words that appear on every card say nothing: tokens weigh by how rare they
// are (TF-IDF), and each card's vector has length 1.
class CardTexts
{
public:
  CardTexts(const BoardIndex& index, const std::vector<CardToken>& tokens) : cards_(index.size())
  {
    std::unordered_map<std::uint32_t, std::unordered_set<std::uint32_t>> cards_of;

    for (const auto& token : tokens)
    {
      const auto card = index.find(token.node);

      if (card != BoardIndex::unknown && std::isfinite(token.weight) && token.weight > 0.0)
      {
        cards_[card][token.token] += token.weight;
        cards_of[token.token].insert(card);
      }
    }

    for (auto& words : cards_)
    {
      auto squares = 0.0;

      for (auto& [token, weight] : words)
      {
        weight *= std::log(
          (1.0 + static_cast<double>(index.size()))
          / (1.0 + static_cast<double>(cards_of.at(token).size())));
        squares += weight * weight;
      }

      for (auto& [token, weight] : words)
      {
        weight = squares > 0.0 ? weight / std::sqrt(squares) : 0.0;
      }
    }
  }

  double similarity(std::size_t left, std::size_t right) const
  {
    const auto& shorter = cards_[left].size() < cards_[right].size() ? cards_[left] : cards_[right];
    const auto& longer = &shorter == &cards_[left] ? cards_[right] : cards_[left];
    auto product = 0.0;

    for (const auto& [token, weight] : shorter)
    {
      if (const auto found = longer.find(token); found != longer.end())
      {
        product += weight * found->second;
      }
    }

    return product;
  }

private:
  std::vector<std::unordered_map<std::uint32_t, double>> cards_;
};

// How often cards with one label wait for cards with another.
class LabelDirections
{
public:
  explicit LabelDirections(const BoardIndex& index)
  {
    for (std::size_t card = 0U; card < index.size(); ++card)
    {
      for (const auto dependency : index.dependencies(card))
      {
        for (const auto waiting : index.labels(card))
        {
          for (const auto first : index.labels(dependency))
          {
            counts_[key(waiting, first)] += 1.0;
          }
        }
      }
    }
  }

  // Above 0 when the card's labels usually wait for the other's.
  double direction(std::span<const std::uint32_t> card, std::span<const std::uint32_t> other) const
  {
    auto total = 0.0;

    for (const auto waiting : card)
    {
      for (const auto first : other)
      {
        const auto forward = count(waiting, first) + 1.0;
        // NOLINTNEXTLINE(readability-suspicious-call-argument): the other way round, on purpose.
        const auto backward = count(first, waiting) + 1.0;
        total += std::log(forward / backward);
      }
    }

    const auto pairs = static_cast<double>(card.size() * other.size());
    return pairs > 0.0 ? std::clamp(total / pairs, -direction_limit, direction_limit) : 0.0;
  }

private:
  static std::uint64_t key(std::uint32_t waiting, std::uint32_t first) noexcept
  {
    return (static_cast<std::uint64_t>(waiting) << 32U) | first;
  }

  double count(std::uint32_t waiting, std::uint32_t first) const
  {
    const auto found = counts_.find(key(waiting, first));
    return found == counts_.end() ? 0.0 : found->second;
  }

  std::unordered_map<std::uint64_t, double> counts_;
};

double shared_labels(std::span<const std::uint32_t> left, std::span<const std::uint32_t> right)
{
  std::size_t shared = 0U;

  for (const auto label : left)
  {
    shared += std::ranges::binary_search(right, label) ? 1U : 0U;
  }

  const auto all = left.size() + right.size() - shared;
  return all == 0U ? 0.0 : static_cast<double>(shared) / static_cast<double>(all);
}

// The most likely hints found so far. There are as many pairs as the square of
// the cards, so only a few times as many as asked for are ever held.
class BestHints
{
public:
  explicit BestHints(std::size_t limit) : limit_(limit)
  {
  }

  void add(const DependencyHint& hint)
  {
    hints_.push_back(hint);

    if (hints_.size() >= (held_per_hint * limit_) + held_at_least)
    {
      keep_best();
    }
  }

  std::vector<DependencyHint> take()
  {
    keep_best();
    return std::move(hints_);
  }

private:
  static constexpr std::size_t held_per_hint = 4U;
  static constexpr std::size_t held_at_least = 256U;

  void keep_best()
  {
    std::ranges::stable_sort(
      hints_,
      [](const DependencyHint& left, const DependencyHint& right)
      {
        return left.probability > right.probability;
      });
    hints_.resize(std::min(hints_.size(), limit_));
  }

  std::size_t limit_;
  std::vector<DependencyHint> hints_;
};

// What waits for each card.
IndexRows reversed(const BoardIndex& index)
{
  std::vector<std::pair<std::uint32_t, std::uint32_t>> pairs;

  for (std::uint32_t card = 0U; card < index.size(); ++card)
  {
    for (const auto dependency : index.dependencies(card))
    {
      pairs.emplace_back(dependency, card);
    }
  }

  return {index.size(), std::move(pairs)};
}

class HintSearch
{
public:
  HintSearch(const PlanningBoard& board, const std::vector<CardToken>& tokens)
      : index_(board), dependents_(reversed(index_)), texts_(index_, tokens), directions_(index_),
        weights_(prior_weights)
  {
    learn();
  }

  const BoardIndex& index() const noexcept
  {
    return index_;
  }

  // Adds what the card may wait for, and is not known to.
  void append(std::uint32_t card, BestHints& best) const
  {
    if (index_.item(card).is_completed)
    {
      return;
    }

    // What the card already waits for, and what waits for it: the first is
    // known, the second would close a loop.
    const auto below = reach(
      card,
      [this](std::uint32_t next)
      {
        return index_.dependencies(next);
      });
    const auto above = reach(
      card,
      [this](std::uint32_t next)
      {
        return dependents_.row(next);
      });

    for (std::uint32_t other = 0U; other < index_.size(); ++other)
    {
      if (other == card || index_.item(other).is_completed || below[other] != 0U
        || above[other] != 0U)
      {
        continue;
      }

      if (const auto probability = probability_of(signals_of(card, other));
        probability >= least_probability)
      {
        best.add(
          DependencyHint{
            .card = index_.item(card).node,
            .dependency = index_.item(other).node,
            .probability = probability
          });
      }
    }
  }

private:
  template <typename Next>
  std::vector<std::uint8_t> reach(std::uint32_t from, const Next& next) const
  {
    std::vector<std::uint8_t> seen(index_.size(), 0U);
    std::vector<std::uint32_t> pending{from};

    while (!pending.empty())
    {
      const auto card = pending.back();
      pending.pop_back();

      for (const auto other : next(card))
      {
        if (seen[other] == 0U)
        {
          seen[other] = 1U;
          pending.push_back(other);
        }
      }
    }

    return seen;
  }

  Signals signals_of(std::size_t card, std::size_t other) const
  {
    const auto& waiting = index_.item(card);
    const auto& first = index_.item(other);
    const auto gap =
      static_cast<double>(waiting.created_order) - static_cast<double>(first.created_order);
    auto before = 0.0;

    if (gap > 0.0 || gap < 0.0)
    {
      before = gap > 0.0 ? 1.0 : -1.0;
    }

    return Signals{
      texts_.similarity(card, other),
      shared_labels(index_.labels(card), index_.labels(other)),
      before,
      std::exp(-std::abs(gap) / together_span),
      directions_.direction(index_.labels(card), index_.labels(other)),
      (first.is_in_progress ? 1.0 : 0.0) - (waiting.is_in_progress ? 1.0 : 0.0)
    };
  }

  double probability_of(const Signals& signals) const
  {
    auto logit = base_logit;

    for (std::size_t signal = 0U; signal < signal_count; ++signal)
    {
      logit += weights_.at(signal) * signals.at(signal);
    }

    return logistic(logit);
  }

  struct Example
  {
    Signals signals{};
    bool is_dependency = false;
  };

  // Every known dependency, its reverse, which is known not to be one, and a
  // few pairs nobody linked.
  std::vector<Example> examples() const
  {
    std::vector<Example> output;
    SplitMix random(index_.size());

    for (std::uint32_t card = 0U; card < index_.size(); ++card)
    {
      for (const auto dependency : index_.dependencies(card))
      {
        if (output.size() >= learned_pair_limit * (2U + negatives_per_positive))
        {
          return output;
        }

        output.push_back(Example{.signals = signals_of(card, dependency), .is_dependency = true});
        // NOLINTNEXTLINE(readability-suspicious-call-argument): the reverse is known not to be one.
        output.push_back(Example{.signals = signals_of(dependency, card)});

        for (std::size_t negative = 0U; negative < negatives_per_positive; ++negative)
        {
          const auto other = random.below(index_.size());

          if (other != card && !std::ranges::binary_search(index_.dependencies(card), other))
          {
            output.push_back(Example{.signals = signals_of(card, other)});
          }
        }
      }
    }

    return output;
  }

  // Newton's method on the posterior, which is concave.
  void learn()
  {
    const auto known = examples();

    if (known.empty())
    {
      return;
    }

    for (int iteration = 0; iteration < learning_iterations; ++iteration)
    {
      std::vector<double> curvature(signal_count * signal_count, 0.0);
      std::array<double, signal_count> slope{};

      for (std::size_t signal = 0U; signal < signal_count; ++signal)
      {
        curvature[(signal * signal_count) + signal] = prior_precision;
        slope.at(signal) = prior_precision * (prior_weights.at(signal) - weights_.at(signal));
      }

      for (const auto& example : known)
      {
        const auto chance = probability_of(example.signals);
        const auto error = (example.is_dependency ? 1.0 : 0.0) - chance;

        for (std::size_t row = 0U; row < signal_count; ++row)
        {
          slope.at(row) += error * example.signals.at(row);

          for (std::size_t column = 0U; column < signal_count; ++column)
          {
            curvature[(row * signal_count) + column] +=
              chance * (1.0 - chance) * example.signals.at(row) * example.signals.at(column);
          }
        }
      }

      Cholesky(std::move(curvature), signal_count).solve(slope);

      for (std::size_t signal = 0U; signal < signal_count; ++signal)
      {
        weights_.at(signal) += slope.at(signal);
      }
    }
  }

  BoardIndex index_;
  IndexRows dependents_;
  CardTexts texts_;
  LabelDirections directions_;
  Signals weights_;
};
}

std::vector<DependencyHint> suggest_dependencies(
  const PlanningBoard& board,
  const std::vector<CardToken>& tokens,
  const NodeId* source,
  std::size_t limit)
{
  const HintSearch search(board, tokens);
  BestHints best(limit);

  if (source != nullptr)
  {
    if (const auto card = search.index().find(*source); card != BoardIndex::unknown)
    {
      search.append(card, best);
    }
  }
  else
  {
    for (std::uint32_t card = 0U; card < search.index().size(); ++card)
    {
      search.append(card, best);
    }
  }

  return best.take();
}
}
