#include "coordina_graph_engine.h"

#include "interop/abi_queries.h"
#include "interop/native_bridge.h"
#include "planning/forecast.h"
#include "planning/scope_cut.h"

// The exports that simulate a milestone's work: its forecast, and what to cut
// so it ships on time.
namespace
{
using coordina::graph::GraphSnapshot;
using coordina::graph::WorkItem;
using coordina::graph::abi::fits;
using coordina::graph::abi::query_work;

coordina_graph_status to_status(coordina::graph::ForecastError error) noexcept
{
  switch (error)
  {
    case coordina::graph::ForecastError::none:
      return COORDINA_GRAPH_OK;
    case coordina::graph::ForecastError::cycle:
      return COORDINA_GRAPH_CYCLE;
    case coordina::graph::ForecastError::unknown_milestone:
    case coordina::graph::ForecastError::unknown_person:
      break;
  }

  return COORDINA_GRAPH_INVALID_ARGUMENT;
}

using coordina::graph::ForecastModel;

template <typename Query>
coordina_graph_status query_model(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  const coordina_graph_work_item* work_items,
  std::size_t work_item_count,
  const coordina_graph_assignee* assignees,
  std::size_t assignee_count,
  coordina_graph_node_id milestone,
  std::uint32_t person_count,
  Query query)
{
  if (coordina::graph::is_missing(assignees, assignee_count))
  {
    return COORDINA_GRAPH_INVALID_ARGUMENT;
  }

  return query_work(
    nodes,
    node_count,
    edges,
    edge_count,
    work_items,
    work_item_count,
    COORDINA_GRAPH_INVALID_ARGUMENT,
    [&](const GraphSnapshot& graph, const std::vector<WorkItem>& items)
    {
      const ForecastModel model(
        graph,
        items,
        coordina::graph::build_assignees(assignees, assignee_count),
        coordina::graph::to_node_id(milestone),
        person_count);

      return model.error() == coordina::graph::ForecastError::none ? query(model, items)
                                                                   : to_status(model.error());
    });
}

bool is_valid(const coordina_graph_scope_options* options, const coordina_graph_scope_cut* output)
{
  return options != nullptr && output != nullptr && options->run_count > 0U && options->goal > 0.0
    && options->goal <= 1.0;
}

coordina_graph_status write_scope(
  const coordina::graph::ScopeCut& cut,
  coordina_graph_scope_cut& output)
{
  output.cut_count = cut.cuts.size();
  output.dropped_count = cut.dropped.size();

  if (!fits(output.cut_count, output.cuts, output.cut_capacity)
    || !fits(output.dropped_count, output.dropped, output.dropped_capacity))
  {
    return COORDINA_GRAPH_INSUFFICIENT_CAPACITY;
  }

  coordina::graph::write_scope_cut(cut, output);
  return COORDINA_GRAPH_OK;
}
}

extern "C" coordina_graph_status coordina_graph_forecast(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  const coordina_graph_work_item* work_items,
  std::size_t work_item_count,
  const coordina_graph_assignee* assignees,
  std::size_t assignee_count,
  const coordina_graph_forecast_options* options,
  coordina_graph_milestone_forecast* output)
{
  if (options == nullptr || output == nullptr || options->run_count == 0U)
  {
    return COORDINA_GRAPH_INVALID_ARGUMENT;
  }

  return query_model(
    nodes,
    node_count,
    edges,
    edge_count,
    work_items,
    work_item_count,
    assignees,
    assignee_count,
    options->milestone,
    options->person_count,
    [&](const ForecastModel& model, const std::vector<WorkItem>& /*items*/)
    {
      const auto forecast = coordina::graph::forecast(
        model,
        coordina::graph::ForecastOptions{
          .run_count = options->run_count,
          .seed = options->seed,
          .thread_count = options->thread_count
        });
      output->card_count = forecast.cards.size();

      if (!fits(output->card_count, output->cards, output->card_capacity))
      {
        return COORDINA_GRAPH_INSUFFICIENT_CAPACITY;
      }

      output->p50_days = forecast.p50_days;
      output->p85_days = forecast.p85_days;
      output->unsized_count = forecast.unsized_count;
      coordina::graph::write_card_forecasts(forecast.cards, output->cards, output->card_capacity);

      return COORDINA_GRAPH_OK;
    });
}

extern "C" coordina_graph_status coordina_graph_propose_scope(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  const coordina_graph_work_item* work_items,
  std::size_t work_item_count,
  const coordina_graph_assignee* assignees,
  std::size_t assignee_count,
  const coordina_graph_node_id* keep,
  std::size_t keep_count,
  const coordina_graph_scope_options* options,
  coordina_graph_scope_cut* output)
{
  if (!is_valid(options, output) || options->screen_run_count == 0U
    || coordina::graph::is_missing(keep, keep_count))
  {
    return COORDINA_GRAPH_INVALID_ARGUMENT;
  }

  return query_model(
    nodes,
    node_count,
    edges,
    edge_count,
    work_items,
    work_item_count,
    assignees,
    assignee_count,
    options->milestone,
    options->person_count,
    [&](const ForecastModel& model, const std::vector<WorkItem>& items)
    {
      return write_scope(
        coordina::graph::propose_scope(
          model,
          items,
          coordina::graph::to_node_id(options->milestone),
          coordina::graph::build_node_ids(keep, keep_count),
          coordina::graph::build_scope_options(*options)),
        *output);
    });
}

extern "C" coordina_graph_status coordina_graph_evaluate_scope(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  const coordina_graph_work_item* work_items,
  std::size_t work_item_count,
  const coordina_graph_assignee* assignees,
  std::size_t assignee_count,
  const coordina_graph_node_id* cuts,
  std::size_t cut_count,
  const coordina_graph_scope_options* options,
  coordina_graph_scope_cut* output)
{
  if (!is_valid(options, output) || coordina::graph::is_missing(cuts, cut_count))
  {
    return COORDINA_GRAPH_INVALID_ARGUMENT;
  }

  return query_model(
    nodes,
    node_count,
    edges,
    edge_count,
    work_items,
    work_item_count,
    assignees,
    assignee_count,
    options->milestone,
    options->person_count,
    [&](const ForecastModel& model, const std::vector<WorkItem>& items)
    {
      return write_scope(
        coordina::graph::evaluate_scope(
          model,
          items,
          coordina::graph::to_node_id(options->milestone),
          coordina::graph::build_node_ids(cuts, cut_count),
          coordina::graph::build_scope_options(*options)),
        *output);
    });
}
