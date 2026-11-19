using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Contracts;

namespace Coordina.Api.Modules.Boards.Application;

public interface IBoardMilestoneService
{
  /// <summary>The board's milestones, soonest target first, each with its forecast.</summary>
  Task<Result<IReadOnlyCollection<BoardMilestoneResponse>>> GetMilestonesAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid userId,
    CancellationToken cancellationToken);
}
