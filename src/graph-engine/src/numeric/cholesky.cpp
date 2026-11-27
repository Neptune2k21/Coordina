#include "numeric/cholesky.h"

#include <algorithm>
#include <cmath>
#include <utility>

namespace coordina::graph
{
namespace
{
constexpr double smallest_pivot = 1e-12;
}

Cholesky::Cholesky(std::vector<double> matrix, std::size_t size)
    : factor_(std::move(matrix)), size_(size)
{
  for (std::size_t row = 0U; row < size_; ++row)
  {
    for (std::size_t column = 0U; column <= row; ++column)
    {
      auto sum = factor_[(row * size_) + column];

      for (std::size_t index = 0U; index < column; ++index)
      {
        sum -= factor_[(row * size_) + index] * factor_[(column * size_) + index];
      }

      factor_[(row * size_) + column] = row == column ? std::sqrt(std::max(sum, smallest_pivot))
                                                      : sum / factor_[(column * size_) + column];
    }
  }
}

std::size_t Cholesky::size() const noexcept
{
  return size_;
}

void Cholesky::solve(std::span<double> values) const noexcept
{
  for (std::size_t row = 0U; row < size_; ++row)
  {
    auto sum = values[row];

    for (std::size_t column = 0U; column < row; ++column)
    {
      sum -= factor_[(row * size_) + column] * values[column];
    }

    values[row] = sum / factor_[(row * size_) + row];
  }

  color(values);
}

void Cholesky::color(std::span<double> values) const noexcept
{
  for (std::size_t row = size_; row-- > 0U;)
  {
    auto sum = values[row];

    for (std::size_t below = row + 1U; below < size_; ++below)
    {
      sum -= factor_[(below * size_) + row] * values[below];
    }

    values[row] = sum / factor_[(row * size_) + row];
  }
}
}
