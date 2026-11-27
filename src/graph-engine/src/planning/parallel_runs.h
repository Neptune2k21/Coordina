#ifndef COORDINA_GRAPH_ENGINE_PARALLEL_RUNS_H
#define COORDINA_GRAPH_ENGINE_PARALLEL_RUNS_H

#include "planning/core_lease.h"

#include <algorithm>
#include <atomic>
#include <cstddef>
#include <cstdint>
#include <exception>
#include <mutex>
#include <span>
#include <system_error>
#include <thread>
#include <vector>

namespace coordina::graph
{
// Runs always split into the same chunks, whatever the thread count, and
// chunk totals add up in chunk order, so even the floating-point sums come out
// identical with one thread or sixteen.
constexpr std::size_t chunk_limit = 16U;

// Below this many card draws, starting threads costs more than it saves.
constexpr std::size_t parallel_draws = std::size_t{1U} << 16U;

inline std::size_t chunk_count_for(std::size_t runs) noexcept
{
  return std::min(chunk_limit, runs);
}

// The first run of a chunk; chunk + 1 gives its end.
inline std::size_t chunk_start(std::size_t runs, std::size_t chunks, std::size_t chunk) noexcept
{
  return runs * chunk / chunks;
}

// requested is 0 for every core.
inline std::size_t thread_count_for(std::uint32_t requested, std::size_t chunks, std::size_t draws)
{
  if (draws < parallel_draws)
  {
    return 1U;
  }

  const auto threads =
    requested == 0U ? std::max(1U, std::thread::hardware_concurrency()) : requested;
  return std::min<std::size_t>(threads, chunks);
}

// Calls work(runner, chunk) once for every chunk, with one thread per runner
// at most: fewer when other calls hold the cores, or when the system refuses a
// thread. The first exception stops the work and reaches the caller.
// Each runner holds the buffers of one thread, so the work allocates nothing.
template <typename Runner, typename Work>
void run_chunks(std::span<Runner> runners, std::size_t chunks, const Work& work)
{
  std::atomic<std::size_t> next_chunk{0U};
  std::exception_ptr failure;
  std::mutex failure_lock;

  const auto drain = [&](Runner& runner) noexcept
  {
    try
    {
      for (auto chunk = next_chunk.fetch_add(1U); chunk < chunks; chunk = next_chunk.fetch_add(1U))
      {
        work(runner, chunk);
      }
    }
    catch (...)
    {
      next_chunk.store(chunks);
      const std::scoped_lock guard(failure_lock);
      failure = failure ? failure : std::current_exception();
    }
  };

  {
    const CoreLease lease(std::min(runners.size(), chunks) - 1U);
    std::vector<std::jthread> helpers;
    helpers.reserve(lease.count());

    try
    {
      for (std::size_t helper = 1U; helper <= lease.count(); ++helper)
      {
        helpers.emplace_back(
          [&drain, &runner = runners[helper]]
          {
            drain(runner);
          });
      }
    }
    // NOLINTNEXTLINE(bugprone-empty-catch): the threads already started, and this one, do the work.
    catch (const std::system_error&)
    {
    }

    drain(runners.front());
  }

  if (failure)
  {
    std::rethrow_exception(failure);
  }
}
}

#endif
