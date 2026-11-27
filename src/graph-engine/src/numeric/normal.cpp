#include "numeric/normal.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <cstddef>
#include <limits>
#include <numbers>

namespace coordina::graph
{
namespace
{
// Beyond this threshold the tail is exponential to within a percent, and
// 1 - CDF starts losing digits.
constexpr double far_tail = 5.0;

// The rate of the exponential a far tail tends to, to the second order.
double tail_rate(double threshold) noexcept
{
  return threshold + (2.0 / threshold);
}

// The polynomial at x, its coefficients from the highest power down.
template <std::size_t Size>
double polynomial(const std::array<double, Size>& coefficients, double x) noexcept
{
  auto value = 0.0;

  for (const auto coefficient : coefficients)
  {
    value = (value * x) + coefficient;
  }

  return value;
}

double central_quantile(double offset) noexcept
{
  static constexpr std::array top{
    2509.0809287301226727,
    33430.575583588128105,
    67265.770927008700853,
    45921.953931549871457,
    13731.693765509461125,
    1971.5909503065514427,
    133.14166789178437745,
    3.387132872796366608
  };
  static constexpr std::array bottom{
    5226.495278852545925,
    28729.085735721942674,
    39307.89580009271061,
    21213.794301586595867,
    5394.1960214247511077,
    687.1870074920579083,
    42.313330701600911252,
    1.0
  };
  const auto x = 0.180625 - (offset * offset);
  return offset * polynomial(top, x) / polynomial(bottom, x);
}

double near_tail_quantile(double root) noexcept
{
  static constexpr std::array top{
    7.7454501427834140764e-4,
    0.0227238449892691845833,
    0.24178072517745061177,
    1.27045825245236838258,
    3.64784832476320460504,
    5.7694972214606914055,
    4.6303378461565452959,
    1.42343711074968357734
  };
  static constexpr std::array bottom{
    1.05075007164441684324e-9,
    5.475938084995344946e-4,
    0.0151986665636164571966,
    0.14810397642748007459,
    0.68976733498510000455,
    1.6763848301838038494,
    2.05319162663775882187,
    1.0
  };
  return polynomial(top, root - 1.6) / polynomial(bottom, root - 1.6);
}

double far_tail_quantile(double root) noexcept
{
  static constexpr std::array top{
    2.01033439929228813265e-7,
    2.71155556874348757815e-5,
    0.0012426609473880784386,
    0.026532189526576123093,
    0.29656057182850489123,
    1.7848265399172913358,
    5.4637849111641143699,
    6.6579046435011037772
  };
  static constexpr std::array bottom{
    2.04426310338993978564e-15,
    1.4215117583164458887e-7,
    1.8463183175100546818e-5,
    7.868691311456132591e-4,
    0.0148753612908506148525,
    0.13692988092273580531,
    0.59983220655588793769,
    1.0
  };
  return polynomial(top, root - 5.0) / polynomial(bottom, root - 5.0);
}
}

double normal_density(double value) noexcept
{
  return std::exp(-0.5 * value * value) * std::numbers::inv_sqrtpi / std::numbers::sqrt2;
}

double normal_cdf(double value) noexcept
{
  return 0.5 * std::erfc(-value / std::numbers::sqrt2);
}

double normal_quantile(double probability) noexcept
{
  constexpr auto smallest = std::numeric_limits<double>::min();
  constexpr auto largest = 1.0 - std::numeric_limits<double>::epsilon();
  const auto p = std::clamp(probability, smallest, largest);
  const auto offset = p - 0.5;

  if (std::abs(offset) <= 0.425)
  {
    return central_quantile(offset);
  }

  const auto root = std::sqrt(-std::log(offset < 0.0 ? p : 1.0 - p));
  const auto value = root <= 5.0 ? near_tail_quantile(root) : far_tail_quantile(root);
  return offset < 0.0 ? -value : value;
}

double normal_quantile_above(double threshold, double unit) noexcept
{
  if (threshold > far_tail)
  {
    return threshold - (std::log1p(-unit) / tail_rate(threshold));
  }

  const auto below = normal_cdf(threshold);
  return std::max(threshold, normal_quantile(below + ((1.0 - below) * unit)));
}

double normal_mean_above(double threshold) noexcept
{
  if (threshold > far_tail)
  {
    return threshold + (1.0 / tail_rate(threshold));
  }

  return normal_density(threshold) / (1.0 - normal_cdf(threshold));
}
}
