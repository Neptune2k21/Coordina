using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Contracts;

namespace Coordina.Api.Modules.Boards.Application;

public interface IBoardMilestoneScopeService
{
  /// <summary>Proposes cards to take out of a milestone so it ships by its target date.</summary>
  Task<Result<BoardMilestoneScopeResponse>> ProposeAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid milestoneId,
    BoardMilestoneScopeRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  /// <summary>Measures taking the chosen cards out of a milestone, without changing anything.</summary>
  Task<Result<BoardMilestoneScopeResponse>> EvaluateAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid milestoneId,
    BoardMilestoneCutRequest request,
    Guid userId,
    CancellationToken cancellationToken);

  /// <summary>
  /// Takes the chosen cards out of a milestone: it stops depending on every
  /// card that leaves its scope. No card is deleted.
  /// </summary>
  Task<Result<BoardResponse>> CutAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid milestoneId,
    BoardMilestoneCutRequest request,
    Guid userId,
    CancellationToken cancellationToken);
}
