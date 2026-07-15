using Coordina.Api.Modules.Projects.Application;

namespace Coordina.Api.Modules.Boards.Application;

internal static class BoardResults
{
  public static BoardResult<T> AccessFailure<T>(ProjectAccessStatus status) =>
    status == ProjectAccessStatus.ProjectReadOnly
      ? new BoardResult<T>(
        BoardResultStatus.Conflict,
        Message: ProjectAccessCheck.ReadOnlyMessage)
      : new BoardResult<T>(BoardResultStatus.NotFound);
}
