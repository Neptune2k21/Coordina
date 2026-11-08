using Coordina.Api.Core.Http;
using Coordina.Api.Core.Results;
using Coordina.Api.Core.Security;
using Coordina.Api.Modules.Work.Application;
using Coordina.Api.Modules.Work.Contracts;

namespace Coordina.Api.Modules.Work;

public static class WorkEndpoints
{
  public static IEndpointRouteBuilder MapWorkEndpoints(this IEndpointRouteBuilder app)
  {
    var group = app.MapApiGroup("/me/work", "Work");

    group.MapGet("/", GetMyWork)
      .Produces<MyWorkResponse>(StatusCodes.Status200OK)
      .WithSummary("Get my work")
      .WithDescription("Ranks the signed-in user's cards across the workspace's active projects: what they are working on, what they can start next, and what is blocked. Each card keeps the score and reasons of its board's planning view.");

    return app;
  }

  private static async Task<IResult> GetMyWork(
    Guid? workspaceId,
    CurrentUser user,
    IMyWorkService work,
    CancellationToken cancellationToken)
  {
    if (workspaceId is null)
    {
      return Error.Validation(nameof(workspaceId), "Choose a workspace.").ToHttpResult();
    }

    var result = await work.GetAsync(workspaceId.Value, user.Id, cancellationToken);

    return result.ToOkResult();
  }
}
