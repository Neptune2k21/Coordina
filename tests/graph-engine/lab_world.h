#ifndef COORDINA_GRAPH_ENGINE_TESTS_LAB_WORLD_H
#define COORDINA_GRAPH_ENGINE_TESTS_LAB_WORLD_H

#include "numeric/normal.h"
#include "planning/board_index.h"
#include "planning/planned_board.h"
#include "planning/planning_board.h"
#include "planning/sampling.h"
#include "planning/schedule.h"

#include <algorithm>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <limits>
#include <vector>

// Made-up teams whose true durations are known, to measure how right the
// engine is about them.
namespace coordina::graph::lab
{
constexpr std::uint64_t milestone_node = 1'000'000U;

struct BoardShape
{
  std::uint32_t cards = 40U;
  std::uint32_t people = 3U;
  double assigned_share = 0.6;
  double unsized_share = 0.0;
};

// How a made-up team really works, which the engine does not see.
struct World
{
  // The spread of what is true of the whole team, of each person and label.
  double team_spread = 0.3;
  double person_spread = 0.2;
  double label_spread = 0.15;
  // What everyone's estimates miss on average, as a logarithm.
  double bias = 0.0;
  // How far the team has changed between its history and the open cards.
  double drift_spread = 0.0;
  // A share of cards that take several times longer than their size says,
  // which no log-normal describes.
  double disaster_share = 0.0;
};

inline NodeId lab_node(std::uint64_t value)
{
  return NodeId{.high = value * 0x9e3779b97f4a7c15ULL, .low = value + 1U};
}

// What is true of one team.
class Truth
{
public:
  Truth(const World& world, std::uint64_t seed) : world_(world), random_(seed)
  {
    team_ = world.bias + (world.team_spread * normal());

    for (std::size_t index = 0U; index < 16U; ++index)
    {
      people_.push_back(world.person_spread * normal());
      labels_.push_back(world.label_spread * normal());
    }
  }

  double hours(CardSize size, std::int64_t person, std::uint32_t label)
  {
    const auto range = size_range(size);
    auto shift = team_ + labels_[label % labels_.size()];
    shift += person < 0 ? 0.0 : people_[static_cast<std::size_t>(person) % people_.size()];
    shift += random_.unit() < world_.disaster_share ? std::log(3.0 + (3.0 * random_.unit())) : 0.0;
    return range.likely_hours * std::exp(shift + (range.sigma() * normal()));
  }

  void drift()
  {
    team_ += world_.drift_spread * normal();
  }

  CardSize size()
  {
    const auto unit = random_.unit();
    return unit < 0.1 ? CardSize::xs
      : unit < 0.4    ? CardSize::s
      : unit < 0.75   ? CardSize::m
      : unit < 0.93   ? CardSize::l
                      : CardSize::xl;
  }

  SplitMix& random()
  {
    return random_;
  }

private:
  double normal()
  {
    return normal_quantile(random_.unit());
  }

  World world_;
  SplitMix random_;
  double team_ = 0.0;
  std::vector<double> people_;
  std::vector<double> labels_;
};

struct LabBoard
{
  PlanningBoard board;
  // By board node order.
  std::vector<double> true_hours;
  std::vector<CardSize> true_sizes;
};

// Cards in layers, each waiting for at most two recent ones, and a milestone
// that waits for every card nothing else does. Completed cards with their true
// durations come first.
inline LabBoard make_board(
  const BoardShape& shape,
  const World& world,
  std::uint32_t history,
  std::uint64_t seed)
{
  Truth truth(world, seed);
  LabBoard result;
  auto& board = result.board;
  board.person_count = shape.people;
  // Nobody is assigned to anything, yet the team is there.
  board.extra_people = shape.assigned_share > 0.0 ? 0U : shape.people;

  const auto add = [&](std::uint64_t id, CardSize size, std::int64_t person, std::uint32_t label)
  {
    WorkItem item;
    item.node = lab_node(id);
    item.due_days = std::numeric_limits<std::int32_t>::max();
    item.position = static_cast<std::uint32_t>(id);
    item.size = size;
    board.nodes.push_back(item.node);
    board.items.push_back(item);
    board.labels.push_back(CardLabel{.node = item.node, .label = label});

    if (person >= 0)
    {
      board.assignees.push_back(
        Assignee{.node = item.node, .person = static_cast<std::uint32_t>(person)});
    }
  };

  for (std::uint32_t done = 0U; done < history; ++done)
  {
    const auto id = 500'000U + done;
    const auto size = truth.size();
    const auto person = static_cast<std::int64_t>(truth.random().below(shape.people));
    const auto label = static_cast<std::uint32_t>(truth.random().below(4U));
    add(id, size, person, label);
    board.items.back().is_completed = true;
    result.true_hours.push_back(0.0);
    result.true_sizes.push_back(size);
    board.history.push_back(
      FinishedCard{
        .node = lab_node(id),
        .hours = truth.hours(size, person, label),
        .age_days = static_cast<double>(history - done) / 2.0
      });
  }

  truth.drift();
  std::vector<std::uint8_t> needed(shape.cards, 0U);

  for (std::uint32_t card = 0U; card < shape.cards; ++card)
  {
    const auto size = truth.size();
    const auto is_assigned = truth.random().unit() < shape.assigned_share;
    const auto person =
      is_assigned ? static_cast<std::int64_t>(truth.random().below(shape.people)) : -1;
    const auto label = static_cast<std::uint32_t>(truth.random().below(4U));
    const auto is_unsized = truth.random().unit() < shape.unsized_share;
    add(card, is_unsized ? CardSize::none : size, person, label);
    result.true_hours.push_back(truth.hours(size, person, label));
    result.true_sizes.push_back(size);
    const auto roll = truth.random().unit();
    const auto dependencies = roll < 0.3 ? 0U : (roll < 0.75 ? 1U : 2U);

    for (std::uint32_t dependency = 0U; dependency < dependencies && card > 0U; ++dependency)
    {
      const auto first = card > shape.cards / 4U ? card - (shape.cards / 4U) : 0U;
      const auto target = first + static_cast<std::uint32_t>(truth.random().below(card - first));
      needed[target] = 1U;
      board.edges.push_back(Edge{.source = lab_node(card), .target = lab_node(target)});
    }
  }

  add(milestone_node, CardSize::xs, -1, 0U);
  result.true_hours.push_back(truth.hours(CardSize::xs, -1, 0U));
  result.true_sizes.push_back(CardSize::xs);

  for (std::uint32_t card = 0U; card < shape.cards; ++card)
  {
    if (needed[card] == 0U)
    {
      board.edges.push_back(Edge{.source = lab_node(milestone_node), .target = lab_node(card)});
    }
  }

  board.milestones.push_back(
    Milestone{.node = lab_node(milestone_node), .target_hours = 0.0, .weight = 1.0});
  return result;
}

// When the milestone really ships if the team follows the plan.
inline double true_finish_days(const LabBoard& lab, const PlanModel& model)
{
  const BoardIndex index(lab.board);
  std::vector<double> hours;

  for (const auto node : model.nodes())
  {
    hours.push_back(lab.true_hours[index.find(node)]);
  }

  Scheduler scheduler(model);
  scheduler.run(hours);
  return scheduler.finish_of(0U) / focus_hours_per_day;
}
}

#endif
