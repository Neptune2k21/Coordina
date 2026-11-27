#include "planning/calibration.h"

#include "numeric/normal.h"
#include "planning/board_index.h"
#include "planning/duration_model.h"

#include <algorithm>
#include <cmath>
#include <span>
#include <unordered_map>

namespace coordina::graph
{
namespace
{
// A prediction from fewer cards says more about the prior than the board.
constexpr std::size_t first_replayed = 5U;
// Fitting again for every card would cost too much on a long history, and
// change little between two neighbours.
constexpr std::size_t replay_fit_limit = 32U;

DurationRatio to_ratio(const Prediction& prediction, std::uint32_t card_count)
{
  return DurationRatio{
    .low = std::exp(prediction.mean - (normal_p90 * prediction.sigma)),
    .median = std::exp(prediction.mean),
    .high = std::exp(prediction.mean + (normal_p90 * prediction.sigma)),
    .card_count = card_count
  };
}

std::vector<DurationRow> completed_rows(const DurationModel& model)
{
  std::vector<DurationRow> rows;

  for (const auto& row : model.rows())
  {
    if (!row.is_censored)
    {
      rows.push_back(row);
    }
  }

  return rows;
}

// The rows are oldest first: each is predicted from the ones before it, as if
// it had just been completed.
void replay(DurationModel model, const std::vector<DurationRow>& rows, Calibration& output)
{
  if (rows.size() <= first_replayed)
  {
    return;
  }

  const auto replayed = rows.size() - first_replayed;
  const auto step =
    std::max<std::size_t>(1U, (replayed + replay_fit_limit - 1U) / replay_fit_limit);
  std::size_t within_p50 = 0U;
  std::size_t within_p85 = 0U;

  for (auto next = first_replayed; next < rows.size(); ++next)
  {
    if ((next - first_replayed) % step == 0U)
    {
      model.refit(std::span(rows).first(next));
    }

    const auto rank = model.rank_of(rows[next]);
    within_p50 += rank <= 0.50 ? 1U : 0U;
    within_p85 += rank <= 0.85 ? 1U : 0U;
  }

  output.replayed_count = static_cast<std::uint32_t>(replayed);
  output.within_p50 = static_cast<double>(within_p50) / static_cast<double>(replayed);
  output.within_p85 = static_cast<double>(within_p85) / static_cast<double>(replayed);
}
}

Calibration calibrate(const PlanningBoard& board)
{
  const BoardIndex index(board);
  Calibration result;
  result.error = index.error();

  if (result.error != PlanError::none)
  {
    return result;
  }

  const DurationModel model(board, index, 0U);
  const auto rows = completed_rows(model);
  result.card_count = static_cast<std::uint32_t>(rows.size());
  result.team = to_ratio(model.team_ratio(), result.card_count);
  result.growth = model.mean_growth();

  std::array<std::uint32_t, card_size_count> by_size{};
  std::unordered_map<std::uint32_t, std::uint32_t> by_label;

  for (const auto& finished : board.history)
  {
    const auto card = index.find(finished.node);

    if (!DurationModel::learns_from(index, finished))
    {
      continue;
    }

    ++by_size.at(static_cast<std::size_t>(index.item(card).size));

    for (const auto label : index.labels(card))
    {
      ++by_label[label];
    }
  }

  for (std::size_t size = 1U; size < card_size_count; ++size)
  {
    result.sizes.at(size) =
      to_ratio(model.size_ratio(static_cast<CardSize>(size)), by_size.at(size));
  }

  for (const auto label : model.learned_labels())
  {
    result.labels.push_back(
      LabelRatio{.label = label, .ratio = to_ratio(model.label_ratio(label), by_label[label])});
  }

  replay(model, rows, result);
  return result;
}
}
