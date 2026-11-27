#include "planning/duration_model.h"

#include "numeric/normal.h"

#include <algorithm>
#include <cmath>

namespace coordina::graph
{
namespace
{
// What no history removes: the next months are not the last ones.
constexpr double drift_spread = 0.12;

// Finding work on the way counts from the first cards found, against as many
// cards finished without finding any.
constexpr double growth_prior_cards = 10.0;
}

double shift_of(std::span<const EffectWeight> effects, std::span<const double> values) noexcept
{
  auto shift = 0.0;

  for (const auto& effect : effects)
  {
    shift += effect.weight * values[effect.effect];
  }

  return shift;
}

double OverrunTail::chance_beyond(double deviations, double sigma) const noexcept
{
  return deviations <= threshold ? 1.0 : std::exp(-(deviations - threshold) * sigma / excess);
}

std::uint32_t DurationModel::team_effect() const noexcept
{
  return static_cast<std::uint32_t>(prior_precision_.size() - 1U);
}

// Several people or labels share one effect between them.
void DurationModel::append_effects(
  const BoardIndex& index,
  std::size_t card,
  std::vector<EffectWeight>& output) const
{
  const auto append = [&output](std::span<const std::uint32_t> keys, const auto& effects)
  {
    for (const auto key : keys)
    {
      if (const auto found = effects.find(key); found != effects.end())
      {
        output.push_back(
          EffectWeight{.effect = found->second, .weight = 1.0 / static_cast<double>(keys.size())});
      }
    }
  };

  append(index.people(card), person_effects_);
  append(index.labels(card), label_effects_);
}

double DurationModel::mean_shift(
  CardSize size,
  std::span<const EffectWeight> effects) const noexcept
{
  const auto effect = size_effects_.at(size_index(size));
  return shift_of(effects, means_) + means_.back() + (effect == no_effect ? 0.0 : means_[effect]);
}

const OverrunTail& DurationModel::overruns() const noexcept
{
  return overruns_;
}

double DurationModel::overrun_chance(double deviations, double sigma) const noexcept
{
  const auto in_tail = overruns_.share * overruns_.chance_beyond(deviations, sigma);
  const auto in_body = (1.0 - overruns_.share) * normal_cdf(-deviations);
  return in_tail / (in_tail + in_body);
}

double DurationModel::mean_growth() const noexcept
{
  return static_cast<double>(growth_.discovered)
    / (growth_prior_cards + static_cast<double>(growth_.finished));
}

const std::array<double, card_size_count>& DurationModel::size_mix() const noexcept
{
  return size_mix_;
}

CardSize DurationModel::size_at(double unit) const noexcept
{
  auto below = 0.0;
  auto last = CardSize::m;

  for (std::size_t size = 1U; size < card_size_count; ++size)
  {
    if (size_mix_.at(size) <= 0.0)
    {
      continue;
    }

    below += size_mix_.at(size);
    last = static_cast<CardSize>(size);

    if (unit < below)
    {
      break;
    }
  }

  return last;
}

SharedDraw DurationModel::make_draw() const
{
  SharedDraw output;
  output.effects.assign(prior_precision_.size(), 0.0);
  return output;
}

void DurationModel::draw(std::uint64_t run, SharedDraw& output) const noexcept
{
  SplitMix random(seed_ ^ (run * 0x9e3779b97f4a7c15ULL));
  output.noise = std::sqrt(noise_rate_ / draw_gamma(noise_shape_, random));
  output.growth = growth_.discovered == 0U
    ? 0.0
    : draw_gamma(static_cast<double>(growth_.discovered), random)
      / (growth_prior_cards + static_cast<double>(growth_.finished));

  const auto team = output.effects.size() - 1U;

  for (std::size_t effect = 0U; effect < team; ++effect)
  {
    output.effects[effect] =
      normal_quantile(streams_.unit(run, RunStreams::even_dimensions + effect));
  }

  output.effects[team] = normal_quantile(streams_.unit(run, 0U));
  precision_.color(output.effects);

  for (std::size_t effect = 0U; effect < output.effects.size(); ++effect)
  {
    output.effects[effect] = means_[effect] + (output.noise * output.effects[effect]);
  }

  const auto shared =
    output.effects[team] + (drift_spread * normal_quantile(streams_.unit(run, 1U)));

  for (std::size_t size = 0U; size < card_size_count; ++size)
  {
    const auto effect = size_effects_.at(size);
    output.by_size.at(size) = shared + (effect == no_effect ? 0.0 : output.effects[effect]);
  }
}

const std::vector<DurationRow>& DurationModel::rows() const noexcept
{
  return rows_;
}

double DurationModel::rank_of(const DurationRow& row) const
{
  const auto shared = combine(row.effects);
  const auto own_variance = row.sigma * row.sigma * noise_rate_ / (noise_shape_ - 1.0);
  const auto sigma =
    std::sqrt((shared.sigma * shared.sigma) + own_variance + (drift_spread * drift_spread));
  const auto deviations = (row.log_ratio - shared.mean) / sigma;
  return ((1.0 - overruns_.share) * normal_cdf(deviations))
    + (overruns_.share * (1.0 - overruns_.chance_beyond(deviations, sigma)));
}

Prediction DurationModel::team_ratio() const
{
  const std::array effects{EffectWeight{.effect = team_effect(), .weight = 1.0}};
  return combine(effects);
}

Prediction DurationModel::size_ratio(CardSize size) const
{
  std::vector<EffectWeight> effects{EffectWeight{.effect = team_effect(), .weight = 1.0}};

  if (const auto effect = size_effects_.at(size_index(size)); effect != no_effect)
  {
    effects.push_back(EffectWeight{.effect = effect, .weight = 1.0});
  }

  return combine(effects);
}

Prediction DurationModel::label_ratio(std::uint32_t label) const
{
  std::vector<EffectWeight> effects{EffectWeight{.effect = team_effect(), .weight = 1.0}};

  if (const auto found = label_effects_.find(label); found != label_effects_.end())
  {
    effects.push_back(EffectWeight{.effect = found->second, .weight = 1.0});
  }

  return combine(effects);
}

const std::vector<std::uint32_t>& DurationModel::learned_labels() const noexcept
{
  return learned_labels_;
}

Prediction DurationModel::combine(std::span<const EffectWeight> effects) const
{
  std::vector<double> solved(prior_precision_.size(), 0.0);

  for (const auto& effect : effects)
  {
    solved[effect.effect] += effect.weight;
  }

  precision_.solve(solved);
  const auto variance =
    std::max(0.0, shift_of(effects, solved)) * noise_rate_ / (noise_shape_ - 1.0);
  return Prediction{.mean = shift_of(effects, means_), .sigma = std::sqrt(variance)};
}
}
