#include "planning/duration_model.h"

#include "numeric/normal.h"

#include <algorithm>
#include <cmath>

// Learning a duration model: which effects the history can tell apart, what it
// says of each, and how often cards run away.
namespace coordina::graph
{
namespace
{
// How far off things can be before the board says anything, as the standard
// deviation of a logarithm: 0.3 is being off by a third or so.
constexpr double team_spread = 0.30;
constexpr double size_spread = 0.20;
constexpr double person_spread = 0.20;
constexpr double label_spread = 0.15;
// The noise variance, as a factor of the sizes' own, weighs as much as this
// many observations at first.
constexpr double prior_noise_shape = 8.0;
constexpr double prior_noise_rate = prior_noise_shape - 1.0;

// One card in fifty runs away, until this many cards have said otherwise.
constexpr double prior_overrun_cards = 50.0;
constexpr double prior_overruns = 1.0;
constexpr double prior_excess = 0.4;
constexpr double prior_excess_cards = 3.0;
// Nobody lets a card run forever: past this it is split or dropped.
constexpr double largest_excess = 1.2;

constexpr double half_life_days = 90.0;
// Beyond this many standard deviations an observation stops pulling harder.
constexpr double huber_limit = 2.0;
constexpr int fit_iterations = 8;

// A card done in under five minutes was moved, not worked on.
constexpr double shortest_hours = 5.0 / 60.0;

// Effects cost a square matrix: only the most observed ones are learned.
constexpr std::size_t learned_people_limit = 48U;
constexpr std::size_t learned_label_limit = 32U;

// What an unsized card is likely to be on a board without sized cards.
constexpr std::array<double, card_size_count>
  prior_size_counts{0.0, 0.5, 1.5, 1.75, 0.9, 0.35, 0.0};

// The keys seen most often, at most limit of them, in key order.
std::vector<std::uint32_t> most_seen(
  const std::unordered_map<std::uint32_t, std::uint32_t>& counts,
  std::size_t limit)
{
  std::vector<std::pair<std::uint32_t, std::uint32_t>> ranking;
  ranking.reserve(counts.size());

  for (const auto& [key, count] : counts)
  {
    ranking.emplace_back(count, key);
  }

  std::ranges::sort(
    ranking,
    [](const auto& left, const auto& right)
    {
      return left.first != right.first ? left.first > right.first : left.second < right.second;
    });
  ranking.resize(std::min(limit, ranking.size()));
  std::vector<std::uint32_t> keys;
  keys.reserve(ranking.size());

  for (const auto& [count, key] : ranking)
  {
    keys.push_back(key);
  }

  std::ranges::sort(keys);
  return keys;
}
}

DurationModel::DurationModel(
  const PlanningBoard& board,
  const BoardIndex& index,
  std::uint64_t seed)
    : growth_(board.growth), streams_(seed), seed_(seed)
{
  size_effects_.fill(no_effect);
  learn_size_mix(index);
  register_effects(board, index);
  collect_rows(board, index);
  refit(rows_);
}

bool DurationModel::learns_from(const BoardIndex& index, const FinishedCard& finished)
{
  const auto card = index.find(finished.node);
  return card != BoardIndex::unknown && index.item(card).size != CardSize::none
    && finished.hours >= shortest_hours;
}

void DurationModel::learn_size_mix(const BoardIndex& index)
{
  size_mix_ = prior_size_counts;

  for (std::size_t card = 0U; card < index.size(); ++card)
  {
    size_mix_.at(size_index(index.item(card).size)) += 1.0;
  }

  size_mix_.front() = 0.0;
  auto total = 0.0;

  for (const auto count : size_mix_)
  {
    total += count;
  }

  for (auto& share : size_mix_)
  {
    share /= total;
  }
}

// The team's effect comes last: Cholesky::color then draws it from a single
// dimension, the most evenly spread one.
void DurationModel::register_effects(const PlanningBoard& board, const BoardIndex& index)
{
  std::array<std::uint32_t, card_size_count> sizes{};
  std::unordered_map<std::uint32_t, std::uint32_t> people;
  std::unordered_map<std::uint32_t, std::uint32_t> labels;

  for (const auto& finished : board.history)
  {
    const auto card = index.find(finished.node);

    if (!learns_from(index, finished))
    {
      continue;
    }

    ++sizes.at(size_index(index.item(card).size));

    for (const auto person : index.people(card))
    {
      ++people[person];
    }

    for (const auto label : index.labels(card))
    {
      ++labels[label];
    }
  }

  for (std::size_t size = 1U; size < card_size_count; ++size)
  {
    if (sizes.at(size) > 0U)
    {
      size_effects_.at(size) = static_cast<std::uint32_t>(prior_precision_.size());
      prior_precision_.push_back(1.0 / (size_spread * size_spread));
    }
  }

  for (const auto person : most_seen(people, learned_people_limit))
  {
    person_effects_.emplace(person, static_cast<std::uint32_t>(prior_precision_.size()));
    prior_precision_.push_back(1.0 / (person_spread * person_spread));
  }

  learned_labels_ = most_seen(labels, learned_label_limit);

  for (const auto label : learned_labels_)
  {
    label_effects_.emplace(label, static_cast<std::uint32_t>(prior_precision_.size()));
    prior_precision_.push_back(1.0 / (label_spread * label_spread));
  }

  prior_precision_.push_back(1.0 / (team_spread * team_spread));
}

void DurationModel::collect_rows(const PlanningBoard& board, const BoardIndex& index)
{
  std::vector<FinishedCard> history = board.history;
  // Oldest first, as the calibration replays them.
  std::ranges::stable_sort(
    history,
    [](const FinishedCard& left, const FinishedCard& right)
    {
      return left.age_days > right.age_days;
    });

  for (const auto& finished : history)
  {
    const auto card = index.find(finished.node);

    if (!learns_from(index, finished))
    {
      continue;
    }

    auto row = row_of(index, card, finished.hours);
    row.weight = std::exp2(-finished.age_days / half_life_days);
    rows_.push_back(std::move(row));
  }

  for (std::size_t card = 0U; card < index.size(); ++card)
  {
    const auto& item = index.item(card);

    if (!item.is_completed && item.is_in_progress && item.size != CardSize::none
      && item.elapsed_hours >= shortest_hours)
    {
      auto row = row_of(index, card, item.elapsed_hours);
      row.is_censored = true;
      rows_.push_back(std::move(row));
    }
  }
}

DurationRow DurationModel::row_of(const BoardIndex& index, std::size_t card, double hours) const
{
  const auto size = index.item(card).size;
  const auto range = size_range(size);
  DurationRow row{
    .effects = {},
    .log_ratio = std::log(hours / range.likely_hours),
    .sigma = range.sigma()
  };
  append_effects(index, card, row.effects);

  if (const auto effect = size_effects_.at(size_index(size)); effect != no_effect)
  {
    row.effects.push_back(EffectWeight{.effect = effect, .weight = 1.0});
  }

  row.effects.push_back(EffectWeight{.effect = team_effect(), .weight = 1.0});
  return row;
}

// Weighted least squares with a Gaussian prior, repeated: each pass bounds the
// outliers and fills the cards still in progress from the pass before.
void DurationModel::refit(std::span<const DurationRow> rows)
{
  const auto size = prior_precision_.size();
  means_.assign(size, 0.0);
  noise_shape_ = prior_noise_shape;
  noise_rate_ = prior_noise_rate;

  for (int iteration = 0; iteration < fit_iterations; ++iteration)
  {
    std::vector<double> matrix(size * size, 0.0);
    std::vector<double> right(size, 0.0);
    auto squares = 0.0;
    auto observed = 0.0;

    for (std::size_t effect = 0U; effect < size; ++effect)
    {
      matrix[(effect * size) + effect] = prior_precision_[effect];
    }

    const auto noise = std::sqrt(noise_rate_ / (noise_shape_ - 1.0));

    for (const auto& row : rows)
    {
      const auto expected = shift_of(row.effects, means_);
      const auto sigma = noise * row.sigma;
      auto value = row.log_ratio;

      if (row.is_censored)
      {
        // Where the card is expected to end, given it is not done yet.
        value = expected + (sigma * normal_mean_above((row.log_ratio - expected) / sigma));
      }

      const auto distance = std::abs(value - expected) / sigma;
      const auto robust = distance <= huber_limit ? 1.0 : huber_limit / distance;
      const auto weight = row.weight * robust / (row.sigma * row.sigma);

      for (const auto& left : row.effects)
      {
        right[left.effect] += weight * left.weight * value;

        for (const auto& other : row.effects)
        {
          matrix[(left.effect * size) + other.effect] += weight * left.weight * other.weight;
        }
      }

      squares += weight * value * value;
      // A filled value says where the mean is, nothing about the noise.
      observed += row.is_censored ? 0.0 : row.weight * robust;
    }

    precision_ = Cholesky(std::move(matrix), size);
    auto means = right;
    precision_.solve(means);
    auto explained = 0.0;

    for (std::size_t effect = 0U; effect < size; ++effect)
    {
      explained += means[effect] * right[effect];
    }

    means_ = std::move(means);
    noise_shape_ = prior_noise_shape + (0.5 * observed);
    noise_rate_ = prior_noise_rate + std::max(0.0, 0.5 * (squares - explained));
  }

  learn_overruns(rows);
}

// Counts the cards past the threshold and how far past they go.
void DurationModel::learn_overruns(std::span<const DurationRow> rows)
{
  const auto noise = std::sqrt(noise_rate_ / (noise_shape_ - 1.0));
  auto observed = prior_overrun_cards;
  auto overruns = prior_overruns;
  auto excess = prior_excess * prior_excess_cards;
  auto excess_cards = prior_excess_cards;

  for (const auto& row : rows)
  {
    if (row.is_censored)
    {
      continue;
    }

    const auto beyond =
      row.log_ratio - shift_of(row.effects, means_) - (OverrunTail::threshold * noise * row.sigma);
    observed += row.weight;

    if (beyond > 0.0)
    {
      overruns += row.weight;
      excess += row.weight * beyond;
      excess_cards += row.weight;
    }
  }

  overruns_ = OverrunTail{
    .share = overruns / observed,
    .excess = std::min(excess / excess_cards, largest_excess)
  };
}
}
