#include "planning/forecast.h"

#include "planning/parallel_runs.h"
#include "planning/schedule.h"

#include <algorithm>
#include <cmath>
#include <span>

namespace coordina::graph
{
namespace
{
struct ChunkTotals
{
  explicit ChunkTotals(std::size_t card_count)
      : driving(card_count, 0U), sum_x(card_count, 0.0), sum_xx(card_count, 0.0),
        sum_xy(card_count, 0.0)
  {
  }

  std::vector<std::uint32_t> driving;
  std::vector<double> sum_x;
  std::vector<double> sum_xx;
  std::vector<double> sum_xy;
  double sum_y = 0.0;
  double sum_yy = 0.0;
};

// What one thread needs, allocated before it starts so the runs allocate
// nothing. Durations and finishes are summed as offsets from their likely
// values, which keeps the variance sums from cancelling out.
class ChunkRunner
{
public:
  ChunkRunner(const ForecastModel& model, const CardStreams& streams, double likely_finish)
      : model_(&model), streams_(&streams), likely_finish_(likely_finish),
        likely_(model.likely_hours()), hours_(model.card_count()), scheduler_(model)
  {
    chain_.reserve(model.card_count());
  }

  void run(std::size_t first, std::size_t end, ChunkTotals& totals, std::span<double> finishes)
  {
    const auto count = model_->card_count();

    for (auto run = first; run < end; ++run)
    {
      for (std::size_t card = 0U; card < count; ++card)
      {
        hours_[card] = model_->duration(card)(streams_->unit(card, run));
      }

      const auto finish = scheduler_.run(hours_);
      finishes[run] = finish;
      scheduler_.driving_chain(chain_);

      for (const auto card : chain_)
      {
        ++totals.driving[card];
      }

      const auto offset = finish - likely_finish_;
      totals.sum_y += offset;
      totals.sum_yy += offset * offset;

      for (std::size_t card = 0U; card < count; ++card)
      {
        const auto deviation = hours_[card] - likely_[card];
        totals.sum_x[card] += deviation;
        totals.sum_xx[card] += deviation * deviation;
        totals.sum_xy[card] += deviation * offset;
      }
    }
  }

private:
  const ForecastModel* model_;
  const CardStreams* streams_;
  double likely_finish_;
  std::vector<double> likely_;
  std::vector<double> hours_;
  std::vector<std::size_t> chain_;
  Scheduler scheduler_;
};

void simulate(
  const ForecastModel& model,
  const ForecastOptions& options,
  std::vector<ChunkTotals>& totals,
  std::vector<double>& finishes)
{
  const auto chunks = totals.size();
  const auto runs = static_cast<std::size_t>(options.run_count);
  const auto threads = thread_count_for(options.thread_count, chunks, runs * model.card_count());
  Scheduler likely(model);
  const auto likely_finish = likely.run(model.likely_hours());
  const CardStreams streams(options.seed, model.nodes());
  std::vector<ChunkRunner> runners(threads, ChunkRunner(model, streams, likely_finish));

  run_chunks(
    std::span(runners),
    chunks,
    [&](ChunkRunner& runner, std::size_t chunk)
    {
      runner.run(
        chunk_start(runs, chunks, chunk),
        chunk_start(runs, chunks, chunk + 1U),
        totals[chunk],
        finishes);
    });
}

double correlation(
  double runs,
  double sum_x,
  double sum_xx,
  double sum_y,
  double sum_yy,
  double sum_xy)
{
  const auto variance_x = (runs * sum_xx) - (sum_x * sum_x);
  const auto variance_y = (runs * sum_yy) - (sum_y * sum_y);

  if (!(variance_x > 0.0) || !(variance_y > 0.0))
  {
    return 0.0;
  }

  const auto value = ((runs * sum_xy) - (sum_x * sum_y)) / std::sqrt(variance_x * variance_y);
  return std::clamp(value, -1.0, 1.0);
}

// Nearest rank: the value below which percent of the runs finish.
std::size_t rank_of(std::size_t count, std::size_t percent) noexcept
{
  const auto rank = ((count * percent) + 99U) / 100U;
  return rank == 0U ? 0U : rank - 1U;
}
}

Forecast forecast(const ForecastModel& model, const ForecastOptions& options)
{
  Forecast result{};
  result.unsized_count = model.unsized_count();

  if (model.error() != ForecastError::none || options.run_count == 0U)
  {
    return result;
  }

  const auto cards = model.card_count();
  const auto runs = static_cast<std::size_t>(options.run_count);
  std::vector<ChunkTotals> totals(chunk_count_for(runs), ChunkTotals(cards));
  std::vector<double> finishes(runs, 0.0);
  simulate(model, options, totals, finishes);

  const auto p50 = finishes.begin() + static_cast<std::ptrdiff_t>(rank_of(runs, 50U));
  const auto p85 = finishes.begin() + static_cast<std::ptrdiff_t>(rank_of(runs, 85U));
  // The second partition reorders everything from the median on, so the
  // median is read before it.
  std::nth_element(finishes.begin(), p50, finishes.end());
  result.p50_days = *p50 / focus_hours_per_day;
  std::nth_element(p50, p85, finishes.end());
  result.p85_days = *p85 / focus_hours_per_day;

  auto merged = ChunkTotals(cards);

  for (const auto& chunk : totals)
  {
    merged.sum_y += chunk.sum_y;
    merged.sum_yy += chunk.sum_yy;

    for (std::size_t card = 0U; card < cards; ++card)
    {
      merged.driving[card] += chunk.driving[card];
      merged.sum_x[card] += chunk.sum_x[card];
      merged.sum_xx[card] += chunk.sum_xx[card];
      merged.sum_xy[card] += chunk.sum_xy[card];
    }
  }

  const auto run_total = static_cast<double>(runs);
  result.cards.reserve(cards);

  for (std::size_t card = 0U; card < cards; ++card)
  {
    result.cards.push_back(
      CardForecast{
        .node = model.nodes()[card],
        .criticality = static_cast<double>(merged.driving[card]) / run_total,
        .sensitivity = correlation(
          run_total,
          merged.sum_x[card],
          merged.sum_xx[card],
          merged.sum_y,
          merged.sum_yy,
          merged.sum_xy[card])
      });
  }

  std::ranges::stable_sort(
    result.cards,
    [](const auto& left, const auto& right)
    {
      return left.criticality > right.criticality;
    });

  return result;
}
}
