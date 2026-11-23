#ifndef COORDINA_GRAPH_ENGINE_SAMPLING_H
#define COORDINA_GRAPH_ENGINE_SAMPLING_H

#include "core/graph_types.h"
#include "planning/card_size.h"

#include <cstddef>
#include <cstdint>
#include <vector>

namespace coordina::graph
{
// One random stream per card, keyed by the card's id rather than its place in
// the schedule: adding or reordering unrelated cards leaves every other
// card's draws untouched, so a forecast only moves when something it depends
// on changes, and two versions of a board can be compared run by run. A draw
// depends only on (seed, card, run), so threads can split the runs.
class CardStreams
{
public:
  CardStreams(std::uint64_t seed, const std::vector<NodeId>& cards);

  double unit(std::size_t card, std::uint64_t run) const noexcept;

private:
  std::vector<std::uint64_t> keys_;
};

// A fast stream of random numbers for searches (splitmix64): the same seed
// gives the same numbers on every machine.
class SplitMix
{
public:
  explicit SplitMix(std::uint64_t seed = 0U) noexcept;

  double unit() noexcept;
  // A number from 0 to bound - 1; bound is not 0.
  std::size_t below(std::size_t bound) noexcept;

private:
  std::uint64_t state_;
};

// The inverse of the triangular distribution's CDF over a size's range: the
// ranges lean high, so cards slip further than they come in early.
class Triangular
{
public:
  Triangular() = default;
  explicit Triangular(const SizeRange& range) noexcept;

  double operator()(double unit) const noexcept;
  double likely() const noexcept;

private:
  double low_ = 0.0;
  double likely_ = 0.0;
  double high_ = 0.0;
  double split_ = 0.0;
  double below_ = 0.0;
  double above_ = 0.0;
};
}

#endif
