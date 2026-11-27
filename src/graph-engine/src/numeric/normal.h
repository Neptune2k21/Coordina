#ifndef COORDINA_GRAPH_ENGINE_NORMAL_H
#define COORDINA_GRAPH_ENGINE_NORMAL_H

namespace coordina::graph
{
// The standard normal value 90% of draws stay below.
constexpr double normal_p90 = 1.2815515655446004;

double normal_density(double value) noexcept;

double normal_cdf(double value) noexcept;

// The inverse of normal_cdf, to about 16 digits (Wichura's AS 241). The
// probability is clamped inside (0, 1), so the result is always finite.
double normal_quantile(double probability) noexcept;

// A standard normal draw that is above a threshold, from a uniform one. Far in
// the tail it uses the exponential the tail tends to, where the CDF rounds
// to 1.
double normal_quantile_above(double threshold, double unit) noexcept;

// The mean of a standard normal draw that is above a threshold.
double normal_mean_above(double threshold) noexcept;
}

#endif
