#include "planning/sampling.h"

#include <algorithm>
#include <cmath>

namespace coordina::graph
{
namespace
{
constexpr std::uint64_t golden_gamma = 0x9e3779b97f4a7c15ULL;

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

double CardStreams::unit(std::size_t card, std::uint64_t run) const noexcept
{
  return to_unit(mix(keys_[card] ^ ((run + 1U) * golden_gamma)));
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

Triangular::Triangular(const SizeRange& range) noexcept
    : low_(range.low_hours), likely_(range.likely_hours), high_(range.high_hours)
{
  const auto span = high_ - low_;

  if (span > 0.0)
  {
    split_ = (likely_ - low_) / span;
    below_ = span * (likely_ - low_);
    above_ = span * (high_ - likely_);
  }
}

double Triangular::operator()(double unit) const noexcept
{
  if (!(high_ > low_))
  {
    return likely_;
  }

  return unit < split_ ? low_ + std::sqrt(unit * below_) : high_ - std::sqrt((1.0 - unit) * above_);
}

double Triangular::likely() const noexcept
{
  return likely_;
}
}
