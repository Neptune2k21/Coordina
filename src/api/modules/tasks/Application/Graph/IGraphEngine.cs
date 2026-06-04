namespace Coordina.Api.Modules.Tasks.Application.Graph;

public interface IGraphEngine<TNode>
  where TNode : notnull
{
  GraphEdgeEvaluation EvaluateEdge(
    IReadOnlyCollection<TNode> nodes,
    IReadOnlyCollection<GraphEdge<TNode>> edges,
    TNode source,
    TNode target);

  IReadOnlyCollection<TNode> SuggestTargets(
    IReadOnlyCollection<TNode> nodes,
    IReadOnlyCollection<GraphEdge<TNode>> edges,
    TNode source,
    int maxSuggestions);

  bool IsAcyclic(
    IReadOnlyCollection<TNode> nodes,
    IReadOnlyCollection<GraphEdge<TNode>> edges);

  IReadOnlyCollection<TNode> SortTopologically(
    IReadOnlyCollection<TNode> nodes,
    IReadOnlyCollection<GraphEdge<TNode>> edges);

  IReadOnlyCollection<TNode> FindReadyNodes(
    IReadOnlyCollection<TNode> nodes,
    IReadOnlyCollection<GraphEdge<TNode>> edges);

  IReadOnlyCollection<TNode> FindImpactedDependents(
    IReadOnlyCollection<TNode> nodes,
    IReadOnlyCollection<GraphEdge<TNode>> edges,
    TNode dependency);
}
