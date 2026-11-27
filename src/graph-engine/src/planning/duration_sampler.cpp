#include "planning/duration_sampler.h"

#include "numeric/normal.h"

#include <algorithm>
#include <cmath>
#include <cstddef>

namespace coordina::graph
{
namespace
{
// The draws of a card's stream: each use has its own number, so changing one
// leaves the others as they were.
constexpr std::uint64_t duration_draw = 0U;
constexpr std::uint64_t size_draw = 1U;
constexpr std::uint64_t overrun_draw = 2U;
constexpr std::uint64_t growth_draw = 3U;
constexpr std::uint64_t found_draw = 4U;

// The most cards one card can uncover in a run.
constexpr int found_limit = 8;
}

DurationSampler::DurationSampler(
  const PlanModel& model,
  const DurationModel& durations,
  std::uint64_t seed)
    : model_(&model), durations_(&durations), streams_(seed, model.nodes())
{
  for (std::size_t size = 1U; size < card_size_count; ++size)
  {
    const auto range = size_range(static_cast<CardSize>(size));
    log_likely_.at(size) = std::log(range.likely_hours);
    sigma_.at(size) = range.sigma();
  }
}

SharedDraw DurationSampler::make_draw() const
{
  return durations_->make_draw();
}

void DurationSampler::draw(
  std::uint64_t run,
  SharedDraw& shared,
  std::span<double> hours,
  std::span<const std::uint8_t> active,
  std::span<double> own) const noexcept
{
  durations_->draw(run, shared);

  for (std::size_t card = 0U; card < hours.size(); ++card)
  {
    auto deviation = 0.0;
    hours[card] =
      active.empty() || active[card] != 0U ? draw_card(card, run, shared, deviation) : 0.0;

    if (!own.empty())
    {
      own[card] = deviation;
    }
  }
}

CardSize DurationSampler::size_of(std::size_t card, std::uint64_t run) const noexcept
{
  return durations_->size_at(streams_.unit(card, run, size_draw));
}

double DurationSampler::draw_card(
  std::size_t card,
  std::uint64_t run,
  const SharedDraw& shared,
  double& own) const noexcept
{
  const auto& duration = model_->duration(card);
  auto size = duration.size;
  auto mean = duration.log_likely;
  auto sigma = duration.sigma;

  if (size == CardSize::none)
  {
    size = size_of(card, run);
    mean = log_likely_.at(static_cast<std::size_t>(size));
    sigma = sigma_.at(static_cast<std::size_t>(size));
    own = mean - duration.log_likely;
  }

  mean += shared.by_size.at(static_cast<std::size_t>(size));
  sigma *= shared.noise;

  for (const auto& effect : model_->effects(card))
  {
    mean += effect.weight * shared.effects[effect.effect];
  }

  const auto extra = shared.growth > 0.0 ? draw_growth(card, run, shared) : 0.0;

  if (!(duration.elapsed_hours > 0.0))
  {
    const auto noise = draw_noise(card, run, sigma, 0.0, false);
    own += noise;
    return std::exp(mean + noise) + extra;
  }

  // Given it already took this long, what is left.
  const auto noise =
    draw_noise(card, run, sigma, (std::log(duration.elapsed_hours) - mean) / sigma, true);
  own += noise;
  return std::max(0.0, std::exp(mean + noise) - duration.elapsed_hours) + extra;
}

// What the card adds by itself to the logarithm of its duration. A card that
// has started is beyond the deviations it already took: the further past its
// usual range, the likelier it has run away, and then what is left does not
// shrink any more.
double DurationSampler::draw_noise(
  std::size_t card,
  std::uint64_t run,
  double sigma,
  double deviations,
  bool is_started) const noexcept
{
  const auto& tail = durations_->overruns();
  const auto chance = is_started ? durations_->overrun_chance(deviations, sigma) : tail.share;
  const auto overrun = streams_.unit(card, run, overrun_draw);

  if (overrun < chance)
  {
    const auto start =
      is_started ? std::max(deviations, OverrunTail::threshold) : OverrunTail::threshold;
    // The draw is uniform below the chance, so its ratio is uniform again.
    return (start * sigma) - (tail.excess * std::log1p(-overrun / chance));
  }

  const auto unit = streams_.unit(card, run, duration_draw);
  return sigma * (is_started ? normal_quantile_above(deviations, unit) : normal_quantile(unit));
}

// The cards this one uncovers, done along with it.
double DurationSampler::draw_growth(
  std::size_t card,
  std::uint64_t run,
  const SharedDraw& shared) const noexcept
{
  auto left = streams_.unit(card, run, growth_draw);
  auto chance = std::exp(-shared.growth);
  auto hours = 0.0;

  for (int found = 0; found < found_limit && left >= chance; ++found)
  {
    left -= chance;
    chance *= shared.growth / static_cast<double>(found + 1);
    const auto stream = found_draw + (2U * static_cast<std::uint64_t>(found));
    const auto size =
      static_cast<std::size_t>(durations_->size_at(streams_.unit(card, run, stream)));
    const auto normal = normal_quantile(streams_.unit(card, run, stream + 1U));
    hours += std::exp(
      log_likely_.at(size) + shared.by_size.at(size) + (sigma_.at(size) * shared.noise * normal));
  }

  return hours;
}
}
