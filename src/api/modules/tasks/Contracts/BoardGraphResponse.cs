namespace Coordina.Api.Modules.Tasks.Contracts;

public sealed record BoardGraphResponse(
  Guid BoardId,
  bool IsAcyclic,
  IReadOnlyCollection<BoardGraphCardResponse> ReadyCards,
  IReadOnlyCollection<BoardGraphCardResponse> UnblockedCards,
  IReadOnlyCollection<BoardGraphCardResponse> DependencyOrder);

public sealed record BoardCardDependencyAnalysisResponse(
  Guid CardId,
  bool IsStructurallyReady,
  bool IsUnblocked,
  IReadOnlyCollection<BoardGraphCardResponse> BlockingDependencies,
  IReadOnlyCollection<BoardGraphCardResponse> SuggestedDependencies,
  IReadOnlyCollection<BoardGraphCardResponse> ImpactedDependents);

public sealed record BoardGraphCardResponse(
  Guid Id,
  Guid ListId,
  string Title,
  bool IsCompleted);
