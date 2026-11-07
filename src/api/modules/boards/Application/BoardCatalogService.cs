using Coordina.Api.Core.Results;
using Coordina.Api.Core.Time;
using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Modules.Projects.Application;

namespace Coordina.Api.Modules.Boards.Application;

public sealed class BoardCatalogService(
  IBoardStore boards,
  IProjectAccessGuard projectAccess,
  TimeProvider clock) : IBoardCatalogService
{
  private const string ProjectAlreadyHasBoard = "This project already has a board.";

  public IReadOnlyCollection<BoardTemplateResponse> ListTemplates() =>
    BoardTemplateCatalog.All
      .Select(BoardResponseMapper.ToResponse)
      .ToArray();

  public async Task<Result<BoardResponse>> GetDefaultAsync(
    Guid workspaceId,
    Guid projectId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var access = await projectAccess.AuthorizeReadAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (!access.IsSuccess)
    {
      return access.Error;
    }

    var board = await boards.FindDefaultForProjectAsync(
      workspaceId,
      projectId,
      cancellationToken);

    return board is null
      ? Error.NotFound()
      : Result.Success(BoardResponseMapper.ToResponse(board));
  }

  public async Task<Result<BoardResponse>> CreateAsync(
    Guid workspaceId,
    Guid projectId,
    CreateBoardRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var access = await projectAccess.AuthorizeWriteAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (!access.IsSuccess)
    {
      return access.Error;
    }

    var errors = BoardRules.ValidateBoard(
      request.Name,
      request.Template,
      request.CustomListTitles,
      request.IncludeExampleCards,
      clock.GetUtcToday(),
      out var template,
      out var listSeeds);

    if (errors.HasErrors)
    {
      return errors.ToError();
    }

    var existing = await boards.FindDefaultForProjectAsync(
      workspaceId,
      projectId,
      cancellationToken);

    if (existing is not null)
    {
      return Error.Conflict(ProjectAlreadyHasBoard);
    }

    var board = await boards.CreateAsync(
      workspaceId,
      projectId,
      request.Name!.Trim(),
      template!.Value,
      listSeeds,
      clock.GetUtcNow(),
      cancellationToken);

    return board is null
      ? Error.Conflict(ProjectAlreadyHasBoard)
      : Result.Success(BoardResponseMapper.ToResponse(board));
  }
}
