namespace Coordina.Api.Modules.Tasks.Domain;

public sealed record ProjectBoard(
  Guid Id,
  Guid ProjectId,
  Guid WorkspaceId,
  string Name,
  BoardTemplate Template,
  DateTimeOffset CreatedAt,
  DateTimeOffset UpdatedAt,
  IReadOnlyCollection<ProjectBoardList> Lists);
