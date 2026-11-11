#ifndef COORDINA_GRAPH_ENGINE_CARD_SIZE_H
#define COORDINA_GRAPH_ENGINE_CARD_SIZE_H

#include <cstdint>

namespace coordina::graph
{
// How big a card is. A size is an order of magnitude, not a duration: the
// bigger the card, the less anyone knows how long it takes.
enum class CardSize : std::uint8_t
{
  none = 0U,
  xs = 1U,
  s = 2U,
  m = 3U,
  l = 4U,
  xl = 5U,
  xxl = 6U
};

// Focus hours a person gives to cards in a working day.
constexpr double focus_hours_per_day = 6.0;

// The focus time a size stands for: the plausible extremes and the most
// likely value, which the simulation draws durations between.
struct SizeRange
{
  double low_hours = 0.0;
  double likely_hours = 0.0;
  double high_hours = 0.0;
  // Too big to plan reliably: the card should be split.
  bool should_split = false;
};

// The range of a size; none has an empty range.
SizeRange size_range(CardSize size) noexcept;

// Reads a size from the C ABI, where an unknown value counts as none.
CardSize to_card_size(std::uint32_t value) noexcept;

// Whether a card of this size fits in a focus slot of at most max_size.
// No limit (max_size none) fits everything; an unsized card fits no limit.
bool fits_size_limit(CardSize size, CardSize max_size) noexcept;
}

#endif
