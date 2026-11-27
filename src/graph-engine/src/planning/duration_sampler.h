#ifndef COORDINA_GRAPH_ENGINE_DURATION_SAMPLER_H
#define COORDINA_GRAPH_ENGINE_DURATION_SAMPLER_H

#include "planning/duration_model.h"
#include "planning/plan_model.h"
#include "planning/sampling.h"

#include <array>
#include <cstdint>
#include <span>
#include <vector>

namespace coordina::graph
{
// Draws how long each card of a plan takes in one run. Drawing allocates
// nothing and changes nothing but the caller's buffers, so threads share one
// sampler.
class DurationSampler
{
public:
  DurationSampler(const PlanModel& model, const DurationModel& durations, std::uint64_t seed);

  SharedDraw make_draw() const;
  // One duration per card, all from the same run. The flags, when not empty,
  // say which cards matter; the others get 0. own, when not empty, receives
  // what each card adds to the logarithm of its duration by itself, apart from
  // what the run shares.
  void draw(
    std::uint64_t run,
    SharedDraw& shared,
    std::span<double> hours,
    std::span<const std::uint8_t> active = {},
    std::span<double> own = {}) const noexcept;
  // The size an unsized card has in a run.
  CardSize size_of(std::size_t card, std::uint64_t run) const noexcept;

private:
  double draw_card(std::size_t card, std::uint64_t run, const SharedDraw& shared, double& own)
    const noexcept;
  double draw_noise(
    std::size_t card,
    std::uint64_t run,
    double sigma,
    double deviations,
    bool is_started) const noexcept;
  double draw_growth(std::size_t card, std::uint64_t run, const SharedDraw& shared) const noexcept;

  const PlanModel* model_;
  const DurationModel* durations_;
  CardStreams streams_;
  std::array<double, card_size_count> log_likely_{};
  std::array<double, card_size_count> sigma_{};
};
}

#endif
