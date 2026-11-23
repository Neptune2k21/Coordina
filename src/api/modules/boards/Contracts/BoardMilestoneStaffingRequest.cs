namespace Coordina.Api.Modules.Boards.Contracts;

/// <summary>
/// Who a proposal may give cards to, all workspace members; none named means
/// the people assigned to open cards of the board, or the whole workspace
/// when nobody is. Cards that already have assignees only move when
/// <paramref name="AllowReassignment"/> is set.
/// </summary>
public sealed record BoardMilestoneStaffingRequest(
  IReadOnlyCollection<Guid>? People,
  bool AllowReassignment);

/// <summary>The cards to give to one person each, as a proposal lists them.</summary>
public sealed record BoardMilestoneStaffChangesRequest(
  IReadOnlyCollection<BoardMilestoneStaffChangeRequest>? Changes);

public sealed record BoardMilestoneStaffChangeRequest(Guid CardId, Guid UserId);
