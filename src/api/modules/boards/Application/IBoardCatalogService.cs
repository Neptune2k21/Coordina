using Coordina.Api.Modules.Boards.Contracts;

namespace Coordina.Api.Modules.Boards.Application;

public interface IBoardCatalogService
{
  Task<BoardResult<BoardResponse>> GetDefaultAsync(
    Guid workspaceId,
    Guid projectId,
    Guid userId,
    CancellationToken cancellationToken);

  Task<BoardResult<BoardResponse>> CreateAsync(
    Guid workspaceId,
    Guid projectId,
    CreateBoardRequest request,
    Guid userId,
    CancellationToken cancellationToken);
}
