#ifndef COORDINA_GRAPH_ENGINE_DURATION_MODEL_H
#define COORDINA_GRAPH_ENGINE_DURATION_MODEL_H

#include "numeric/cholesky.h"
#include "planning/board_index.h"
#include "planning/card_size.h"
#include "planning/planning_board.h"
#include "planning/sampling.h"

#include <array>
#include <cstddef>
#include <cstdint>
#include <limits>
#include <span>
#include <unordered_map>
#include <vector>

namespace coordina::graph
{
// What one run shares between cards: how far this team's sizes, people and
// labels are off in that run, how noisy its durations are, and how much work
// each card uncovers. The buffer belongs to one thread.
struct SharedDraw
{
  std::vector<double> effects;
  std::array<double, card_size_count> by_size{};
  double noise = 1.0;
  double growth = 0.0;
};

// An effect and how much of it applies to a card.
struct EffectWeight
{
  std::uint32_t effect = 0U;
  double weight = 0.0;
};

// What is known about an observed or planned card's duration.
struct DurationRow
{
  // Who and what it was: every effect that applies, the team's included.
  std::vector<EffectWeight> effects;
  // The logarithm of the hours over the size's likely hours.
  double log_ratio = 0.0;
  double sigma = 1.0;
  double weight = 1.0;
  // Still in progress: it takes at least this long.
  bool is_censored = false;
};

// Cards that run away from their size altogether. A log-normal has no room for
// them: past a threshold the logarithm of the duration has an exponential tail,
// which makes the duration itself a power law, as measured on IT projects.
struct OverrunTail
{
  // Where the tail starts, in standard deviations of a card's noise.
  static constexpr double threshold = 2.5;

  // The share of cards in the tail.
  double share = 0.0;
  // The mean distance past the threshold, in logarithm.
  double excess = 1.0;

  // The chance that a card in the tail is beyond that many standard deviations.
  double chance_beyond(double deviations, double sigma) const noexcept;
};

constexpr std::size_t size_index(CardSize size) noexcept
{
  return static_cast<std::size_t>(size);
}

// What the effects that apply to a card add up to.
double shift_of(std::span<const EffectWeight> effects, std::span<const double> values) noexcept;

// The mean and spread of a log ratio.
struct Prediction
{
  double mean = 0.0;
  double sigma = 1.0;
};

// How long cards really take on this board, learned from its completed cards.
//
// The logarithm of a duration is the size's, plus what the team, the size, the
// people and the labels add, plus noise. Those effects are Gaussian with a
// prior around zero and the noise variance follows an inverse gamma, so the
// posterior has a closed form: with no history the sizes mean what they say and
// the shared effects are wide; with history they move and narrow, gradually,
// and an effect seen on two cards hardly moves. A run draws the effects once
// for all its cards, so when the team is slow, every card is.
//
// Observations lose half their weight every 90 days, outliers are bounded
// (Huber), and cards still in progress count as "at least this long", without
// which only the quick ones would be learned from.
class DurationModel
{
public:
  static constexpr auto no_effect = std::numeric_limits<std::uint32_t>::max();

  DurationModel(const PlanningBoard& board, const BoardIndex& index, std::uint64_t seed);

  // The people and label effects that apply to a card, in a stable order.
  void append_effects(
    const BoardIndex& index,
    std::size_t card,
    std::vector<EffectWeight>& output) const;
  // The mean shift of the logarithm of a duration, before noise.
  double mean_shift(CardSize size, std::span<const EffectWeight> effects) const noexcept;
  double mean_growth() const noexcept;
  // The chance an unsized card has each size: the board's own mix.
  const std::array<double, card_size_count>& size_mix() const noexcept;
  const OverrunTail& overruns() const noexcept;
  // The chance a card that has already taken that long is in the tail.
  double overrun_chance(double deviations, double sigma) const noexcept;
  // The size at that point of the mix, from 0 to 1.
  CardSize size_at(double unit) const noexcept;

  SharedDraw make_draw() const;
  void draw(std::uint64_t run, SharedDraw& output) const noexcept;

  // Whether a completed card says how long its size takes: it has one, and it
  // was worked on rather than only moved.
  static bool learns_from(const BoardIndex& index, const FinishedCard& finished);

  // For the calibration report.
  const std::vector<DurationRow>& rows() const noexcept;
  // The chance a card like this one takes no longer than it did.
  double rank_of(const DurationRow& row) const;
  // The same model fitted on some of its rows only.
  void refit(std::span<const DurationRow> rows);
  // The log ratio of a team, size or label effect, team included: its mean
  // and standard deviation.
  Prediction team_ratio() const;
  Prediction size_ratio(CardSize size) const;
  Prediction label_ratio(std::uint32_t label) const;
  const std::vector<std::uint32_t>& learned_labels() const noexcept;

private:
  void learn_size_mix(const BoardIndex& index);
  void learn_overruns(std::span<const DurationRow> rows);
  void register_effects(const PlanningBoard& board, const BoardIndex& index);
  void collect_rows(const PlanningBoard& board, const BoardIndex& index);
  DurationRow row_of(const BoardIndex& index, std::size_t card, double hours) const;
  std::uint32_t team_effect() const noexcept;
  Prediction combine(std::span<const EffectWeight> effects) const;

  std::array<double, card_size_count> size_mix_{};
  std::array<std::uint32_t, card_size_count> size_effects_{};
  std::unordered_map<std::uint32_t, std::uint32_t> person_effects_;
  std::unordered_map<std::uint32_t, std::uint32_t> label_effects_;
  std::vector<std::uint32_t> learned_labels_;
  std::vector<DurationRow> rows_;
  std::vector<double> prior_precision_;
  std::vector<double> means_;
  Cholesky precision_;
  double noise_shape_ = 0.0;
  double noise_rate_ = 0.0;
  OverrunTail overruns_;
  ScopeGrowth growth_;
  RunStreams streams_;
  std::uint64_t seed_;
};
}

#endif
