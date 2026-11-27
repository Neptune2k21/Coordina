#include "interop/abi_errors.h"

#include <array>
#include <cstdio>

namespace coordina::graph::abi
{
namespace
{
// A fixed buffer, so reporting that memory ran out needs none.
std::array<char, 256U>& error_buffer() noexcept
{
  thread_local std::array<char, 256U> buffer{};
  return buffer;
}
}

const char* last_error() noexcept
{
  return error_buffer().data();
}

void set_error(const char* message) noexcept
{
  auto& buffer = error_buffer();
  // NOLINTNEXTLINE(cppcoreguidelines-pro-type-vararg,cert-err33-c): bounded copy; a longer text is cut.
  std::snprintf(buffer.data(), buffer.size(), "%s", message);
}

coordina_graph_status invalid(const char* message) noexcept
{
  set_error(message);
  return COORDINA_GRAPH_INVALID_ARGUMENT;
}
}

extern "C" const char* coordina_graph_last_error(void)
{
  return coordina::graph::abi::last_error();
}
