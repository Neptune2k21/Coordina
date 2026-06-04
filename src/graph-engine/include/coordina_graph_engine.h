#ifndef COORDINA_GRAPH_ENGINE_H
#define COORDINA_GRAPH_ENGINE_H

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32)
#define COORDINA_GRAPH_EXPORT __declspec(dllexport)
#else
#define COORDINA_GRAPH_EXPORT __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef struct coordina_graph_node_id
{
  uint64_t high;
  uint64_t low;
} coordina_graph_node_id;

typedef struct coordina_graph_edge
{
  coordina_graph_node_id source;
  coordina_graph_node_id target;
} coordina_graph_edge;

typedef enum coordina_graph_edge_status
{
  COORDINA_GRAPH_EDGE_ALLOWED = 0,
  COORDINA_GRAPH_EDGE_MISSING_SOURCE = 1,
  COORDINA_GRAPH_EDGE_MISSING_TARGET = 2,
  COORDINA_GRAPH_EDGE_SELF_REFERENCE = 3,
  COORDINA_GRAPH_EDGE_DUPLICATE = 4,
  COORDINA_GRAPH_EDGE_CYCLE = 5,
  COORDINA_GRAPH_EDGE_INVALID_ARGUMENT = 100
} coordina_graph_edge_status;

COORDINA_GRAPH_EXPORT const char* coordina_graph_version(void);

COORDINA_GRAPH_EXPORT coordina_graph_edge_status coordina_graph_evaluate_edge(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  coordina_graph_node_id source,
  coordina_graph_node_id target);

COORDINA_GRAPH_EXPORT int coordina_graph_has_path(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  coordina_graph_node_id source,
  coordina_graph_node_id target);

COORDINA_GRAPH_EXPORT size_t coordina_graph_suggest_targets(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  coordina_graph_node_id source,
  coordina_graph_node_id* output,
  size_t output_capacity);

COORDINA_GRAPH_EXPORT int coordina_graph_is_acyclic(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count);

COORDINA_GRAPH_EXPORT size_t coordina_graph_topological_sort(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  coordina_graph_node_id* output,
  size_t output_capacity);

COORDINA_GRAPH_EXPORT size_t coordina_graph_ready_nodes(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  coordina_graph_node_id* output,
  size_t output_capacity);

COORDINA_GRAPH_EXPORT size_t coordina_graph_impacted_dependents(
  const coordina_graph_node_id* nodes,
  size_t node_count,
  const coordina_graph_edge* edges,
  size_t edge_count,
  coordina_graph_node_id dependency,
  coordina_graph_node_id* output,
  size_t output_capacity);

#ifdef __cplusplus
}
#endif

#endif
