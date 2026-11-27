#include "planning/plan_simulator.h"

#include "planning/parallel_runs.h"

#include <algorithm>

namespace coordina::graph
{
PlanSimulator::Runner::Runner(const PlanModel& model, const DurationSampler& sampler)
    : shared(sampler.make_draw()), hours(model.card_count(), 0.0), scheduler(model)
{
  chain.reserve(model.card_count());
}

PlanSimulator::PlanSimulator(
  const PlanModel& model,
  const DurationSampler& sampler,
  std::size_t milestone,
  double goal,
  std::uint32_t largest_run_count,
  std::uint32_t thread_count)
    : model_(&model), sampler_(&sampler), milestone_(milestone), goal_(goal),
      thread_count_(thread_count),
      contributions_(chunk_limit, std::vector<double>(model.card_count(), 0.0))
{
  const std::size_t runs = largest_run_count;
  finishes_.reserve(runs);
  runners_.assign(
    thread_count_for(thread_count, chunk_count_for(runs), runs * model.card_count()),
    Runner(model, sampler));
}

PlanSample PlanSimulator::run(
  std::size_t runs,
  const Cut& cut,
  const Staffing& staffing,
  std::vector<double>* contribution)
{
  const auto chunks = chunk_count_for(runs);
  const auto threads =
    std::min(runners_.size(), thread_count_for(thread_count_, chunks, runs * model_->card_count()));
  finishes_.assign(runs, 0.0);

  if (contribution != nullptr)
  {
    for (auto& chunk : contributions_)
    {
      std::ranges::fill(chunk, 0.0);
    }
  }

  run_chunks(
    std::span(runners_).first(threads),
    chunks,
    [&](Runner& runner, std::size_t chunk)
    {
      simulate(
        runner,
        cut,
        staffing,
        chunk_start(runs, chunks, chunk),
        chunk_start(runs, chunks, chunk + 1U),
        contribution == nullptr ? nullptr : &contributions_[chunk]);
    });

  if (contribution != nullptr)
  {
    contribution->assign(model_->card_count(), 0.0);

    for (const auto& chunk : contributions_)
    {
      for (std::size_t card = 0U; card < contribution->size(); ++card)
      {
        (*contribution)[card] += chunk[card] / static_cast<double>(runs);
      }
    }
  }

  const auto target = model_->milestones()[milestone_].target_hours;
  std::size_t on_time = 0U;
  auto total = 0.0;

  for (const auto finish : finishes_)
  {
    on_time += finish > target ? 0U : 1U;
    total += finish;
  }

  return PlanSample{
    .on_time = static_cast<double>(on_time) / static_cast<double>(runs),
    .goal_hours = quantile_of(finishes_, goal_),
    .mean_hours = total / static_cast<double>(runs),
    .p85_hours = quantile_of(finishes_, 0.85)
  };
}

void PlanSimulator::simulate(
  Runner& runner,
  const Cut& cut,
  const Staffing& staffing,
  std::size_t first,
  std::size_t end,
  std::vector<double>* contribution)
{
  for (auto run = first; run < end; ++run)
  {
    sampler_->draw(run, runner.shared, runner.hours, cut.active);
    runner.scheduler.run(runner.hours, cut, staffing);
    finishes_[run] = runner.scheduler.finish_of(milestone_);

    if (contribution != nullptr)
    {
      runner.scheduler.driving_chain(model_->milestones()[milestone_].card, runner.chain);

      for (const auto card : runner.chain)
      {
        (*contribution)[card] += runner.hours[card];
      }
    }
  }
}
}
