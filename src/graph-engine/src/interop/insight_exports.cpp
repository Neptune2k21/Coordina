#include "coordina_graph_engine.h"

#include "interop/abi_errors.h"
#include "interop/planning_bridge.h"
#include "planning/actions.h"
#include "planning/calibration.h"
#include "planning/dependency_hints.h"
#include "planning/questions.h"

#include <cstddef>
#include <vector>

// The exports that advise: what to do about a milestone, what to ask, what the
// board probably misses, and what its history has taught.
namespace
{
namespace abi = coordina::graph::abi;

using coordina::graph::PlanError;
using coordina::graph::to_abi_node_id;
using coordina::graph::to_node_id;

// An answer is a handful of things to read.
constexpr std::uint32_t answer_limit = 1000U;

coordina_graph_status check_limit(std::uint32_t limit) noexcept
{
  return limit == 0U || limit > answer_limit ? abi::invalid("limit is outside 1 to 1000.")
                                             : COORDINA_GRAPH_OK;
}

coordina_graph_status check_hints(
  const coordina_graph_dependency_hint* hints,
  std::size_t count) noexcept
{
  if (const auto status = abi::check_array(hints, count); status != COORDINA_GRAPH_OK)
  {
    return status;
  }

  for (std::size_t index = 0U; index < count; ++index)
  {
    // NOLINTNEXTLINE(readability-simplify-boolean-expr): written so that a NaN is outside.
    if (!(hints[index].probability >= 0.0 && hints[index].probability <= 1.0))
    {
      return abi::invalid("A probability is outside 0 to 1.");
    }
  }

  return COORDINA_GRAPH_OK;
}

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

extern "C" coordina_graph_status coordina_graph_propose_actions(
  const coordina_graph_board* board,
  const std::uint32_t* receivers,
  std::size_t receiver_count,
  const coordina_graph_action_options* options,
  coordina_graph_action_output* output)
{
  return abi::guarded(
    [&]
    {
      if (const auto status = abi::check_board(board); status != COORDINA_GRAPH_OK)
      {
        return status;
      }

      if (options == nullptr || output == nullptr)
      {
        return abi::invalid("An argument is null.");
      }

      if (options->screen_run_count == 0U || options->screen_run_count > abi::run_count_limit)
      {
        return abi::invalid("screen_run_count is outside 1 to 100000.");
      }

      for (const auto status :
        {abi::check_people(receivers, receiver_count, *board),
          abi::check_simulation(options->simulation),
          check_limit(options->limit)})
      {
        if (status != COORDINA_GRAPH_OK)
        {
          return status;
        }
      }

      const auto plan = coordina::graph::propose_actions(
        abi::build_board(*board),
        std::vector<std::uint32_t>(receivers, receivers + receiver_count),
        coordina::graph::ActionOptions{
          .milestone = to_node_id(options->milestone),
          .simulation = abi::build_simulation(options->simulation),
          .screen_run_count = options->screen_run_count,
          .limit = options->limit
        });

      if (plan.error != PlanError::none)
      {
        return abi::to_status(plan.error);
      }

      const auto fits = abi::write_items(
        plan.actions,
        output->actions,
        output->action_capacity,
        output->action_count,
        [](const coordina::graph::Action& action)
        {
          return coordina_graph_action{
            .node = to_abi_node_id(action.node),
            .kind = static_cast<std::uint32_t>(action.kind),
            .person = action.person,
            .on_time_before = action.on_time_before,
            .on_time_after = action.on_time_after,
            .p85_days_before = action.p85_days_before,
            .p85_days_after = action.p85_days_after,
            .others_delay_days = action.others_delay_days,
            .dropped_count = action.dropped_count,
            .reserved = 0U,
            .value_lost = action.value_lost
          };
        });
      return fits ? COORDINA_GRAPH_OK : COORDINA_GRAPH_INSUFFICIENT_CAPACITY;
    });
}

extern "C" coordina_graph_status coordina_graph_rank_questions(
  const coordina_graph_board* board,
  const coordina_graph_card_token* tokens,
  std::size_t token_count,
  const coordina_graph_dependency_hint* hints,
  std::size_t hint_count,
  const coordina_graph_question_options* options,
  coordina_graph_question_output* output)
{
  return abi::guarded(
    [&]
    {
      if (const auto status = abi::check_board(board); status != COORDINA_GRAPH_OK)
      {
        return status;
      }

      if (options == nullptr || output == nullptr)
      {
        return abi::invalid("An argument is null.");
      }

      for (const auto status :
        {abi::check_array(tokens, token_count),
          check_hints(hints, hint_count),
          abi::check_simulation(options->simulation),
          check_limit(options->limit)})
      {
        if (status != COORDINA_GRAPH_OK)
        {
          return status;
        }
      }

      std::vector<coordina::graph::DependencyHint> known;
      known.reserve(hint_count);

      for (std::size_t index = 0U; index < hint_count; ++index)
      {
        known.push_back(
          coordina::graph::DependencyHint{
            .card = to_node_id(hints[index].card),
            .dependency = to_node_id(hints[index].dependency),
            .probability = hints[index].probability
          });
      }

      const auto list = coordina::graph::rank_questions(
        abi::build_board(*board),
        abi::build_tokens(tokens, token_count),
        known,
        coordina::graph::QuestionOptions{
          .milestone = to_node_id(options->milestone),
          .simulation = abi::build_simulation(options->simulation),
          .limit = options->limit
        });

      if (list.error != PlanError::none)
      {
        return abi::to_status(list.error);
      }

      const auto fits = abi::write_items(
        list.questions,
        output->questions,
        output->question_capacity,
        output->question_count,
        [](const coordina::graph::Question& question)
        {
          return coordina_graph_question{
            .node = to_abi_node_id(question.node),
            .other = to_abi_node_id(question.other),
            .kind = static_cast<std::uint32_t>(question.kind),
            .reserved = 0U,
            .probability = question.probability,
            .margin_days = question.margin_days,
            .shift_days = question.shift_days
          };
        });
      output->margin_days = list.margin_days;
      return fits ? COORDINA_GRAPH_OK : COORDINA_GRAPH_INSUFFICIENT_CAPACITY;
    });
}

extern "C" coordina_graph_status coordina_graph_suggest_dependencies(
  const coordina_graph_board* board,
  const coordina_graph_card_token* tokens,
  std::size_t token_count,
  const coordina_graph_node_id* source,
  std::uint32_t limit,
  coordina_graph_hint_output* output)
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

      for (const auto status : {abi::check_array(tokens, token_count), check_limit(limit)})
      {
        if (status != COORDINA_GRAPH_OK)
        {
          return status;
        }
      }

      const auto card = source == nullptr ? coordina::graph::NodeId{} : to_node_id(*source);
      const auto hints = coordina::graph::suggest_dependencies(
        abi::build_board(*board),
        abi::build_tokens(tokens, token_count),
        source == nullptr ? nullptr : &card,
        limit);
      const auto fits = abi::write_items(
        hints,
        output->hints,
        output->hint_capacity,
        output->hint_count,
        [](const coordina::graph::DependencyHint& hint)
        {
          return coordina_graph_dependency_hint{
            .card = to_abi_node_id(hint.card),
            .dependency = to_abi_node_id(hint.dependency),
            .probability = hint.probability
          };
        });
      return fits ? COORDINA_GRAPH_OK : COORDINA_GRAPH_INSUFFICIENT_CAPACITY;
    });
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
