#include "planning/sampling.h"

#include "numeric/normal.h"

#include <algorithm>
#include <array>
#include <cmath>

namespace coordina::graph
{
namespace
{
constexpr std::uint64_t golden_gamma = 0x9e3779b97f4a7c15ULL;
constexpr std::uint64_t draw_gamma_step = 0xd1342543de82ef95ULL;

// The plastic number's inverse powers: the steps of the two-dimensional
// sequence that stays evenly spread however many points it has.
constexpr std::array<double, RunStreams::even_dimensions> even_steps{
  0.7548776662466927,
  0.5698402909980532
};

// The splitmix64 finalizer.
constexpr std::uint64_t mix(std::uint64_t value) noexcept
{
  value = (value ^ (value >> 30U)) * 0xbf58476d1ce4e5b9ULL;
  value = (value ^ (value >> 27U)) * 0x94d049bb133111ebULL;
  return value ^ (value >> 31U);
}

constexpr double to_unit(std::uint64_t bits) noexcept
{
  constexpr auto scale = 1.0 / static_cast<double>(std::uint64_t{1U} << 53U);
  return static_cast<double>(bits >> 11U) * scale;
}
}

CardStreams::CardStreams(std::uint64_t seed, const std::vector<NodeId>& cards)
{
  keys_.reserve(cards.size());

  for (const auto card : cards)
  {
    keys_.push_back(mix(mix(seed ^ card.high) ^ (card.low * golden_gamma)));
  }
}

double CardStreams::unit(std::size_t card, std::uint64_t run, std::uint64_t draw) const noexcept
{
  return to_unit(mix(keys_[card] ^ ((run + 1U) * golden_gamma) ^ (draw * draw_gamma_step)));
}

RunStreams::RunStreams(std::uint64_t seed) noexcept : seed_(mix(seed ^ golden_gamma))
{
}

double RunStreams::unit(std::uint64_t run, std::uint64_t dimension) const noexcept
{
  if (dimension < even_dimensions)
  {
    const auto start = to_unit(mix(seed_ + dimension));
    const auto value = start + (static_cast<double>(run + 1U) * even_steps.at(dimension));
    return value - std::floor(value);
  }

  return to_unit(mix(mix(seed_ ^ (dimension * draw_gamma_step)) ^ ((run + 1U) * golden_gamma)));
}

SplitMix::SplitMix(std::uint64_t seed) noexcept : state_(mix(seed))
{
}

double SplitMix::unit() noexcept
{
  state_ += golden_gamma;
  return to_unit(mix(state_));
}

std::size_t SplitMix::below(std::size_t bound) noexcept
{
  const auto drawn = static_cast<std::size_t>(unit() * static_cast<double>(bound));
  return std::min(drawn, bound - 1U);
}

double draw_gamma(double shape, SplitMix& random) noexcept
{
  const auto d = shape - (1.0 / 3.0);
  const auto c = 1.0 / std::sqrt(9.0 * d);

  for (;;)
  {
    const auto normal = normal_quantile(random.unit());
    const auto cube = 1.0 + (c * normal);

    if (cube <= 0.0)
    {
      continue;
    }

    const auto v = cube * cube * cube;
    const auto accept = std::log(std::max(random.unit(), 1e-300));

    if (accept < (0.5 * normal * normal) + d - (d * v) + (d * std::log(v)))
    {
      return d * v;
    }
  }
}
}
