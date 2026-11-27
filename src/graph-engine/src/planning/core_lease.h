#ifndef COORDINA_GRAPH_ENGINE_CORE_LEASE_H
#define COORDINA_GRAPH_ENGINE_CORE_LEASE_H

#include <cstddef>

namespace coordina::graph
{
// Helper threads borrowed from a budget the whole process shares, one per core
// besides the caller's. When several requests simulate at once they split the
// cores instead of each starting one thread per core; results do not depend on
// how many they get.
class CoreLease
{
public:
  explicit CoreLease(std::size_t wanted) noexcept;
  ~CoreLease();

  CoreLease(const CoreLease&) = delete;
  CoreLease& operator=(const CoreLease&) = delete;
  CoreLease(CoreLease&&) = delete;
  CoreLease& operator=(CoreLease&&) = delete;

  std::size_t count() const noexcept;

private:
  std::size_t count_ = 0U;
};
}

#endif
