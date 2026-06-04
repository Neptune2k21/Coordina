using Coordina.Api.Modules.Tasks.Application.Graph;
using Coordina.Api.Modules.Tasks.Domain;

namespace Coordina.Api.Modules.Tasks.Application;

internal static class BoardDependencyGraph
{
  private const int DefaultSuggestionLimit = 8;

  public static GraphEdgeEvaluation EvaluateDependency(
    IGraphEngine<Guid> graphEngine,
    ProjectBoard board,
    Guid cardId,
    Guid dependsOnCardId)
  {
    ArgumentNullException.ThrowIfNull(graphEngine);
    ArgumentNullException.ThrowIfNull(board);

    var graph = ToGraph(board);
    return graphEngine.EvaluateEdge(
      graph.Nodes,
      graph.Edges,
      cardId,
      dependsOnCardId);
  }

  public static BoardGraphAnalysis AnalyzeBoard(
    IGraphEngine<Guid> graphEngine,
    ProjectBoard board)
  {
    ArgumentNullException.ThrowIfNull(graphEngine);
    ArgumentNullException.ThrowIfNull(board);

    var graph = ToGraph(board);
    var cardsById = graph.Cards.ToDictionary(card => card.Id);

    return new BoardGraphAnalysis(
      board.Id,
      graphEngine.IsAcyclic(graph.Nodes, graph.Edges),
      ToCards(
        graphEngine.FindReadyNodes(graph.Nodes, graph.Edges),
        cardsById),
      graph.Cards
        .Where(card => card.Dependencies.All(dependency =>
          cardsById.TryGetValue(dependency.CardId, out var dependencyCard)
            && dependencyCard.IsCompleted))
        .ToArray(),
      ToCards(
        graphEngine.SortTopologically(graph.Nodes, graph.Edges),
        cardsById));
  }

  public static BoardCardDependencyAnalysis? AnalyzeCard(
    IGraphEngine<Guid> graphEngine,
    ProjectBoard board,
    Guid cardId,
    int maxSuggestions = DefaultSuggestionLimit)
  {
    ArgumentNullException.ThrowIfNull(graphEngine);
    ArgumentNullException.ThrowIfNull(board);

    var graph = ToGraph(board);
    var cardsById = graph.Cards.ToDictionary(card => card.Id);

    if (!cardsById.TryGetValue(cardId, out var card))
    {
      return null;
    }

    var blockingDependencies = card.Dependencies
      .Select(dependency => cardsById.TryGetValue(
        dependency.CardId,
        out var dependencyCard)
          ? dependencyCard
          : null)
      .OfType<ProjectBoardCard>()
      .Where(dependency => !dependency.IsCompleted)
      .ToArray();
    var readyCardIds = graphEngine.FindReadyNodes(graph.Nodes, graph.Edges)
      .ToHashSet();
    var suggestions = graphEngine.SuggestTargets(
      graph.Nodes,
      graph.Edges,
      cardId,
      maxSuggestions);
    var impacted = graphEngine.FindImpactedDependents(
      graph.Nodes,
      graph.Edges,
      cardId);

    return new BoardCardDependencyAnalysis(
      cardId,
      readyCardIds.Contains(cardId),
      blockingDependencies.Length == 0,
      blockingDependencies,
      ToCards(suggestions, cardsById),
      ToCards(impacted, cardsById));
  }

  private static BoardGraphSnapshot ToGraph(ProjectBoard board)
  {
    var cards = board.Lists
      .SelectMany(list => list.Cards)
      .ToArray();
    var cardIds = cards
      .Select(card => card.Id)
      .ToArray();
    var edges = cards
      .SelectMany(card => card.Dependencies.Select(dependency =>
        new GraphEdge<Guid>(card.Id, dependency.CardId)))
      .ToArray();

    return new BoardGraphSnapshot(cards, cardIds, edges);
  }

  private static ProjectBoardCard[] ToCards(
    IReadOnlyCollection<Guid> cardIds,
    Dictionary<Guid, ProjectBoardCard> cardsById) =>
    cardIds
      .Select(cardId => cardsById.TryGetValue(cardId, out var card)
        ? card
        : null)
      .OfType<ProjectBoardCard>()
      .ToArray();

  private sealed record BoardGraphSnapshot(
    IReadOnlyCollection<ProjectBoardCard> Cards,
    IReadOnlyCollection<Guid> Nodes,
    IReadOnlyCollection<GraphEdge<Guid>> Edges);
}

internal sealed record BoardGraphAnalysis(
  Guid BoardId,
  bool IsAcyclic,
  IReadOnlyCollection<ProjectBoardCard> ReadyCards,
  IReadOnlyCollection<ProjectBoardCard> UnblockedCards,
  IReadOnlyCollection<ProjectBoardCard> DependencyOrder);

internal sealed record BoardCardDependencyAnalysis(
  Guid CardId,
  bool IsStructurallyReady,
  bool IsUnblocked,
  IReadOnlyCollection<ProjectBoardCard> BlockingDependencies,
  IReadOnlyCollection<ProjectBoardCard> SuggestedDependencies,
  IReadOnlyCollection<ProjectBoardCard> ImpactedDependents);
