#ifndef COORDINA_GRAPH_ENGINE_SAMPLING_H
#define COORDINA_GRAPH_ENGINE_SAMPLING_H

#include "core/graph_types.h"

#include <cstddef>
#include <cstdint>
#include <vector>

namespace coordina::graph
{
// One random stream per card, keyed by the card's id rather than its place in
// the schedule: adding or reordering unrelated cards leaves every other
// card's draws untouched, so a forecast only moves when something it depends
// on changes, and two versions of a board can be compared run by run. A draw
// depends only on (seed, card, run, draw), so threads can split the runs.
class CardStreams
{
public:
  CardStreams(std::uint64_t seed, const std::vector<NodeId>& cards);

  double unit(std::size_t card, std::uint64_t run, std::uint64_t draw = 0U) const noexcept;

private:
  std::vector<std::uint64_t> keys_;
};

// What a whole run shares, such as how far the team's estimates are off. One
// shared draw moves every card at once, so it makes most of the date's spread:
// the first two dimensions follow a low-discrepancy sequence, which covers it
// evenly from the first runs on, and any prefix of the runs is a fair sample.
class RunStreams
{
public:
  static constexpr std::uint64_t even_dimensions = 2U;

  explicit RunStreams(std::uint64_t seed) noexcept;

  double unit(std::uint64_t run, std::uint64_t dimension) const noexcept;

private:
  std::uint64_t seed_;
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

// A gamma draw of the given shape, at least 1, and scale 1 (Marsaglia and Tsang).
double draw_gamma(double shape, SplitMix& random) noexcept;
}

#endif
