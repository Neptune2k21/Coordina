using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Work.Contracts;

namespace Coordina.Api.Modules.Work.Application;

public interface IMyWorkService
{
  Task<Result<MyWorkResponse>> GetAsync(
    Guid workspaceId,
    Guid userId,
    CancellationToken cancellationToken);
}
