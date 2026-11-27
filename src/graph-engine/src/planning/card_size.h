#ifndef COORDINA_GRAPH_ENGINE_CARD_SIZE_H
#define COORDINA_GRAPH_ENGINE_CARD_SIZE_H

#include <cstddef>
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

// The sizes a card can have, none included.
constexpr std::size_t card_size_count = 7U;

// Focus hours a person gives to cards in a working day.
constexpr double focus_hours_per_day = 6.0;

// The focus time a size stands for: half the cards take longer than
// likely_hours, and eight in ten stay within a factor of spread of it. A
// factor rather than a margin: a card can take three times its likely time,
// never three times less than nothing, so slipping costs more than finishing
// early saves.
struct SizeRange
{
  double likely_hours = 0.0;
  double spread = 1.0;
  // Too big to plan reliably: the card should be split.
  bool should_split = false;

  double low_hours() const noexcept;
  double high_hours() const noexcept;
  // The standard deviation of the duration's logarithm.
  double sigma() const noexcept;
};

// The range of a size; none has an empty range.
SizeRange size_range(CardSize size) noexcept;

// The range that stands for a card in rankings and critical paths, where a
// single number is enough: an unsized card counts as an M.
SizeRange planned_range(CardSize size) noexcept;

// Reads a size from the C ABI, where an unknown value counts as none.
CardSize to_card_size(std::uint32_t value) noexcept;

// Whether a card of this size fits in a focus slot of at most max_size.
// No limit (max_size none) fits everything; an unsized card fits no limit.
bool fits_size_limit(CardSize size, CardSize max_size) noexcept;
}

#endif
