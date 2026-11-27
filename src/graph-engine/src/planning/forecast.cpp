#include "planning/forecast.h"

#include "planning/duration_sampler.h"
#include "planning/parallel_runs.h"
#include "planning/schedule.h"

#include <algorithm>
#include <cmath>
#include <span>

namespace coordina::graph
{
namespace
{
// Finishing at the target, give or take a rounding error, is on time.
constexpr double on_time_tolerance = 1e-9;

// Enough for the median of a bound that only informs.
constexpr std::size_t chain_run_limit = 256U;

// The cards of each milestone's scope, in pick-up order.
using Scopes = std::vector<std::vector<std::uint32_t>>;

Scopes scopes_of(const PlanModel& model)
{
  Scopes scopes(model.milestones().size());

  for (std::size_t milestone = 0U; milestone < scopes.size(); ++milestone)
  {
    for (std::uint32_t card = 0U; card < model.card_count(); ++card)
    {
      if (model.in_scope(milestone, card))
      {
        scopes[milestone].push_back(card);
      }
    }
  }

  return scopes;
}

// What the runs of a chunk add up to for one milestone. Finishes are summed as
// offsets from their planned values, which keeps the variance sums from
// cancelling out.
struct MilestoneTotals
{
  MilestoneTotals(std::size_t card_count, std::size_t scope_size)
      : driving(card_count, 0U), sum_xy(scope_size, 0.0)
  {
  }

  void add(const MilestoneTotals& other)
  {
    sum_y += other.sum_y;
    sum_yy += other.sum_yy;

    for (std::size_t card = 0U; card < driving.size(); ++card)
    {
      driving[card] += other.driving[card];
    }

    for (std::size_t place = 0U; place < sum_xy.size(); ++place)
    {
      sum_xy[place] += other.sum_xy[place];
    }
  }

  std::vector<std::uint32_t> driving;
  // By place in the milestone's scope.
  std::vector<double> sum_xy;
  double sum_y = 0.0;
  double sum_yy = 0.0;
};

struct ChunkTotals
{
  ChunkTotals(std::size_t card_count, const Scopes& scopes)
      : sum_x(card_count, 0.0), sum_xx(card_count, 0.0)
  {
    milestones.reserve(scopes.size());

    for (const auto& scope : scopes)
    {
      milestones.emplace_back(card_count, scope.size());
    }
  }

  void add(const ChunkTotals& other)
  {
    for (std::size_t card = 0U; card < sum_x.size(); ++card)
    {
      sum_x[card] += other.sum_x[card];
      sum_xx[card] += other.sum_xx[card];
    }

    for (std::size_t milestone = 0U; milestone < milestones.size(); ++milestone)
    {
      milestones[milestone].add(other.milestones[milestone]);
    }
  }

  std::vector<double> sum_x;
  std::vector<double> sum_xx;
  std::vector<MilestoneTotals> milestones;
};

// Per milestone, one value per run.
struct RunResults
{
  std::vector<std::vector<double>> finishes;
  std::vector<std::vector<double>> chains;
};

// What one thread needs, allocated before it starts so the runs allocate
// nothing.
class Runner
{
public:
  Runner(
    const PlanModel& model,
    const DurationSampler& sampler,
    const Scopes& scopes,
    const std::vector<double>& planned)
      : model_(&model), sampler_(&sampler), scopes_(&scopes), planned_(&planned),
        shared_(sampler.make_draw()), hours_(model.card_count()), own_(model.card_count()),
        ends_(model.card_count()), scheduler_(model)
  {
    chain_.reserve(model.card_count());
  }

  void run(std::size_t first, std::size_t end, ChunkTotals& totals, RunResults& results)
  {
    for (auto run = first; run < end; ++run)
    {
      sampler_->draw(run, shared_, hours_, {}, own_);
      scheduler_.run(hours_);
      add_durations(totals);

      for (std::size_t milestone = 0U; milestone < model_->milestones().size(); ++milestone)
      {
        results.finishes[milestone][run] = scheduler_.finish_of(milestone);
        add_milestone(milestone, totals.milestones[milestone]);
      }

      if (run < chain_run_limit)
      {
        measure_chains(run, results);
      }
    }
  }

private:
  void add_durations(ChunkTotals& totals) const
  {
    for (std::size_t card = 0U; card < own_.size(); ++card)
    {
      totals.sum_x[card] += own_[card];
      totals.sum_xx[card] += own_[card] * own_[card];
    }
  }

  void add_milestone(std::size_t milestone, MilestoneTotals& totals)
  {
    const auto card = model_->milestones()[milestone].card;

    if (card == PlanMilestone::done)
    {
      return;
    }

    scheduler_.driving_chain(card, chain_);

    for (const auto driving : chain_)
    {
      ++totals.driving[driving];
    }

    const auto offset = scheduler_.finish_of(milestone) - (*planned_)[milestone];
    totals.sum_y += offset;
    totals.sum_yy += offset * offset;

    const auto& scope = (*scopes_)[milestone];

    for (std::size_t place = 0U; place < scope.size(); ++place)
    {
      totals.sum_xy[place] += own_[scope[place]] * offset;
    }
  }

  // With enough people nothing waits for anyone: each card ends when its
  // chain does. Dependencies may come later in the pick-up order when a
  // card in progress waits for them, so each is followed down.
  void measure_chains(std::size_t run, RunResults& results)
  {
    std::ranges::fill(ends_, -1.0);

    for (std::size_t milestone = 0U; milestone < model_->milestones().size(); ++milestone)
    {
      const auto card = model_->milestones()[milestone].card;
      results.chains[milestone][run] = card == PlanMilestone::done ? 0.0 : chain_end(card);
    }
  }

  double chain_end(std::size_t card)
  {
    chain_.clear();
    chain_.push_back(card);

    while (!chain_.empty())
    {
      const auto next = chain_.back();
      auto start = 0.0;
      auto is_ready = true;

      for (const auto dependency : model_->dependencies(next))
      {
        if (ends_[dependency] < 0.0)
        {
          chain_.push_back(dependency);
          is_ready = false;
        }
        else
        {
          start = std::max(start, ends_[dependency]);
        }
      }

      if (is_ready)
      {
        ends_[next] = start + hours_[next];
        chain_.pop_back();
      }
    }

    return ends_[card];
  }

  const PlanModel* model_;
  const DurationSampler* sampler_;
  const Scopes* scopes_;
  const std::vector<double>* planned_;
  SharedDraw shared_;
  std::vector<double> hours_;
  std::vector<double> own_;
  std::vector<double> ends_;
  std::vector<std::size_t> chain_;
  Scheduler scheduler_;
};

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

void read_finishes(
  const PlanMilestone& milestone,
  std::vector<double>& finishes,
  MilestoneForecast& output)
{
  auto late = 0.0;
  std::size_t on_time = 0U;

  for (const auto finish : finishes)
  {
    late += std::max(0.0, finish - milestone.target_hours);
    on_time += finish > milestone.target_hours + on_time_tolerance ? 0U : 1U;
  }

  const auto runs = static_cast<double>(finishes.size());
  output.on_time = static_cast<double>(on_time) / runs;
  output.late_days = late / runs / focus_hours_per_day;
  output.p10_days = quantile_of(finishes, 0.10) / focus_hours_per_day;
  output.p50_days = quantile_of(finishes, 0.50) / focus_hours_per_day;
  output.p85_days = quantile_of(finishes, 0.85) / focus_hours_per_day;
  output.p95_days = quantile_of(finishes, 0.95) / focus_hours_per_day;
}

void read_cards(
  const PlanModel& model,
  std::span<const std::uint32_t> scope,
  const ChunkTotals& totals,
  const MilestoneTotals& own,
  double runs,
  MilestoneForecast& output)
{
  output.cards.reserve(scope.size());

  for (std::size_t place = 0U; place < scope.size(); ++place)
  {
    const auto card = scope[place];
    output.cards.push_back(
      CardForecast{
        .node = model.nodes()[card],
        .criticality = static_cast<double>(own.driving[card]) / runs,
        .sensitivity = correlation(
          runs,
          totals.sum_x[card],
          totals.sum_xx[card],
          own.sum_y,
          own.sum_yy,
          own.sum_xy[place])
      });
  }

  std::ranges::stable_sort(
    output.cards,
    [](const auto& left, const auto& right)
    {
      return left.criticality > right.criticality;
    });
}

std::vector<PlannedCard> read_plan(const PlanModel& model, const Schedule& schedule)
{
  std::vector<PlannedCard> plan;

  for (std::size_t card = 0U; card < model.card_count(); ++card)
  {
    if (!model.is_outside_scopes(card))
    {
      const auto& scheduled = schedule.cards[card];
      plan.push_back(
        PlannedCard{
          .node = scheduled.node,
          .person = scheduled.person,
          .start_days = scheduled.start_hours / focus_hours_per_day,
          .finish_days = scheduled.finish_hours / focus_hours_per_day
        });
    }
  }

  return plan;
}
}

double quantile_of(std::vector<double>& values, double share)
{
  const auto count = values.size();
  const auto rank = static_cast<std::size_t>(std::ceil(static_cast<double>(count) * share));
  const auto position =
    values.begin() + static_cast<std::ptrdiff_t>(std::clamp<std::size_t>(rank, 1U, count) - 1U);
  std::ranges::nth_element(values, position);
  return *position;
}

Forecast forecast(
  const PlanModel& model,
  const DurationModel& durations,
  const SimulationOptions& options)
{
  Forecast result;
  result.error = model.error();

  if (model.error() != PlanError::none || options.run_count == 0U)
  {
    return result;
  }

  Scheduler planner(model);
  planner.run(model.planned_hours());
  const auto milestone_count = model.milestones().size();
  std::vector<double> planned;
  planned.reserve(milestone_count);

  for (std::size_t milestone = 0U; milestone < milestone_count; ++milestone)
  {
    planned.push_back(planner.finish_of(milestone));
  }

  result.plan = read_plan(model, planner.schedule());

  const auto runs = static_cast<std::size_t>(options.run_count);
  const auto chunks = chunk_count_for(runs);
  const DurationSampler sampler(model, durations, options.seed);
  RunResults results{
    .finishes = {milestone_count, std::vector<double>(runs, 0.0)},
    .chains = {milestone_count, std::vector<double>(std::min(runs, chain_run_limit), 0.0)}
  };
  const auto scopes = scopes_of(model);
  std::vector<ChunkTotals> totals(chunks, ChunkTotals(model.card_count(), scopes));
  std::vector<Runner> runners(
    thread_count_for(options.thread_count, chunks, runs * model.card_count()),
    Runner(model, sampler, scopes, planned));
  run_chunks(
    std::span(runners),
    chunks,
    [&](Runner& runner, std::size_t chunk)
    {
      runner.run(
        chunk_start(runs, chunks, chunk),
        chunk_start(runs, chunks, chunk + 1U),
        totals[chunk],
        results);
    });

  for (std::size_t chunk = 1U; chunk < chunks; ++chunk)
  {
    totals.front().add(totals[chunk]);
  }

  for (std::size_t milestone = 0U; milestone < milestone_count; ++milestone)
  {
    const auto& planned_milestone = model.milestones()[milestone];
    MilestoneForecast output{
      .node = planned_milestone.node,
      .card_count = planned_milestone.card_count,
      .unsized_count = planned_milestone.unsized_count,
      .cards = {}
    };
    read_finishes(planned_milestone, results.finishes[milestone], output);
    output.chain_days = quantile_of(results.chains[milestone], 0.5) / focus_hours_per_day;
    read_cards(
      model,
      scopes[milestone],
      totals.front(),
      totals.front().milestones[milestone],
      static_cast<double>(runs),
      output);
    result.milestones.push_back(std::move(output));
  }

  return result;
}

Forecast forecast(const PlanningBoard& board, const SimulationOptions& options)
{
  const BoardIndex index(board);

  if (index.error() != PlanError::none)
  {
    return Forecast{.error = index.error(), .milestones = {}, .plan = {}};
  }

  const DurationModel durations(board, index, options.seed);
  return forecast(PlanModel(board, index, durations), durations, options);
}
}
