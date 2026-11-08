using Coordina.Api.Modules.Boards.Contracts;

namespace Coordina.Api.Modules.Work.Contracts;

/// <summary>
/// The signed-in user's work across a workspace's projects, ranked by the
/// same planning score as each board's planning view.
/// </summary>
public sealed record MyWorkResponse(
  Guid WorkspaceId,
  IReadOnlyCollection<MyWorkItemResponse> Now,
  IReadOnlyCollection<MyWorkItemResponse> Next,
  int NextTotal,
  IReadOnlyCollection<MyWorkItemResponse> Blocked);

/// <summary>A card to work on, where it lives and why it is ranked there.</summary>
public sealed record MyWorkItemResponse(
  MyWorkProjectResponse Project,
  Guid BoardId,
  DateOnly? DueDate,
  bool IsUnassigned,
  BoardGraphPlanItemResponse Plan);

public sealed record MyWorkProjectResponse(
  Guid Id,
  string Name,
  string? Key,
  string? Icon,
  string? Color);
