#include "planning/card_size.h"

namespace coordina::graph
{
SizeRange size_range(CardSize size) noexcept
{
  constexpr double day = focus_hours_per_day;

  switch (size)
  {
    case CardSize::xs:
      return SizeRange{.low_hours = 0.25, .likely_hours = 0.5, .high_hours = 1.0};
    case CardSize::s:
      return SizeRange{.low_hours = 1.0, .likely_hours = 2.0, .high_hours = day / 2.0};
    case CardSize::m:
      return SizeRange{.low_hours = day / 2.0, .likely_hours = day, .high_hours = 2.0 * day};
    case CardSize::l:
      return SizeRange{.low_hours = 2.0 * day, .likely_hours = 3.0 * day, .high_hours = 4.0 * day};
    case CardSize::xl:
      return SizeRange{.low_hours = 4.0 * day, .likely_hours = 5.0 * day, .high_hours = 8.0 * day};
    case CardSize::xxl:
      return SizeRange{
        .low_hours = 5.0 * day,
        .likely_hours = 8.0 * day,
        .high_hours = 15.0 * day,
        .should_split = true
      };
    case CardSize::none:
      break;
  }

  return SizeRange{};
}

CardSize to_card_size(std::uint32_t value) noexcept
{
  return value <= static_cast<std::uint32_t>(CardSize::xxl) ? static_cast<CardSize>(value)
                                                            : CardSize::none;
}

bool fits_size_limit(CardSize size, CardSize max_size) noexcept
{
  return max_size == CardSize::none || (size != CardSize::none && size <= max_size);
}
}
