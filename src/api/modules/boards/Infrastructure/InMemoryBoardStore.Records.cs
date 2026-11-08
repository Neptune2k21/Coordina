using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Modules.Boards.Infrastructure;

// What the store keeps for each board, list, card, comment, subtask and dependency.
public sealed partial class InMemoryBoardStore
{
  private sealed record BoardRecord(
    Guid Id,
    Guid ProjectId,
    Guid WorkspaceId,
    string Name,
    BoardTemplate Template,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

  private sealed record ListRecord(
    Guid Id,
    Guid BoardId,
    Guid ProjectId,
    Guid WorkspaceId,
    string Title,
    int Position,
    BoardTaskStatus TaskStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

  private sealed record CardRecord(
    Guid Id,
    Guid BoardId,
    Guid ListId,
    Guid ProjectId,
    Guid WorkspaceId,
    string Title,
    string? Description,
    BoardCardPriority? Priority,
    DateOnly? DueDate,
    int? EstimatedMinutes,
    IReadOnlyCollection<string> Labels,
    bool IsCompleted,
    DateTimeOffset? CompletedAt,
    Guid? CompletedByUserId,
    int Position,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<Guid> AssigneeIds);

  private sealed record CommentRecord(
    Guid Id,
    Guid CardId,
    Guid UserId,
    string Body,
    DateTimeOffset CreatedAt);

  private sealed record SubtaskRecord(
    Guid Id,
    Guid CardId,
    string Title,
    bool IsCompleted,
    DateTimeOffset? CompletedAt,
    Guid? CompletedByUserId,
    int Position,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

  private sealed record DependencyRecord(
    Guid Id,
    Guid CardId,
    Guid DependsOnCardId,
    DateTimeOffset CreatedAt);
}
