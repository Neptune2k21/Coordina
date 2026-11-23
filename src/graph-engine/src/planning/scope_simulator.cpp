#include "planning/scope_simulator.h"

#include "planning/parallel_runs.h"

#include <algorithm>
#include <cmath>

namespace coordina::graph
{
namespace
{
// Nearest rank: the run below which that share of the runs finish.
std::size_t rank_at(std::size_t count, double share) noexcept
{
  const auto rank = static_cast<std::size_t>(std::ceil(static_cast<double>(count) * share));
  return std::clamp<std::size_t>(rank, 1U, count) - 1U;
}

double quantile(std::span<double> finishes, double share)
{
  const auto rank = finishes.begin() + static_cast<std::ptrdiff_t>(rank_at(finishes.size(), share));
  std::ranges::nth_element(finishes, rank);
  return *rank;
}
}

ScopeSimulator::Runner::Runner(const ForecastModel& model)
    : hours(model.card_count(), 0.0), scheduler(model)
{
  chain.reserve(model.card_count());
}

ScopeSimulator::Chunk::Chunk(std::size_t card_count) : contribution(card_count, 0.0)
{
}

ScopeSimulator::ScopeSimulator(const ForecastModel& model, const ScopeOptions& options)
    : model_(&model), streams_(options.seed, model.nodes()), target_hours_(options.target_hours),
      goal_(options.goal), thread_count_(options.thread_count),
      finishes_(std::max(options.run_count, options.screen_run_count), 0.0)
{
  const auto runs = finishes_.size();
  const auto threads =
    thread_count_for(thread_count_, chunk_count_for(runs), runs * model.card_count());
  runners_.assign(threads, Runner(model));
  chunks_.assign(chunk_limit, Chunk(model.card_count()));
}

ScopeSample ScopeSimulator::run(
  std::span<const std::uint8_t> active,
  std::size_t runs,
  std::vector<double>* contribution,
  const Staffing& staffing)
{
  const auto chunks = chunk_count_for(runs);
  const auto threads =
    std::min(runners_.size(), thread_count_for(thread_count_, chunks, runs * model_->card_count()));

  for (std::size_t chunk = 0U; chunk < chunks; ++chunk)
  {
    chunks_[chunk].on_time = 0U;

    if (contribution != nullptr)
    {
      std::ranges::fill(chunks_[chunk].contribution, 0.0);
    }
  }

  run_chunks(
    std::span(runners_).first(threads),
    chunks,
    [&](Runner& runner, std::size_t chunk)
    {
      simulate(
        runner,
        active,
        staffing,
        chunk_start(runs, chunks, chunk),
        chunk_start(runs, chunks, chunk + 1U),
        chunks_[chunk],
        contribution != nullptr);
    });

  return summarize(runs, chunks, contribution);
}

void ScopeSimulator::simulate(
  Runner& runner,
  std::span<const std::uint8_t> active,
  const Staffing& staffing,
  std::size_t first,
  std::size_t end,
  Chunk& chunk,
  bool with_contribution)
{
  const auto count = model_->card_count();

  for (auto run = first; run < end; ++run)
  {
    for (std::size_t card = 0U; card < count; ++card)
    {
      runner.hours[card] =
        active[card] == 0U ? 0.0 : model_->duration(card)(streams_.unit(card, run));
    }

    const auto finish = runner.scheduler.run(runner.hours, active, staffing);
    finishes_[run] = finish;
    chunk.on_time += finish > target_hours_ ? 0U : 1U;

    if (with_contribution)
    {
      runner.scheduler.driving_chain(runner.chain);

      for (const auto card : runner.chain)
      {
        chunk.contribution[card] += runner.hours[card];
      }
    }
  }
}

ScopeSample ScopeSimulator::summarize(
  std::size_t runs,
  std::size_t chunks,
  std::vector<double>* contribution)
{
  const auto run_total = static_cast<double>(runs);
  std::size_t on_time = 0U;

  if (contribution != nullptr)
  {
    contribution->assign(model_->card_count(), 0.0);
  }

  for (std::size_t chunk = 0U; chunk < chunks; ++chunk)
  {
    on_time += chunks_[chunk].on_time;

    if (contribution != nullptr)
    {
      for (std::size_t card = 0U; card < contribution->size(); ++card)
      {
        (*contribution)[card] += chunks_[chunk].contribution[card];
      }
    }
  }

  if (contribution != nullptr)
  {
    for (auto& hours : *contribution)
    {
      hours /= run_total;
    }
  }

  const auto finishes = std::span(finishes_).first(runs);
  return ScopeSample{
    .on_time = static_cast<double>(on_time) / run_total,
    .goal_hours = quantile(finishes, goal_),
    .p50_hours = quantile(finishes, 0.5),
    .p85_hours = quantile(finishes, 0.85)
  };
}
}
