#ifndef COORDINA_GRAPH_ENGINE_ABI_ERRORS_H
#define COORDINA_GRAPH_ENGINE_ABI_ERRORS_H

#include "coordina_graph_engine.h"

#include <exception>
#include <new>

// No exception crosses the C ABI: each becomes a status, and its text what
// coordina_graph_last_error returns.
namespace coordina::graph::abi
{
const char* last_error() noexcept;
void set_error(const char* message) noexcept;

// Records why the call fails.
coordina_graph_status invalid(const char* message) noexcept;

// Runs an exported function that returns a status.
template <typename Call>
coordina_graph_status guarded(const Call& call) noexcept
{
  set_error("");

  try
  {
    return call();
  }
  catch (const std::bad_alloc&)
  {
    set_error("Out of memory.");
    return COORDINA_GRAPH_OUT_OF_MEMORY;
  }
  catch (const std::exception& error)
  {
    set_error(error.what());
  }
  catch (...)
  {
    set_error("Unknown error.");
  }

  return COORDINA_GRAPH_INTERNAL_ERROR;
}

// Runs an exported function that returns a value, failed when it throws.
template <typename Result, typename Call>
Result guarded_value(Result failed, const Call& call) noexcept
{
  auto result = failed;
  guarded(
    [&]
    {
      result = call();
      return COORDINA_GRAPH_OK;
    });
  return result;
}
}

#endif
