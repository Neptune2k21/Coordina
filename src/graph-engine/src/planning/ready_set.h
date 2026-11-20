#ifndef COORDINA_GRAPH_ENGINE_READY_SET_H
#define COORDINA_GRAPH_ENGINE_READY_SET_H

#include <cstddef>
#include <cstdint>
#include <limits>
#include <vector>

namespace coordina::graph
{
// A set of card indexes that yields its smallest member in two bit scans: a
// summary word marks the non-empty leaf words below it, so neither insert,
// erase nor min ever walks the cards.
class ReadySet
{
public:
  static constexpr auto none = std::numeric_limits<std::size_t>::max();

  explicit ReadySet(std::size_t capacity = 0U);

  void insert(std::size_t index) noexcept;
  void erase(std::size_t index) noexcept;
  std::size_t min() const noexcept;
  bool empty() const noexcept;

private:
  std::vector<std::uint64_t> leaves_;
  std::vector<std::uint64_t> summaries_;
  std::size_t count_ = 0U;
};
}

#endif
