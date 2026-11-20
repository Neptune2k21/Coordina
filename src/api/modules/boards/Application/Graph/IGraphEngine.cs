namespace Coordina.Api.Modules.Boards.Application.Graph;

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

  IReadOnlyCollection<GraphNodeAnalysis<TNode>> AnalyzeNodes(
    IReadOnlyCollection<TNode> nodes,
    IReadOnlyCollection<GraphEdge<TNode>> edges);

  /// <summary>The longest chain of open cards, weighted by their sizes.</summary>
  IReadOnlyCollection<TNode> FindCriticalPath(
    IReadOnlyCollection<TNode> nodes,
    IReadOnlyCollection<GraphEdge<TNode>> edges,
    IReadOnlyCollection<GraphWorkItem<TNode>> workItems);

  IReadOnlyCollection<GraphWorkRecommendation<TNode>> PlanWork(
    IReadOnlyCollection<TNode> nodes,
    IReadOnlyCollection<GraphEdge<TNode>> edges,
    IReadOnlyCollection<GraphWorkItem<TNode>> workItems,
    GraphPlanningOptions? options = null);

  GraphBoardAnalysis<TNode> AnalyzeBoard(
    IReadOnlyCollection<TNode> nodes,
    IReadOnlyCollection<GraphEdge<TNode>> edges,
    IReadOnlyCollection<GraphWorkItem<TNode>> workItems,
    GraphPlanningOptions? options = null);

  /// <summary>Simulates when the milestone's open cards are done; see <c>coordina_graph_forecast</c>.</summary>
  GraphMilestoneForecast<TNode> Forecast(
    IReadOnlyCollection<TNode> nodes,
    IReadOnlyCollection<GraphEdge<TNode>> edges,
    IReadOnlyCollection<GraphWorkItem<TNode>> workItems,
    IReadOnlyCollection<GraphAssignee<TNode>> assignees,
    GraphForecastOptions<TNode> options);
}
