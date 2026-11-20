#include "planning/ready_set.h"

#include <bit>

namespace coordina::graph
{
namespace
{
constexpr std::size_t word_bits = 64U;

constexpr std::uint64_t bit(std::size_t index) noexcept
{
  return std::uint64_t{1U} << (index % word_bits);
}
}

ReadySet::ReadySet(std::size_t capacity)
    : leaves_((capacity + word_bits - 1U) / word_bits, 0U),
      summaries_((leaves_.size() + word_bits - 1U) / word_bits, 0U)
{
}

void ReadySet::insert(std::size_t index) noexcept
{
  auto& leaf = leaves_[index / word_bits];

  if ((leaf & bit(index)) != 0U)
  {
    return;
  }

  leaf |= bit(index);
  summaries_[index / word_bits / word_bits] |= bit(index / word_bits);
  ++count_;
}

void ReadySet::erase(std::size_t index) noexcept
{
  const auto leaf_index = index / word_bits;
  auto& leaf = leaves_[leaf_index];

  if ((leaf & bit(index)) == 0U)
  {
    return;
  }

  leaf &= ~bit(index);
  --count_;

  if (leaf == 0U)
  {
    summaries_[leaf_index / word_bits] &= ~bit(leaf_index);
  }
}

std::size_t ReadySet::min() const noexcept
{
  if (count_ == 0U)
  {
    return none;
  }

  for (std::size_t summary = 0U; summary < summaries_.size(); ++summary)
  {
    if (summaries_[summary] != 0U)
    {
      const auto leaf_index =
        (summary * word_bits) + static_cast<std::size_t>(std::countr_zero(summaries_[summary]));
      return (leaf_index * word_bits)
        + static_cast<std::size_t>(std::countr_zero(leaves_[leaf_index]));
    }
  }

  return none;
}

bool ReadySet::empty() const noexcept
{
  return count_ == 0U;
}
}
