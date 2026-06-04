using Coordina.Api.Modules.Tasks.Contracts;
using Coordina.Api.Modules.Tasks.Domain;

namespace Coordina.Api.Modules.Tasks.Application;

internal static class BoardGraphResponseMapper
{
  public static BoardGraphResponse ToResponse(BoardGraphAnalysis analysis) =>
    new(
      analysis.BoardId,
      analysis.IsAcyclic,
      ToCards(analysis.ReadyCards),
      ToCards(analysis.UnblockedCards),
      ToCards(analysis.DependencyOrder));

  public static BoardCardDependencyAnalysisResponse ToResponse(
    BoardCardDependencyAnalysis analysis) =>
    new(
      analysis.CardId,
      analysis.IsStructurallyReady,
      analysis.IsUnblocked,
      ToCards(analysis.BlockingDependencies),
      ToCards(analysis.SuggestedDependencies),
      ToCards(analysis.ImpactedDependents));

  private static BoardGraphCardResponse[] ToCards(
    IReadOnlyCollection<ProjectBoardCard> cards) =>
    cards
      .Select(card => new BoardGraphCardResponse(
        card.Id,
        card.ListId,
        card.Title,
        card.IsCompleted))
      .ToArray();
}
