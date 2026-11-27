#include "planning/questions.h"

#include "numeric/normal.h"
#include "planning/duration_sampler.h"
#include "planning/parallel_runs.h"
#include "planning/planned_board.h"
#include "planning/schedule.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <span>

namespace coordina::graph
{
namespace
{
// Splitting a card or asking what is left of it does not tell everything.
constexpr double partial_answer = 0.75;

// Below this a card's uncertainty is not worth anyone's time.
constexpr double least_share = 0.005;

// A card past its 85% duration is worth a question.
constexpr double overrun_share = 0.85;

constexpr std::size_t hint_candidate_limit = 12U;
constexpr double least_hint_probability = 0.15;
constexpr std::uint32_t hint_run_count = 256U;

struct SizeTotals
{
  std::array<double, card_size_count> sums{};
  std::array<std::uint32_t, card_size_count> counts{};
};

struct ChunkTotals
{
  explicit ChunkTotals(std::size_t card_count) : sizes(card_count)
  {
  }

  std::vector<SizeTotals> sizes;
};

struct Runner
{
  Runner(const PlanModel& model, const DurationSampler& sampler)
      : shared(sampler.make_draw()), hours(model.card_count(), 0.0), scheduler(model)
  {
  }

  SharedDraw shared;
  std::vector<double> hours;
  Scheduler scheduler;
};

class QuestionSearch
{
public:
  QuestionSearch(const PlannedBoard& planned, std::size_t milestone, const QuestionOptions& options)
      : planned_(&planned), model_(&planned.model()), options_(&options), milestone_(milestone),
        sampler_(planned.model(), planned.durations(), options.simulation.seed),
        forecast_(planned.forecast_now())
  {
    const auto& forecast = forecast_.milestones[milestone_];
    margin_ = forecast.p85_days - forecast.p50_days;
  }

  QuestionList rank(const std::vector<CardToken>& tokens, const std::vector<DependencyHint>& hints)
  {
    QuestionList result;
    result.margin_days = margin_;
    simulate();
    ask_sizes(result.questions);
    ask_about_cards(result.questions);
    ask_dependencies(tokens, hints, result.questions);
    std::ranges::stable_sort(
      result.questions,
      [](const Question& left, const Question& right)
      {
        return left.margin_days > right.margin_days;
      });
    result.questions.resize(std::min<std::size_t>(result.questions.size(), options_->limit));
    return result;
  }

private:
  // Where the date ends up, by the size each unsized card has in the run.
  void simulate()
  {
    const auto runs = static_cast<std::size_t>(options_->simulation.run_count);
    const auto chunks = chunk_count_for(runs);
    finishes_.assign(runs, 0.0);
    std::vector<ChunkTotals> totals(chunks, ChunkTotals(model_->card_count()));
    std::vector<Runner> runners(
      thread_count_for(options_->simulation.thread_count, chunks, runs * model_->card_count()),
      Runner(*model_, sampler_));
    run_chunks(
      std::span(runners),
      chunks,
      [&](Runner& runner, std::size_t chunk)
      {
        const auto end = chunk_start(runs, chunks, chunk + 1U);

        for (auto run = chunk_start(runs, chunks, chunk); run < end; ++run)
        {
          sampler_.draw(run, runner.shared, runner.hours);
          runner.scheduler.run(runner.hours);
          finishes_[run] = runner.scheduler.finish_of(milestone_) / focus_hours_per_day;
          add_sizes(run, totals[chunk]);
        }
      });

    sizes_ = std::move(totals.front().sizes);

    for (std::size_t chunk = 1U; chunk < chunks; ++chunk)
    {
      for (std::size_t card = 0U; card < sizes_.size(); ++card)
      {
        for (std::size_t size = 0U; size < card_size_count; ++size)
        {
          sizes_[card].sums.at(size) += totals[chunk].sizes[card].sums.at(size);
          sizes_[card].counts.at(size) += totals[chunk].sizes[card].counts.at(size);
        }
      }
    }

    auto total = 0.0;

    for (const auto finish : finishes_)
    {
      total += finish;
    }

    mean_ = total / static_cast<double>(runs);

    for (const auto finish : finishes_)
    {
      variance_ += (finish - mean_) * (finish - mean_) / static_cast<double>(runs);
    }
  }

  void add_sizes(std::size_t run, ChunkTotals& totals) const
  {
    for (std::size_t card = 0U; card < model_->card_count(); ++card)
    {
      if (is_asked(card) && model_->item(card).size == CardSize::none)
      {
        const auto size = static_cast<std::size_t>(sampler_.size_of(card, run));
        totals.sizes[card].sums.at(size) += finishes_[run];
        ++totals.sizes[card].counts.at(size);
      }
    }
  }

  bool is_asked(std::size_t card) const noexcept
  {
    return model_->in_scope(milestone_, card) && card != model_->milestones()[milestone_].card;
  }

  // What removing that share of the variance takes off the margin.
  double saved_days(double share) const noexcept
  {
    return margin_ * (1.0 - std::sqrt(1.0 - std::clamp(share, 0.0, 1.0)));
  }

  // The variance between the mean dates of each size, over the whole: what
  // knowing the size removes. The noise of those means adds to it, and is
  // taken off.
  double share_of_size(std::size_t card) const noexcept
  {
    const auto& totals = sizes_[card];
    const auto runs = static_cast<double>(finishes_.size());
    auto between = 0.0;
    auto groups = 0.0;

    for (std::size_t size = 1U; size < card_size_count; ++size)
    {
      if (totals.counts.at(size) > 0U)
      {
        const auto count = static_cast<double>(totals.counts.at(size));
        const auto offset = (totals.sums.at(size) / count) - mean_;
        between += count * offset * offset / runs;
        groups += 1.0;
      }
    }

    const auto noise = std::max(0.0, groups - 1.0) * variance_ / runs;
    return variance_ > 0.0 ? std::max(0.0, between - noise) / variance_ : 0.0;
  }

  void ask_sizes(std::vector<Question>& output) const
  {
    for (std::size_t card = 0U; card < model_->card_count(); ++card)
    {
      if (is_asked(card) && model_->item(card).size == CardSize::none)
      {
        add_card(QuestionKind::size_card, card, share_of_size(card), output);
      }
    }
  }

  void ask_about_cards(std::vector<Question>& output) const
  {
    for (const auto& forecast : forecast_.milestones[milestone_].cards)
    {
      const auto found = std::ranges::find(model_->nodes(), forecast.node);
      const auto card = static_cast<std::size_t>(found - model_->nodes().begin());

      if (!is_asked(card))
      {
        continue;
      }

      const auto share = forecast.sensitivity * forecast.sensitivity * partial_answer;

      if (model_->item(card).is_in_progress && is_overrunning(card))
      {
        add_card(QuestionKind::check_progress, card, share, output);
      }
      else if (!model_->item(card).is_in_progress
        && size_range(model_->item(card).size).should_split)
      {
        add_card(QuestionKind::split_card, card, share, output);
      }
    }
  }

  bool is_overrunning(std::size_t card) const
  {
    const auto& duration = model_->duration(card);

    if (!(duration.elapsed_hours > 0.0) || duration.size == CardSize::none)
    {
      return false;
    }

    const auto mean =
      duration.log_likely + planned_->durations().mean_shift(duration.size, model_->effects(card));
    return normal_cdf((std::log(duration.elapsed_hours) - mean) / duration.sigma) > overrun_share;
  }

  void add_card(QuestionKind kind, std::size_t card, double share, std::vector<Question>& output)
    const
  {
    if (share >= least_share)
    {
      output.push_back(
        Question{.kind = kind, .node = model_->nodes()[card], .margin_days = saved_days(share)});
    }
  }

  // The pairs to try: those given, then the engine's own.
  std::vector<DependencyHint> candidates(
    const std::vector<CardToken>& tokens,
    const std::vector<DependencyHint>& hints) const
  {
    auto all = hints;

    for (const auto& guess :
      suggest_dependencies(planned_->board(), tokens, nullptr, hint_candidate_limit * 8U))
    {
      const auto is_known = std::ranges::any_of(
        hints,
        [&guess](const DependencyHint& hint)
        {
          return hint.card == guess.card && hint.dependency == guess.dependency;
        });

      if (!is_known)
      {
        all.push_back(guess);
      }
    }

    std::erase_if(
      all,
      [this](const DependencyHint& hint)
      {
        const auto found = std::ranges::find(model_->nodes(), hint.card);
        return hint.probability < least_hint_probability || found == model_->nodes().end()
          || !model_->in_scope(
            milestone_,
            static_cast<std::size_t>(found - model_->nodes().begin()))
          || hint.card == hint.dependency;
      });
    std::ranges::stable_sort(
      all,
      [](const DependencyHint& left, const DependencyHint& right)
      {
        return left.probability > right.probability;
      });
    all.resize(std::min(all.size(), hint_candidate_limit));
    return all;
  }

  void ask_dependencies(
    const std::vector<CardToken>& tokens,
    const std::vector<DependencyHint>& hints,
    std::vector<Question>& output) const
  {
    auto simulation = options_->simulation;
    simulation.run_count = std::min(simulation.run_count, hint_run_count);
    const PlannedBoard quick(planned_->board(), simulation);
    const auto before = quick.forecast_now().milestones[milestone_].p50_days;

    for (const auto& hint : candidates(tokens, hints))
    {
      const auto after = quick.forecast_with(Edge{.source = hint.card, .target = hint.dependency});

      // A pair that would close a loop is not a dependency.
      if (after.error != PlanError::none)
      {
        continue;
      }

      const auto shift = after.milestones[milestone_].p50_days - before;
      const auto variance = hint.probability * (1.0 - hint.probability) * shift * shift;
      const auto share = variance_ + variance > 0.0 ? variance / (variance_ + variance) : 0.0;

      if (share >= least_share)
      {
        output.push_back(
          Question{
            .kind = QuestionKind::dependency,
            .node = hint.card,
            .other = hint.dependency,
            .probability = hint.probability,
            .margin_days = saved_days(share),
            .shift_days = shift
          });
      }
    }
  }

  const PlannedBoard* planned_;
  const PlanModel* model_;
  const QuestionOptions* options_;
  std::size_t milestone_;
  DurationSampler sampler_;
  Forecast forecast_;
  double margin_ = 0.0;
  double mean_ = 0.0;
  double variance_ = 0.0;
  std::vector<double> finishes_;
  std::vector<SizeTotals> sizes_;
};
}

QuestionList rank_questions(
  const PlanningBoard& board,
  const std::vector<CardToken>& tokens,
  const std::vector<DependencyHint>& hints,
  const QuestionOptions& options)
{
  const PlannedBoard planned(board, options.simulation);

  if (planned.error() != PlanError::none)
  {
    return QuestionList{.error = planned.error(), .margin_days = 0.0, .questions = {}};
  }

  const auto milestone = planned.find_milestone(options.milestone);

  if (milestone == PlannedBoard::no_milestone)
  {
    return QuestionList{.error = PlanError::unknown_milestone, .margin_days = 0.0, .questions = {}};
  }

  if (planned.model().milestones()[milestone].card == PlanMilestone::done)
  {
    return QuestionList{};
  }

  QuestionSearch search(planned, milestone, options);
  return search.rank(tokens, hints);
}
}
