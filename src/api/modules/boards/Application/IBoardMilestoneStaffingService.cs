using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Contracts;

namespace Coordina.Api.Modules.Boards.Application;

public interface IBoardMilestoneStaffingService
{
  /// <summary>Proposes who should take which card so a milestone ships soonest.</summary>
  Task<Result<BoardMilestoneStaffingResponse>> ProposeAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid milestoneId,
    BoardMilestoneStaffingRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  /// <summary>Measures giving the chosen cards to the chosen people, without changing anything.</summary>
  Task<Result<BoardMilestoneStaffingResponse>> EvaluateAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid milestoneId,
    BoardMilestoneStaffChangesRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  /// <summary>Makes each chosen person the only assignee of their card.</summary>
  Task<Result<BoardResponse>> ApplyAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid milestoneId,
    BoardMilestoneStaffChangesRequest request,
    Guid userId,
    CancellationToken cancellationToken);
}
