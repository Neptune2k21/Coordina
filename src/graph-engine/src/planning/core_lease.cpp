#include "planning/core_lease.h"

#include <algorithm>
#include <atomic>
#include <thread>

namespace coordina::graph
{
namespace
{
std::atomic<std::size_t>& free_cores() noexcept
{
  static std::atomic<std::size_t> cores{std::max(1U, std::thread::hardware_concurrency()) - 1U};
  return cores;
}
}

CoreLease::CoreLease(std::size_t wanted) noexcept
{
  auto& cores = free_cores();
  auto available = cores.load();
  count_ = std::min(wanted, available);

  while (!cores.compare_exchange_weak(available, available - count_))
  {
    count_ = std::min(wanted, available);
  }
}

CoreLease::~CoreLease()
{
  free_cores().fetch_add(count_);
}

std::size_t CoreLease::count() const noexcept
{
  return count_;
}
}
