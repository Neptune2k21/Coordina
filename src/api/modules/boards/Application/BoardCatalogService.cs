using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Projects.Application;

namespace Coordina.Api.Modules.Boards.Application;

public sealed class BoardCatalogService(
  IBoardStore boards,
  IProjectAccessGuard projectAccess) : IBoardCatalogService
{
  public async Task<BoardResult<BoardResponse>> GetDefaultAsync(
    Guid workspaceId,
    Guid projectId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var access = await projectAccess.FindAccessAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (access is null)
    {
      return new BoardResult<BoardResponse>(BoardResultStatus.NotFound);
    }

    var board = await boards.FindDefaultForProjectAsync(
      workspaceId,
      projectId,
      cancellationToken);

    return board is null
      ? new BoardResult<BoardResponse>(BoardResultStatus.NotFound)
      : new BoardResult<BoardResponse>(
        BoardResultStatus.Success,
        BoardResponseMapper.ToResponse(board));
  }

  public async Task<BoardResult<BoardResponse>> CreateAsync(
    Guid workspaceId,
    Guid projectId,
    CreateBoardRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var access = await projectAccess.FindWritableAccessAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (access.Status != ProjectAccessStatus.Granted)
    {
      return BoardResults.AccessFailure<BoardResponse>(access.Status);
    }

    var template = BoardRules.ParseTemplate(request.Template, out var templateError);
    var listSeeds = template is null
      ? []
      : BoardRules.BuildListSeeds(template.Value, request.CustomListTitles);
    var errors = BoardRules.ValidateBoard(
      request.Name,
      template,
      templateError,
      listSeeds);

    if (errors.Count > 0)
    {
      return new BoardResult<BoardResponse>(
        BoardResultStatus.ValidationError,
        Errors: errors);
    }

    var now = DateTimeOffset.UtcNow;
    var board = await boards.CreateAsync(
      workspaceId,
      projectId,
      request.Name!.Trim(),
      template!.Value,
      listSeeds,
      now,
      cancellationToken);

    return new BoardResult<BoardResponse>(
      BoardResultStatus.Success,
      BoardResponseMapper.ToResponse(board));
  }
}
