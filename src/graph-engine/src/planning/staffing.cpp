#include "planning/staff_search.h"

#include "planning/parallel_runs.h"

#include <algorithm>
#include <limits>
#include <numeric>
#include <unordered_set>

// Setting a staffing search up from the team, and what it answers: the plan
// it proposes, or the measure of changes someone chose.
namespace coordina::graph
{
namespace
{
constexpr auto keep = Staffing::keep_assignees;
constexpr auto no_card = std::numeric_limits<std::size_t>::max();

// The share of the time left that annealing gets; pruning gets the rest.
constexpr double annealing_share = 0.8;

ScopeOptions scope_options_of(const StaffingOptions& options) noexcept
{
  return ScopeOptions{
    .seed = options.seed,
    .run_count = options.run_count,
    .screen_run_count = options.run_count,
    .thread_count = options.thread_count
  };
}

// Whether the second plan ships sooner: its 85% date first, then its 50% one.
bool ships_sooner(const ScopeSample& after, const ScopeSample& before) noexcept
{
  return after.p85_hours < before.p85_hours
    || (!(after.p85_hours > before.p85_hours) && after.p50_hours < before.p50_hours);
}
}

StaffSearch::Plan::Plan(const ForecastModel& model)
    : people(model.card_count(), keep), held(model.person_count(), 0U)
{
}

StaffSearch::Runner::Runner(const ForecastModel& model, std::size_t pool_words)
    : plan(model), pool(pool_words, 0U), scheduler(model)
{
  driving.reserve(model.card_count());
}

StaffSearch::StaffSearch(
  const ForecastModel& model,
  const std::vector<WorkItem>& work_items,
  const StaffingTeam& team,
  const StaffingOptions& options)
    : model_(&model), options_(&options), nobody_(model.person_count() - 1U),
      pool_words_((model.person_count() + word_bits - 1U) / word_bits),
      simulator_(model, scope_options_of(options)), likely_(model.likely_hours()),
      active_(model.card_count(), 1U), in_progress_(model.card_count(), 0U),
      is_receiver_(model.person_count(), 0U), elsewhere_(pool_words_, 0U),
      movable_flags_(model.card_count(), 0U), keep_(model), started_(Clock::now())
{
  index_cards();
  read_work(work_items, team.assignees);
  link_skills(team.skilled);
  draw_screen_runs();
  pick_receivers(team.receivers);
  find_movable();

  const auto cards = model.card_count();

  // A move changes the finish by about one card's duration.
  if (cards > 0U)
  {
    temperature_ = std::max(
      std::accumulate(likely_.begin(), likely_.end(), 0.0) / static_cast<double>(cards),
      0.25);
  }

  const auto draws =
    static_cast<std::size_t>(options.evaluation_limit) * options.screen_run_count * cards;
  runners_.assign(
    thread_count_for(options.thread_count, chain_count, draws),
    Runner(model, pool_words_));
  bests_.assign(chain_count, keep_);
  best_costs_.assign(chain_count, 0.0);
}

// Indexes the cards by node, and counts who holds each card as it is.
void StaffSearch::index_cards()
{
  index_.reserve(model_->card_count());

  for (std::size_t card = 0U; card < model_->card_count(); ++card)
  {
    index_.emplace(model_->nodes()[card], card);

    for (const auto person : model_->people(card))
    {
      ++keep_.held[person];
    }
  }
}

// Marks the cards in progress, and the people assigned to open cards outside
// the scope: they stay on the team whatever the plan.
void StaffSearch::read_work(
  const std::vector<WorkItem>& work_items,
  const std::vector<Assignee>& assignees)
{
  std::unordered_set<NodeId, NodeHash> completed;

  for (const auto& item : work_items)
  {
    if (item.is_completed)
    {
      completed.insert(item.node);
    }
    else if (const auto card = index_of(item.node); card != no_card)
    {
      in_progress_[card] = item.is_in_progress ? 1U : 0U;
    }
  }

  for (const auto& assignee : assignees)
  {
    if (assignee.person < nobody_ && index_of(assignee.node) == no_card
      && !completed.contains(assignee.node))
    {
      set_bit(elsewhere_, assignee.person);
    }
  }
}

// The people skilled for each card, sorted: the card's entries are
// skilled_[skilled_offsets_[card], skilled_offsets_[card + 1]).
void StaffSearch::link_skills(const std::vector<Assignee>& skilled)
{
  std::vector<std::pair<std::size_t, std::uint32_t>> skills;

  for (const auto& skill : skilled)
  {
    if (const auto card = index_of(skill.node); card != no_card && skill.person < nobody_)
    {
      skills.emplace_back(card, skill.person);
    }
  }

  std::ranges::sort(skills);
  const auto duplicates = std::ranges::unique(skills);
  skills.erase(duplicates.begin(), duplicates.end());
  skilled_offsets_.assign(model_->card_count() + 1U, 0U);

  for (const auto& [card, person] : skills)
  {
    ++skilled_offsets_[card + 1U];
    skilled_.push_back(person);
  }

  std::partial_sum(skilled_offsets_.begin(), skilled_offsets_.end(), skilled_offsets_.begin());
}

// The screening runs, drawn once from the forecast's streams.
void StaffSearch::draw_screen_runs()
{
  const auto cards = model_->card_count();
  const CardStreams streams(options_->seed, model_->nodes());
  screen_hours_.reserve(options_->screen_run_count * cards);

  for (std::uint32_t run = 0U; run < options_->screen_run_count; ++run)
  {
    for (std::size_t card = 0U; card < cards; ++card)
    {
      screen_hours_.push_back(model_->duration(card)(streams.unit(card, run)));
    }
  }
}

void StaffSearch::pick_receivers(const std::vector<std::uint32_t>& receivers)
{
  for (const auto person : receivers)
  {
    if (person < nobody_ && is_receiver_[person] == 0U)
    {
      is_receiver_[person] = 1U;
      receivers_.push_back(person);
    }
  }

  std::ranges::sort(receivers_);
}

void StaffSearch::find_movable()
{
  for (std::size_t card = 0U; card < model_->card_count(); ++card)
  {
    if (can_move(card))
    {
      movable_flags_[card] = 1U;
      movable_.push_back(card);
    }
  }
}

std::size_t StaffSearch::index_of(NodeId node) const
{
  const auto found = index_.find(node);
  return found == index_.end() ? no_card : found->second;
}

StaffingPlan StaffSearch::propose()
{
  auto& first = runners_.front();
  first.plan = keep_;
  const auto before = measure(first);

  if (movable_.empty())
  {
    return finish(first, before, before);
  }

  auto budget = Clock::duration::zero();

  if (options_->time_limit.count() > 0)
  {
    // Measuring the plan found takes about as long as measuring the current
    // one, with some room to spare.
    const auto now = Clock::now();
    deadline_ = started_ + options_->time_limit - (2 * (now - started_));

    if (deadline_ <= now)
    {
      return finish(first, before, before);
    }

    const auto left = std::chrono::duration<double>(deadline_ - now) * annealing_share;
    budget = std::chrono::duration_cast<Clock::duration>(
      left * static_cast<double>(runners_.size()) / static_cast<double>(chain_count));
  }

  run_chunks(
    std::span(runners_),
    chain_count,
    [&](Runner& runner, std::size_t chain)
    {
      anneal(runner, chain, budget);
    });

  const auto best_chain =
    static_cast<std::size_t>(std::ranges::min_element(best_costs_) - best_costs_.begin());
  first.plan = bests_[best_chain];
  auto best = best_costs_[best_chain];
  prune(first, best);

  if (first.plan.changes == 0U)
  {
    return finish(first, before, before);
  }

  const auto after = measure(first);

  if (!ships_sooner(after, before))
  {
    first.plan = keep_;
    return finish(first, before, before);
  }

  return finish(first, before, after);
}

StaffingPlan StaffSearch::evaluate(const std::vector<Assignee>& changes)
{
  auto& first = runners_.front();
  first.plan = keep_;
  const auto before = measure(first);

  for (const auto& change : changes)
  {
    if (const auto card = index_of(change.node); card != no_card && change.person < nobody_)
    {
      give(first.plan, card, normalized(card, change.person));
    }
  }

  return finish(first, before, first.plan.changes == 0U ? before : measure(first));
}

// When each card starts if every card takes its likely time.
std::vector<double> StaffSearch::starts_of(Runner& runner, const Plan& plan)
{
  fill_pool(plan, runner.pool);
  runner.scheduler.run(likely_, {}, Staffing{.people = plan.people, .pool = runner.pool});
  std::vector<double> starts;
  starts.reserve(model_->card_count());

  for (const auto& card : runner.scheduler.schedule().cards)
  {
    starts.push_back(card.start_hours);
  }

  return starts;
}

StaffingPlan StaffSearch::finish(
  Runner& runner,
  const ScopeSample& before,
  const ScopeSample& after)
{
  StaffingPlan result;
  result.p50_days_before = before.p50_hours / focus_hours_per_day;
  result.p85_days_before = before.p85_hours / focus_hours_per_day;
  result.p50_days_after = after.p50_hours / focus_hours_per_day;
  result.p85_days_after = after.p85_hours / focus_hours_per_day;

  if (runner.plan.changes == 0U)
  {
    return result;
  }

  const auto plan = runner.plan;
  const auto starts_before = starts_of(runner, keep_);
  const auto starts_after = starts_of(runner, plan);

  for (std::size_t card = 0U; card < plan.people.size(); ++card)
  {
    if (plan.people[card] != keep)
    {
      result.changes.push_back(
        StaffChange{
          .node = model_->nodes()[card],
          .person = plan.people[card],
          .start_days_before = starts_before[card] / focus_hours_per_day,
          .start_days_after = starts_after[card] / focus_hours_per_day
        });
    }
  }

  std::ranges::stable_sort(
    result.changes,
    [](const StaffChange& left, const StaffChange& right)
    {
      return left.start_days_after < right.start_days_after;
    });

  return result;
}

StaffingPlan propose_staffing(
  const ForecastModel& model,
  const std::vector<WorkItem>& work_items,
  const StaffingTeam& team,
  const StaffingOptions& options)
{
  if (model.error() != ForecastError::none || options.run_count == 0U
    || options.screen_run_count == 0U)
  {
    return {};
  }

  StaffSearch search(model, work_items, team, options);
  return search.propose();
}

StaffingPlan evaluate_staffing(
  const ForecastModel& model,
  const std::vector<WorkItem>& work_items,
  const StaffingTeam& team,
  const std::vector<Assignee>& changes,
  const StaffingOptions& options)
{
  if (model.error() != ForecastError::none || options.run_count == 0U)
  {
    return {};
  }

  StaffSearch search(model, work_items, team, options);
  return search.evaluate(changes);
}
}
