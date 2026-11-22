#include "coordina_graph_engine.h"

#include "algorithms/critical_path.h"
#include "algorithms/dependency_validator.h"
#include "algorithms/graph_traversal.h"
#include "algorithms/node_analysis.h"
#include "algorithms/suggestion_engine.h"
#include "algorithms/topological_order.h"
#include "interop/native_bridge.h"
#include "planning/forecast.h"
#include "planning/planning_analysis.h"
#include "planning/work_planner.h"

namespace
{
using coordina::graph::GraphSnapshot;
using coordina::graph::WorkItem;

template <typename Result, typename Query>
Result query_graph(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  Result invalid,
  Query query)
{
  if (coordina::graph::is_missing(nodes, node_count)
    || coordina::graph::is_missing(edges, edge_count))
  {
    return invalid;
  }

  return query(coordina::graph::build_snapshot(nodes, node_count, edges, edge_count));
}

template <typename Result, typename Query>
Result query_work(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  const coordina_graph_work_item* work_items,
  std::size_t work_item_count,
  Result invalid,
  Query query)
{
  if (coordina::graph::is_missing(work_items, work_item_count))
  {
    return invalid;
  }

  return query_graph(
    nodes,
    node_count,
    edges,
    edge_count,
    invalid,
    [&](const GraphSnapshot& graph)
    {
      return query(graph, coordina::graph::build_work_items(work_items, work_item_count));
    });
}

template <typename Query>
std::size_t write_node_query(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  coordina_graph_node_id* output,
  std::size_t output_capacity,
  Query query)
{
  return query_graph(
    nodes,
    node_count,
    edges,
    edge_count,
    std::size_t{0U},
    [&](const GraphSnapshot& graph)
    {
      return coordina::graph::write_nodes(query(graph), output, output_capacity);
    });
}

bool fits(std::size_t required, const void* items, std::size_t capacity) noexcept
{
  return required == 0U || (items != nullptr && capacity >= required);
}

bool fits(const coordina_graph_node_buffer& buffer) noexcept
{
  return fits(buffer.count, buffer.items, buffer.capacity);
}

void write_nodes(
  const std::vector<coordina::graph::NodeId>& nodes,
  const coordina_graph_node_buffer& buffer)
{
  coordina::graph::write_nodes(nodes, buffer.items, buffer.capacity);
}

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
}

extern "C" const char* coordina_graph_version(void)
{
  return "coordina-graph-engine/0.6.0";
}

extern "C" coordina_graph_edge_status coordina_graph_evaluate_edge(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  coordina_graph_node_id source,
  coordina_graph_node_id target)
{
  return query_graph(
    nodes,
    node_count,
    edges,
    edge_count,
    COORDINA_GRAPH_EDGE_INVALID_ARGUMENT,
    [&](const GraphSnapshot& graph)
    {
      return coordina::graph::evaluate_edge(
        graph,
        coordina::graph::to_node_id(source),
        coordina::graph::to_node_id(target));
    });
}

extern "C" int coordina_graph_has_path(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  coordina_graph_node_id source,
  coordina_graph_node_id target)
{
  return query_graph(
    nodes,
    node_count,
    edges,
    edge_count,
    -1,
    [&](const GraphSnapshot& graph)
    {
      return coordina::graph::has_path(
               graph,
               coordina::graph::to_node_id(source),
               coordina::graph::to_node_id(target))
        ? 1
        : 0;
    });
}

extern "C" std::size_t coordina_graph_suggest_targets(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  coordina_graph_node_id source,
  coordina_graph_node_id* output,
  std::size_t output_capacity)
{
  return write_node_query(
    nodes,
    node_count,
    edges,
    edge_count,
    output,
    output_capacity,
    [&](const GraphSnapshot& graph)
    {
      return coordina::graph::suggest_targets(
        graph,
        coordina::graph::to_node_id(source),
        output_capacity);
    });
}

extern "C" int coordina_graph_is_acyclic(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count)
{
  return query_graph(
    nodes,
    node_count,
    edges,
    edge_count,
    -1,
    [](const GraphSnapshot& graph)
    {
      return coordina::graph::is_acyclic(graph) ? 1 : 0;
    });
}

extern "C" std::size_t coordina_graph_topological_sort(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  coordina_graph_node_id* output,
  std::size_t output_capacity)
{
  return write_node_query(
    nodes,
    node_count,
    edges,
    edge_count,
    output,
    output_capacity,
    [](const GraphSnapshot& graph)
    {
      return coordina::graph::dependency_first_topological_order(graph);
    });
}

extern "C" std::size_t coordina_graph_ready_nodes(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  coordina_graph_node_id* output,
  std::size_t output_capacity)
{
  return write_node_query(
    nodes,
    node_count,
    edges,
    edge_count,
    output,
    output_capacity,
    [](const GraphSnapshot& graph)
    {
      return coordina::graph::ready_nodes(graph);
    });
}

extern "C" std::size_t coordina_graph_impacted_dependents(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  coordina_graph_node_id dependency,
  coordina_graph_node_id* output,
  std::size_t output_capacity)
{
  return write_node_query(
    nodes,
    node_count,
    edges,
    edge_count,
    output,
    output_capacity,
    [&](const GraphSnapshot& graph)
    {
      return coordina::graph::reachable_dependents(graph, coordina::graph::to_node_id(dependency));
    });
}

extern "C" std::size_t coordina_graph_analyze_nodes(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  coordina_graph_node_analysis* output,
  std::size_t output_capacity)
{
  return query_graph(
    nodes,
    node_count,
    edges,
    edge_count,
    std::size_t{0U},
    [&](const GraphSnapshot& graph)
    {
      return coordina::graph::write_node_analyses(
        coordina::graph::analyze_nodes(graph),
        output,
        output_capacity);
    });
}

extern "C" std::size_t coordina_graph_critical_path(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  const coordina_graph_work_item* work_items,
  std::size_t work_item_count,
  coordina_graph_node_id* output,
  std::size_t output_capacity)
{
  return query_work(
    nodes,
    node_count,
    edges,
    edge_count,
    work_items,
    work_item_count,
    std::size_t{0U},
    [&](const GraphSnapshot& graph, const std::vector<WorkItem>& items)
    {
      const auto path = coordina::graph::critical_path(
        graph,
        coordina::graph::dependency_first_topological_order(graph),
        coordina::graph::open_card_hours(graph, items));

      return coordina::graph::write_nodes(path, output, output_capacity);
    });
}

extern "C" std::size_t coordina_graph_plan_work(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  const coordina_graph_work_item* work_items,
  std::size_t work_item_count,
  const coordina_graph_planning_options* options,
  coordina_graph_work_recommendation* output,
  std::size_t output_capacity)
{
  return query_work(
    nodes,
    node_count,
    edges,
    edge_count,
    work_items,
    work_item_count,
    std::size_t{0U},
    [&](const GraphSnapshot& graph, const std::vector<WorkItem>& items)
    {
      const auto recommendations =
        coordina::graph::plan_work(graph, items, coordina::graph::build_planning_options(options));

      return coordina::graph::write_work_recommendations(recommendations, output, output_capacity);
    });
}

extern "C" coordina_graph_status coordina_graph_analyze_board(
  const coordina_graph_node_id* nodes,
  std::size_t node_count,
  const coordina_graph_edge* edges,
  std::size_t edge_count,
  const coordina_graph_work_item* work_items,
  std::size_t work_item_count,
  const coordina_graph_planning_options* options,
  coordina_graph_board_analysis* output)
{
  if (output == nullptr)
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
      const auto analysis = coordina::graph::analyze_board(
        graph,
        items,
        coordina::graph::build_planning_options(options));
      const auto& planning = analysis.planning;

      output->is_acyclic = planning.is_acyclic ? 1U : 0U;
      output->ready_nodes.count = planning.ready_nodes.size();
      output->dependency_order.count = planning.dependency_order.size();
      output->critical_path.count = planning.critical_path.size();
      output->node_analysis_count = planning.node_analyses.size();
      output->recommendation_count = analysis.recommendations.size();

      if (!fits(output->ready_nodes) || !fits(output->dependency_order)
        || !fits(output->critical_path)
        || !fits(output->node_analysis_count, output->node_analyses, output->node_analysis_capacity)
        || !fits(
          output->recommendation_count,
          output->recommendations,
          output->recommendation_capacity))
      {
        return COORDINA_GRAPH_INSUFFICIENT_CAPACITY;
      }

      write_nodes(planning.ready_nodes, output->ready_nodes);
      write_nodes(planning.dependency_order, output->dependency_order);
      write_nodes(planning.critical_path, output->critical_path);
      coordina::graph::write_node_analyses(
        planning.node_analyses,
        output->node_analyses,
        output->node_analysis_capacity);
      coordina::graph::write_work_recommendations(
        analysis.recommendations,
        output->recommendations,
        output->recommendation_capacity);

      return COORDINA_GRAPH_OK;
    });
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
  if (options == nullptr || output == nullptr || options->run_count == 0U
    || coordina::graph::is_missing(assignees, assignee_count))
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
      const coordina::graph::ForecastModel model(
        graph,
        items,
        coordina::graph::build_assignees(assignees, assignee_count),
        coordina::graph::to_node_id(options->milestone),
        options->person_count);

      if (model.error() != coordina::graph::ForecastError::none)
      {
        return to_status(model.error());
      }

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
