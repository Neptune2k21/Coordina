#include "abi_fixtures.h"
#include "coordina_graph_engine.h"

#include <cassert>
#include <cstddef>
#include <cstdint>
#include <vector>

namespace
{
using namespace coordina::graph::fixtures;

// 2 depends on 1; 3 and 4 depend on 2.
Graph dependency_tree()
{
  return Graph(4U, {{2U, 1U}, {3U, 2U}, {4U, 2U}});
}

void detects_cycles()
{
  const Graph chain(3U, {{1U, 2U}, {2U, 3U}});

  assert(chain.call(coordina_graph_evaluate_edge, node(3U), node(1U)) == COORDINA_GRAPH_EDGE_CYCLE);
}

void suggests_only_safe_targets()
{
  const Graph graph(4U, {{1U, 2U}, {3U, 1U}});

  assert(graph.ids(coordina_graph_suggest_targets, node(1U)) == Ids{4U});
}

void sorts_dependency_first()
{
  assert(dependency_tree().ids(coordina_graph_topological_sort) == (Ids{1U, 2U, 3U, 4U}));
}

void finds_ready_nodes()
{
  const Graph graph(3U, {{2U, 1U}, {3U, 1U}});

  assert(graph.ids(coordina_graph_ready_nodes) == Ids{1U});
}

void finds_impacted_dependents()
{
  assert(dependency_tree().ids(coordina_graph_impacted_dependents, node(1U)) == (Ids{2U, 3U, 4U}));
}

void analyzes_node_pressure()
{
  std::vector<coordina_graph_node_analysis> output(4U);

  const auto count =
    dependency_tree().call(coordina_graph_analyze_nodes, output.data(), output.size());

  assert(count == 4U);
  assert(output[0U].node.low == 1U);
  assert(output[0U].dependency_count == 0U);
  assert(output[0U].dependent_count == 1U);
  assert(output[0U].dependency_depth == 0U);
  assert(output[0U].dependent_depth == 2U);
  assert(output[0U].transitive_dependent_count == 3U);
  assert(output[2U].node.low == 3U);
  assert(output[2U].dependency_count == 1U);
  assert(output[2U].dependent_count == 0U);
  assert(output[2U].dependency_depth == 2U);
  assert(output[2U].dependent_depth == 0U);
  assert(output[2U].transitive_dependent_count == 0U);
}

void finds_critical_path()
{
  assert(dependency_tree().ids(coordina_graph_critical_path, nullptr, 0U) == (Ids{1U, 2U, 3U}));
}

struct BatchBuffers
{
  explicit BatchBuffers(std::size_t size)
      : ready(size), order(size), critical(size), analyses(size), recommendations(size)
  {
    output.ready_nodes = {ready.data(), ready.size(), 0U};
    output.dependency_order = {order.data(), order.size(), 0U};
    output.critical_path = {critical.data(), critical.size(), 0U};
    output.node_analyses = analyses.data();
    output.node_analysis_capacity = analyses.size();
    output.recommendations = recommendations.data();
    output.recommendation_capacity = recommendations.size();
  }

  std::vector<coordina_graph_node_id> ready;
  std::vector<coordina_graph_node_id> order;
  std::vector<coordina_graph_node_id> critical;
  std::vector<coordina_graph_node_analysis> analyses;
  std::vector<coordina_graph_work_recommendation> recommendations;
  coordina_graph_board_analysis output{};
};

Ids written(const std::vector<coordina_graph_node_id>& buffer, std::size_t count)
{
  return ids_of({buffer.begin(), buffer.begin() + static_cast<std::ptrdiff_t>(count)});
}

void transitive_counts_match_reachable_dependents()
{
  constexpr std::size_t size = 300U;
  Graph graph(size, {});

  for (std::size_t index = 0U; index < size; ++index)
  {
    if (index > 0U && index % 4U != 0U)
    {
      graph.edges.push_back(coordina_graph_edge{node(index + 1U), node(index)});
    }

    if (index > 2U)
    {
      graph.edges.push_back(coordina_graph_edge{node(index + 1U), node((index / 3U) + 1U)});
    }
  }

  std::vector<coordina_graph_node_analysis> analyses(size);
  const auto count = graph.call(coordina_graph_analyze_nodes, analyses.data(), size);
  assert(count == size);

  for (const auto& analysis : analyses)
  {
    const auto expected = graph.ids(coordina_graph_impacted_dependents, analysis.node).size();
    assert(analysis.transitive_dependent_count == expected);
  }
}

void batch_analysis_matches_individual_calls()
{
  const Graph board(5U, {{2U, 1U}, {3U, 2U}, {4U, 2U}});
  std::vector<coordina_graph_work_item> items;

  for (unsigned long long id = 1U; id <= 5U; ++id)
  {
    auto item = work_item(id, static_cast<std::uint32_t>(5U - id));
    item.size = static_cast<std::uint32_t>(id);
    item.assignment = static_cast<std::uint32_t>(id % 3U);
    item.is_in_progress = id == 4U ? 1U : 0U;
    items.push_back(item);
  }

  const auto focus = options(COORDINA_GRAPH_SIZE_M);
  BatchBuffers batch(5U);

  const auto status =
    board.call(coordina_graph_analyze_board, items.data(), items.size(), &focus, &batch.output);

  assert(status == COORDINA_GRAPH_OK);
  assert(batch.output.is_acyclic == 1U);
  assert(board.call(coordina_graph_is_acyclic) == 1);
  assert(
    board.ids(coordina_graph_ready_nodes) == written(batch.ready, batch.output.ready_nodes.count));
  assert(
    board.ids(coordina_graph_topological_sort)
    == written(batch.order, batch.output.dependency_order.count));
  assert(
    board.ids(coordina_graph_critical_path, items.data(), items.size())
    == written(batch.critical, batch.output.critical_path.count));

  coordina_graph_node_analysis expected_analyses[5U] = {};
  auto count = board.call(coordina_graph_analyze_nodes, expected_analyses, 5U);
  assert(count == batch.output.node_analysis_count);
  for (std::size_t index = 0U; index < count; ++index)
  {
    assert(same_node(expected_analyses[index].node, batch.analyses[index].node));
    assert(
      expected_analyses[index].transitive_dependent_count
      == batch.analyses[index].transitive_dependent_count);
    assert(expected_analyses[index].dependent_depth == batch.analyses[index].dependent_depth);
  }

  coordina_graph_work_recommendation expected_plan[5U] = {};
  count =
    board.call(coordina_graph_plan_work, items.data(), items.size(), &focus, expected_plan, 5U);
  assert(count == batch.output.recommendation_count);
  for (std::size_t index = 0U; index < count; ++index)
  {
    assert(same_node(expected_plan[index].node, batch.recommendations[index].node));
    assert(expected_plan[index].score == batch.recommendations[index].score);
    assert(expected_plan[index].effort_score == batch.recommendations[index].effort_score);
  }
}

void batch_analysis_reports_missing_capacity_without_writing()
{
  const Graph board(3U, {{2U, 1U}});
  const std::vector<coordina_graph_work_item> items =
    {work_item(1U, 0U), work_item(2U, 1U), work_item(3U, 2U)};
  BatchBuffers batch(1U);
  batch.ready[0U] = node(99U);

  const auto status =
    board.call(coordina_graph_analyze_board, items.data(), items.size(), nullptr, &batch.output);

  assert(status == COORDINA_GRAPH_INSUFFICIENT_CAPACITY);
  assert(batch.output.ready_nodes.count == 2U);
  assert(batch.output.dependency_order.count == 3U);
  assert(batch.output.node_analysis_count == 3U);
  assert(batch.output.recommendation_count == 3U);
  assert(batch.ready[0U].low == 99U);
}

void batch_analysis_rejects_invalid_arguments()
{
  const coordina_graph_node_id nodes[] = {node(1U)};
  BatchBuffers batch(1U);

  assert(
    coordina_graph_analyze_board(nodes, 1U, nullptr, 0U, nullptr, 0U, nullptr, nullptr)
    == COORDINA_GRAPH_INVALID_ARGUMENT);
  assert(
    coordina_graph_analyze_board(nullptr, 1U, nullptr, 0U, nullptr, 0U, nullptr, &batch.output)
    == COORDINA_GRAPH_INVALID_ARGUMENT);
  assert(
    coordina_graph_analyze_board(nodes, 1U, nullptr, 0U, nullptr, 2U, nullptr, &batch.output)
    == COORDINA_GRAPH_INVALID_ARGUMENT);
}

void batch_analysis_flags_cycles()
{
  const Graph board(2U, {{1U, 2U}, {2U, 1U}});
  const std::vector<coordina_graph_work_item> items = {work_item(1U, 0U), work_item(2U, 1U)};
  BatchBuffers batch(2U);

  const auto status =
    board.call(coordina_graph_analyze_board, items.data(), items.size(), nullptr, &batch.output);

  assert(status == COORDINA_GRAPH_OK);
  assert(batch.output.is_acyclic == 0U);
  assert(batch.output.dependency_order.count == 0U);
  assert(batch.output.critical_path.count == 0U);
  assert(batch.output.recommendation_count == 0U);
}
}

int main()
{
  detects_cycles();
  suggests_only_safe_targets();
  sorts_dependency_first();
  finds_ready_nodes();
  finds_impacted_dependents();
  analyzes_node_pressure();
  finds_critical_path();
  transitive_counts_match_reachable_dependents();
  batch_analysis_matches_individual_calls();
  batch_analysis_reports_missing_capacity_without_writing();
  batch_analysis_rejects_invalid_arguments();
  batch_analysis_flags_cycles();

  return 0;
}
