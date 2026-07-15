namespace Coordina.Api.Modules.Tasks.Domain;

public sealed record ProjectBoardList(
  Guid Id,
  Guid BoardId,
  Guid ProjectId,
  Guid WorkspaceId,
  string Title,
  int Position,
  DateTimeOffset CreatedAt,
  DateTimeOffset UpdatedAt,
  IReadOnlyCollection<ProjectBoardCard> Cards);
