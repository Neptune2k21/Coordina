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

  /// <summary>Simulates when every milestone's open cards are done; see <c>coordina_graph_forecast</c>.</summary>
  GraphForecast<TNode> Forecast(GraphBoard<TNode> board, GraphSimulation simulation);

  /// <summary>
  /// Proposes cards to take out of a milestone so it ships by its target,
  /// giving up as little value as possible. The milestone, cards in progress,
  /// the <paramref name="keep"/> cards and whatever they need are never proposed.
  /// </summary>
  GraphScopeCut<TNode> ProposeScope(
    GraphBoard<TNode> board,
    IReadOnlyCollection<TNode> keep,
    GraphScopeOptions<TNode> options);

  /// <summary>Measures a cut someone chose; cards outside the scope and the milestone are ignored.</summary>
  GraphScopeCut<TNode> EvaluateScope(
    GraphBoard<TNode> board,
    IReadOnlyCollection<TNode> cuts,
    GraphScopeOptions<TNode> options);

  /// <summary>
  /// Proposes who should take which card so the milestone ships soonest. A
  /// card only goes to one of the <paramref name="receivers"/>, preferably a
  /// <paramref name="skilled"/> one; see <c>coordina_graph_propose_staffing</c>.
  /// </summary>
  GraphStaffing<TNode> ProposeStaffing(
    GraphBoard<TNode> board,
    IReadOnlyCollection<int> receivers,
    IReadOnlyCollection<GraphAssignee<TNode>> skilled,
    GraphStaffingOptions<TNode> options);

  /// <summary>Measures changes someone chose, each giving a card to one person.</summary>
  GraphStaffing<TNode> EvaluateStaffing(
    GraphBoard<TNode> board,
    IReadOnlyCollection<GraphAssignee<TNode>> changes,
    GraphStaffingOptions<TNode> options);

  /// <summary>What the board's history has taught the forecasts.</summary>
  GraphCalibration Calibrate(GraphBoard<TNode> board);
}
