#include "coordina_graph_engine.h"

#include "interop/abi_errors.h"
#include "interop/planning_bridge.h"
#include "planning/calibration.h"

#include <cstdint>

// The exports that advise: for now, what a board's history has taught.
namespace
{
namespace abi = coordina::graph::abi;

using coordina::graph::PlanError;

coordina_graph_duration_ratio to_abi(
  const coordina::graph::DurationRatio& ratio,
  std::uint32_t key) noexcept
{
  return coordina_graph_duration_ratio{
    .low = ratio.low,
    .median = ratio.median,
    .high = ratio.high,
    .key = key,
    .card_count = ratio.card_count
  };
}

coordina_graph_status write_calibration(
  const coordina::graph::Calibration& calibration,
  coordina_graph_calibration& output)
{
  output.size_count = calibration.sizes.size() - 1U;
  output.label_count = calibration.labels.size();

  if (!abi::fits(output.size_count, output.sizes, output.size_capacity)
    || !abi::fits(output.label_count, output.labels, output.label_capacity))
  {
    return COORDINA_GRAPH_INSUFFICIENT_CAPACITY;
  }

  for (std::uint32_t size = 1U; size < calibration.sizes.size(); ++size)
  {
    output.sizes[size - 1U] = to_abi(calibration.sizes.at(size), size);
  }

  for (std::size_t index = 0U; index < calibration.labels.size(); ++index)
  {
    output.labels[index] = to_abi(calibration.labels[index].ratio, calibration.labels[index].label);
  }

  output.team = to_abi(calibration.team, 0U);
  output.within_p50 = calibration.within_p50;
  output.within_p85 = calibration.within_p85;
  output.growth = calibration.growth;
  output.replayed_count = calibration.replayed_count;
  return COORDINA_GRAPH_OK;
}
}

extern "C" coordina_graph_status coordina_graph_calibrate(
  const coordina_graph_board* board,
  coordina_graph_calibration* output)
{
  return abi::guarded(
    [&]
    {
      if (const auto status = abi::check_board(board); status != COORDINA_GRAPH_OK)
      {
        return status;
      }

      if (output == nullptr)
      {
        return abi::invalid("An argument is null.");
      }

      const auto calibration = coordina::graph::calibrate(abi::build_board(*board));
      return calibration.error == PlanError::none ? write_calibration(calibration, *output)
                                                  : abi::to_status(calibration.error);
    });
}
