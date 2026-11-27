using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Modules.Insights.Contracts;
using Coordina.Api.Shared.Enums;

namespace Coordina.Api.Modules.Insights.Application;

/// <summary>Puts what happened to a board into codes and dates.</summary>
internal static class BoardInsights
{
  public static BoardCardEventResponse ToResponse(BoardCardEvent cardEvent) =>
    new(
      cardEvent.Id,
      cardEvent.CardId,
      EnumCode.Format(cardEvent.Kind),
      cardEvent.From,
      cardEvent.To,
      cardEvent.OccurredAt);
}
