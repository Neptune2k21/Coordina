#include "planning/card_size.h"

#include "numeric/normal.h"

#include <cmath>

namespace coordina::graph
{
double SizeRange::low_hours() const noexcept
{
  return likely_hours / spread;
}

double SizeRange::high_hours() const noexcept
{
  return likely_hours * spread;
}

double SizeRange::sigma() const noexcept
{
  return std::log(spread) / normal_p90;
}

SizeRange size_range(CardSize size) noexcept
{
  constexpr double day = focus_hours_per_day;

  switch (size)
  {
    case CardSize::xs:
      return SizeRange{.likely_hours = 0.5, .spread = 2.0};
    case CardSize::s:
      return SizeRange{.likely_hours = 2.0, .spread = 2.0};
    case CardSize::m:
      return SizeRange{.likely_hours = day, .spread = 2.0};
    case CardSize::l:
      return SizeRange{.likely_hours = 3.0 * day, .spread = 1.5};
    case CardSize::xl:
      return SizeRange{.likely_hours = 5.0 * day, .spread = 1.5};
    case CardSize::xxl:
      return SizeRange{.likely_hours = 8.0 * day, .spread = 2.0, .should_split = true};
    case CardSize::none:
      break;
  }

  return SizeRange{};
}

SizeRange planned_range(CardSize size) noexcept
{
  return size_range(size == CardSize::none ? CardSize::m : size);
}

CardSize to_card_size(std::uint32_t value) noexcept
{
  return value < card_size_count ? static_cast<CardSize>(value) : CardSize::none;
}

bool fits_size_limit(CardSize size, CardSize max_size) noexcept
{
  return max_size == CardSize::none || (size != CardSize::none && size <= max_size);
}
}
