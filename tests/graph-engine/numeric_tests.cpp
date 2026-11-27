#include "numeric/cholesky.h"
#include "numeric/normal.h"
#include "planning/sampling.h"

#include <cassert>
#include <cmath>
#include <cstdint>
#include <limits>
#include <vector>

namespace
{
using namespace coordina::graph;

bool is_close(double left, double right, double tolerance)
{
  return std::abs(left - right) <= tolerance;
}

void inverts_the_normal_distribution()
{
  assert(is_close(normal_quantile(0.5), 0.0, 1e-15));
  assert(is_close(normal_quantile(0.9), normal_p90, 1e-12));
  assert(is_close(normal_quantile(0.975), 1.959963984540054, 1e-12));
  assert(is_close(normal_quantile(1e-10), -6.361340902404056, 1e-9));

  for (int step = 1; step < 1000; ++step)
  {
    const auto probability = step / 1000.0;
    assert(is_close(normal_cdf(normal_quantile(probability)), probability, 1e-12));
  }

  // The ends of the range are finite.
  assert(std::isfinite(normal_quantile(0.0)) && std::isfinite(normal_quantile(1.0)));
}

void draws_above_a_threshold()
{
  for (const auto threshold : {-3.0, 0.0, 2.0, 4.9, 5.1, 12.0, 60.0})
  {
    auto previous = threshold;

    for (const auto unit : {0.0, 0.1, 0.5, 0.9, 0.999})
    {
      const auto value = normal_quantile_above(threshold, unit);
      assert(std::isfinite(value) && value >= previous);
      previous = value;
    }

    const auto mean = normal_mean_above(threshold);
    assert(std::isfinite(mean) && mean > threshold);
  }

  // The tail's approximation meets the exact value where it takes over.
  assert(is_close(normal_quantile_above(4.999, 0.5), normal_quantile_above(5.001, 0.5), 0.01));
  assert(is_close(normal_mean_above(4.999), normal_mean_above(5.001), 0.01));
}

void solves_linear_systems()
{
  const Cholesky factor({4.0, 2.0, 0.6, 2.0, 5.0, 1.0, 0.6, 1.0, 3.0}, 3U);
  std::vector<double> values{1.0, 2.0, 3.0};
  factor.solve(values);

  assert(is_close((4.0 * values[0]) + (2.0 * values[1]) + (0.6 * values[2]), 1.0, 1e-12));
  assert(is_close((2.0 * values[0]) + (5.0 * values[1]) + (1.0 * values[2]), 2.0, 1e-12));
  assert(is_close((0.6 * values[0]) + (1.0 * values[1]) + (3.0 * values[2]), 3.0, 1e-12));

  // A matrix rounding has made singular still factors.
  const Cholesky singular({1.0, 1.0, 1.0, 1.0}, 2U);
  std::vector<double> right{1.0, 1.0};
  singular.solve(right);
  assert(std::isfinite(right[0]) && std::isfinite(right[1]));
}

void colors_draws_with_the_inverse_covariance()
{
  // The inverse of this matrix has 5/16 and 4/16 on its diagonal, -2/16 off it.
  const Cholesky factor({4.0, 2.0, 2.0, 5.0}, 2U);
  SplitMix random(7U);
  constexpr int draws = 200000;
  auto first = 0.0;
  auto second = 0.0;
  auto both = 0.0;

  for (int draw = 0; draw < draws; ++draw)
  {
    std::vector<double> values{normal_quantile(random.unit()), normal_quantile(random.unit())};
    factor.color(values);
    first += values[0] * values[0] / draws;
    second += values[1] * values[1] / draws;
    both += values[0] * values[1] / draws;
  }

  assert(is_close(first, 5.0 / 16.0, 0.01));
  assert(is_close(second, 4.0 / 16.0, 0.01));
  assert(is_close(both, -2.0 / 16.0, 0.01));
}

void draws_gamma_values()
{
  for (const auto shape : {1.0, 2.5, 8.0, 60.0})
  {
    SplitMix random(11U);
    constexpr int draws = 100000;
    auto mean = 0.0;
    auto square = 0.0;

    for (int draw = 0; draw < draws; ++draw)
    {
      const auto value = draw_gamma(shape, random);
      assert(value > 0.0);
      mean += value / draws;
      square += value * value / draws;
    }

    // A gamma's mean and variance are both its shape.
    assert(is_close(mean / shape, 1.0, 0.02));
    assert(is_close((square - (mean * mean)) / shape, 1.0, 0.04));
  }
}

void spreads_shared_draws_evenly_over_any_prefix()
{
  const RunStreams streams(42U);

  for (std::uint64_t dimension = 0U; dimension < RunStreams::even_dimensions; ++dimension)
  {
    for (const auto runs : {16U, 100U, 2000U})
    {
      auto mean = 0.0;

      for (std::uint64_t run = 0U; run < runs; ++run)
      {
        const auto unit = streams.unit(run, dimension);
        assert(unit >= 0.0 && unit < 1.0);
        mean += unit / runs;
      }

      // Independent draws would be off by 0.29 / sqrt(runs).
      assert(is_close(mean, 0.5, (1.5 / runs) + 0.02));
    }
  }
}
}

int main()
{
  inverts_the_normal_distribution();
  draws_above_a_threshold();
  solves_linear_systems();
  colors_draws_with_the_inverse_covariance();
  draws_gamma_values();
  spreads_shared_draws_evenly_over_any_prefix();

  return 0;
}
