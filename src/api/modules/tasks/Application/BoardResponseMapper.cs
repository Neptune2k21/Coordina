using Coordina.Api.Modules.Tasks.Contracts;
using Coordina.Api.Modules.Tasks.Domain;

namespace Coordina.Api.Modules.Tasks.Application;

internal static class BoardResponseMapper
{
  public static BoardResponse ToResponse(ProjectBoard board) =>
    new(
      board.Id,
      board.ProjectId,
      board.Name,
      ToApiTemplate(board.Template),
      board.CreatedAt,
      board.UpdatedAt,
      board.Lists
        .OrderBy(list => list.Position)
        .Select(ToResponse)
        .ToArray());

  private static BoardListResponse ToResponse(ProjectBoardList list) =>
    new(
      list.Id,
      list.BoardId,
      list.Title,
      list.Position,
      list.CreatedAt,
      list.UpdatedAt,
      list.Cards
        .OrderBy(card => card.Position)
        .Select(ToResponse)
        .ToArray());

  private static BoardCardResponse ToResponse(ProjectBoardCard card) =>
    new(
      card.Id,
      card.BoardId,
      card.ListId,
      card.Title,
      card.Description,
      card.Priority?.ToString().ToUpperInvariant(),
      card.DueDate,
      card.Labels,
      card.Position,
      card.CreatedAt,
      card.UpdatedAt,
      card.Assignees
        .Select(assignee => new BoardCardAssigneeResponse(
          assignee.UserId,
          assignee.Name,
          assignee.Email))
        .ToArray());

  private static string ToApiTemplate(BoardTemplate template)
  {
    return template switch
    {
      BoardTemplate.AgileScrum => "AGILE_SCRUM",
      BoardTemplate.BugTracking => "BUG_TRACKING",
      BoardTemplate.ProductRoadmap => "PRODUCT_ROADMAP",
      BoardTemplate.Custom => "CUSTOM",
      _ => "BASIC"
    };
  }
}
