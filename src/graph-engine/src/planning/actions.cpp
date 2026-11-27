#include "planning/actions.h"

#include "planning/duration_sampler.h"
#include "planning/parallel_runs.h"
#include "planning/plan_simulator.h"
#include "planning/planned_board.h"
#include "planning/scope_graph.h"

#include <algorithm>
#include <array>
#include <span>

namespace coordina::graph
{
namespace
{
constexpr std::size_t word_bits = 64U;

// The race compares mean finishes; the simulator still asks for a goal.
constexpr double screening_goal = 0.85;

// Only the cards that drive the date can move it.
constexpr double least_criticality = 0.02;
constexpr std::size_t cut_candidate_limit = 48U;
constexpr std::size_t assign_candidate_limit = 24U;

// The first heat runs on a quarter of the screening runs, and a third of the
// candidates go through.
constexpr std::uint32_t first_heat_share = 4U;
constexpr std::size_t survivors_share = 3U;

// Below this an action is within the noise of the forecast.
constexpr double least_on_time_gain = 0.005;
constexpr double least_days_gain = 0.1;

// The chance each unit of value given up has to bring: two points for a
// low-priority card, eight for a high one. It is also above the noise of a
// forecast, so which of two like cards goes first does not depend on luck.
constexpr double chance_per_value = 0.02;

struct Candidate
{
  ActionKind kind = ActionKind::cut_card;
  std::size_t card = 0U;
  std::uint32_t person = 0U;
  // What a cut gives up; nothing for the other kinds.
  double value_lost = 0.0;
  double mean_hours = 0.0;
};

// The buffers of one thread of the race.
struct Racer
{
  Racer(
    const PlanModel& model,
    const DurationSampler& sampler,
    std::size_t milestone,
    std::uint32_t runs)
      : simulator(model, sampler, milestone, screening_goal, runs, 1U), graph(model, milestone),
        cut(model.card_count(), 0U), dropped(model.card_count(), 0U),
        active(model.card_count(), 1U), people(model.card_count(), Staffing::keep_assignees),
        team(model.team().begin(), model.team().end())
  {
  }

  PlanSimulator simulator;
  ScopeGraph graph;
  CardMask cut;
  CardMask dropped;
  CardMask active;
  std::vector<std::uint32_t> people;
  std::vector<std::uint64_t> team;
};

class ActionSearch
{
public:
  ActionSearch(const PlannedBoard& planned, std::size_t milestone, const ActionOptions& options)
      : planned_(&planned), model_(&planned.model()), options_(&options), milestone_(milestone),
        sampler_(planned.model(), planned.durations(), options.simulation.seed),
        graph_(planned.model(), milestone), criticality_(planned.model().card_count(), 0.0),
        before_(planned.forecast_now())
  {
    for (std::size_t card = 0U; card < model_->card_count(); ++card)
    {
      const auto& cards = before_.milestones[milestone_].cards;
      const auto found = std::ranges::find(cards, model_->nodes()[card], &CardForecast::node);
      criticality_[card] = found == cards.end() ? 0.0 : found->criticality;
    }
  }

  std::vector<Action> propose(const std::vector<std::uint32_t>& receivers)
  {
    std::vector<Candidate> candidates;
    add_cuts(candidates);
    add_assignments(receivers, candidates);
    race(candidates);

    std::vector<Action> actions;

    for (const auto& candidate : candidates)
    {
      append(measure(candidate), actions);
    }

    if (has_unassigned_work())
    {
      append(measure(Candidate{.kind = ActionKind::add_person}), actions);
    }

    std::ranges::stable_sort(
      actions,
      [](const Action& left, const Action& right)
      {
        return net_chance(left) > net_chance(right)
          || (!(net_chance(left) < net_chance(right)) && left.p85_days_after < right.p85_days_after);
      });
    actions.resize(std::min<std::size_t>(actions.size(), options_->limit));
    return actions;
  }

private:
  // The chance of being on time, less what the action has to bring.
  static double net_chance(const Action& action) noexcept
  {
    return action.on_time_after - (chance_per_value * action.value_lost);
  }

  // The cards of the scope that drive the date, most critical first.
  std::vector<std::size_t> driving_cards() const
  {
    std::vector<std::size_t> cards;

    for (std::size_t card = 0U; card < model_->card_count(); ++card)
    {
      if (graph_.contains(card) && card != graph_.milestone_card()
        && criticality_[card] >= least_criticality && !model_->item(card).is_in_progress)
      {
        cards.push_back(card);
      }
    }

    std::ranges::stable_sort(
      cards,
      [this](std::size_t left, std::size_t right)
      {
        return weight_of(left) > weight_of(right);
      });
    return cards;
  }

  // About the hours the card adds to the date.
  double weight_of(std::size_t card) const
  {
    return criticality_[card] * model_->planned_hours()[card];
  }

  // A card only one other card needs leaves with that card, so only the one
  // closer to the milestone is a candidate.
  void add_cuts(std::vector<Candidate>& candidates)
  {
    CardMask closure(model_->card_count(), 0U);
    std::size_t added = 0U;

    for (const auto card : driving_cards())
    {
      const auto dependents = model_->dependents(card);

      if (added == cut_candidate_limit
        || (dependents.size() == 1U && dependents.front() != graph_.milestone_card()))
      {
        continue;
      }

      std::ranges::fill(closure, 0U);
      graph_.close_up(card, closure);
      auto takes_out_started = false;
      auto value = 0.0;

      for (std::size_t other = 0U; other < closure.size(); ++other)
      {
        if (closure[other] != 0U)
        {
          takes_out_started = takes_out_started || model_->item(other).is_in_progress;
          value += priority_weight(model_->item(other).priority);
        }
      }

      if (!takes_out_started)
      {
        candidates.push_back(
          Candidate{.kind = ActionKind::cut_card, .card = card, .value_lost = value});
        ++added;
      }
    }
  }

  void add_assignments(
    const std::vector<std::uint32_t>& receivers,
    std::vector<Candidate>& candidates) const
  {
    auto cards = driving_cards();
    cards.resize(std::min(cards.size(), assign_candidate_limit));

    for (const auto card : cards)
    {
      const auto people = model_->people(card);

      for (const auto person : receivers)
      {
        const auto is_theirs = people.size() == 1U && people.front() == person;

        if (person < model_->nobody() && !is_theirs)
        {
          candidates.push_back(
            Candidate{.kind = ActionKind::assign_card, .card = card, .person = person});
        }
      }
    }
  }

  bool has_unassigned_work() const
  {
    for (std::size_t card = 0U; card < model_->card_count(); ++card)
    {
      if (graph_.contains(card) && card != graph_.milestone_card() && model_->people(card).empty())
      {
        return true;
      }
    }

    return false;
  }

  void race(std::vector<Candidate>& candidates)
  {
    const auto finalists = static_cast<std::size_t>(options_->limit);
    PlanSimulator simulator(
      *model_,
      sampler_,
      milestone_,
      screening_goal,
      options_->screen_run_count,
      options_->simulation.thread_count);
    baseline_hours_ = simulator.run(options_->screen_run_count, {}, {}, nullptr).mean_hours;
    const auto first_heat = std::max(1U, options_->screen_run_count / first_heat_share);
    run_heat(candidates, first_heat);
    keep_best(candidates, std::max(finalists, candidates.size() / survivors_share));
    run_heat(candidates, options_->screen_run_count);
    keep_best(candidates, finalists);
  }

  void run_heat(std::vector<Candidate>& candidates, std::uint32_t runs)
  {
    if (candidates.empty())
    {
      return;
    }

    const auto chunks = std::min(chunk_limit, candidates.size());
    std::vector<Racer> racers(
      thread_count_for(
        options_->simulation.thread_count,
        chunks,
        candidates.size() * runs * model_->card_count()),
      Racer(*model_, sampler_, milestone_, runs));
    run_chunks(
      std::span(racers),
      chunks,
      [&](Racer& racer, std::size_t chunk)
      {
        const auto end = chunk_start(candidates.size(), chunks, chunk + 1U);

        for (auto next = chunk_start(candidates.size(), chunks, chunk); next < end; ++next)
        {
          candidates[next].mean_hours = simulate(racer, candidates[next], runs);
        }
      });
  }

  // The chance of being on time is too coarse to compare on a few runs; the
  // mean finish ranks the same actions with less noise.
  static double simulate(Racer& racer, const Candidate& candidate, std::uint32_t runs)
  {
    if (candidate.kind == ActionKind::cut_card)
    {
      std::ranges::fill(racer.cut, 0U);
      racer.graph.close_up(candidate.card, racer.cut);
      racer.graph.apply(racer.cut, racer.dropped, racer.active);
      const Cut cut{
        .active = racer.active,
        .dropped = racer.dropped,
        .milestone_card = racer.graph.milestone_card()
      };
      return racer.simulator.run(runs, cut, {}, nullptr).mean_hours;
    }

    const auto word = candidate.person / word_bits;
    const auto team = racer.team[word];
    racer.people[candidate.card] = candidate.person;
    racer.team[word] |= std::uint64_t{1U} << (candidate.person % word_bits);
    const Staffing staffing{.people = racer.people, .team = racer.team};
    const auto mean = racer.simulator.run(runs, {}, staffing, nullptr).mean_hours;
    racer.people[candidate.card] = Staffing::keep_assignees;
    racer.team[word] = team;
    return mean;
  }

  // The hours a candidate saves, for each unit of value it gives up.
  double worth_of(const Candidate& candidate) const
  {
    const auto saved = baseline_hours_ - candidate.mean_hours;
    return candidate.kind == ActionKind::cut_card ? saved / candidate.value_lost : saved;
  }

  // Each kind keeps its own finalists: they answer different questions.
  void keep_best(std::vector<Candidate>& candidates, std::size_t limit) const
  {
    std::ranges::stable_sort(
      candidates,
      [this](const Candidate& left, const Candidate& right)
      {
        return worth_of(left) > worth_of(right);
      });
    std::array<std::size_t, 4U> kept{};
    std::erase_if(
      candidates,
      [&](const Candidate& candidate)
      {
        return kept.at(static_cast<std::size_t>(candidate.kind))++ >= limit;
      });
  }

  Action measure(const Candidate& candidate)
  {
    const auto& before = before_.milestones[milestone_];
    Action action{
      .kind = candidate.kind,
      .on_time_before = before.on_time,
      .p85_days_before = before.p85_days
    };
    auto after = before_;

    switch (candidate.kind)
    {
      case ActionKind::cut_card:
        after = forecast_cut(candidate.card, action);
        break;
      case ActionKind::assign_card:
        action.node = model_->nodes()[candidate.card];
        action.person = candidate.person;
        after = planned_->forecast_with(
          std::vector{Assignee{.node = action.node, .person = candidate.person}});
        break;
      case ActionKind::add_person:
        after = planned_->forecast_with_people(1U);
        break;
    }

    action.on_time_after = after.milestones[milestone_].on_time;
    action.p85_days_after = after.milestones[milestone_].p85_days;

    for (std::size_t other = 0U; other < after.milestones.size(); ++other)
    {
      if (other != milestone_)
      {
        action.others_delay_days +=
          (after.milestones[other].late_days - before_.milestones[other].late_days)
          * model_->milestones()[other].weight / model_->milestones()[milestone_].weight;
      }
    }

    return action;
  }

  Forecast forecast_cut(std::size_t card, Action& action)
  {
    CardMask cut(model_->card_count(), 0U);
    CardMask dropped(model_->card_count(), 0U);
    CardMask active(model_->card_count(), 1U);
    graph_.close_up(card, cut);
    graph_.apply(cut, dropped, active);
    std::vector<NodeId> leaving;

    for (std::size_t other = 0U; other < dropped.size(); ++other)
    {
      if (dropped[other] != 0U)
      {
        leaving.push_back(model_->nodes()[other]);
        action.value_lost += priority_weight(model_->item(other).priority);
      }
    }

    action.node = model_->nodes()[card];
    action.dropped_count = static_cast<std::uint32_t>(leaving.size());
    return planned_->forecast_without(options_->milestone, leaving);
  }

  static void append(const Action& action, std::vector<Action>& actions)
  {
    if (action.on_time_after - action.on_time_before >= least_on_time_gain
      || (action.on_time_after >= action.on_time_before && action.p85_days_before - action.p85_days_after >= least_days_gain))
    {
      actions.push_back(action);
    }
  }

  const PlannedBoard* planned_;
  const PlanModel* model_;
  const ActionOptions* options_;
  std::size_t milestone_;
  DurationSampler sampler_;
  ScopeGraph graph_;
  std::vector<double> criticality_;
  Forecast before_;
  double baseline_hours_ = 0.0;
};
}

ActionPlan propose_actions(
  const PlanningBoard& board,
  const std::vector<std::uint32_t>& receivers,
  const ActionOptions& options)
{
  const PlannedBoard planned(board, options.simulation);

  if (planned.error() != PlanError::none)
  {
    return ActionPlan{.error = planned.error(), .actions = {}};
  }

  const auto milestone = planned.find_milestone(options.milestone);

  if (milestone == PlannedBoard::no_milestone)
  {
    return ActionPlan{.error = PlanError::unknown_milestone, .actions = {}};
  }

  if (planned.model().milestones()[milestone].card == PlanMilestone::done)
  {
    return ActionPlan{};
  }

  ActionSearch search(planned, milestone, options);
  return ActionPlan{.error = PlanError::none, .actions = search.propose(receivers)};
}
}
