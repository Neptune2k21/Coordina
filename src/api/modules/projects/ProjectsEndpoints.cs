using System.Security.Claims;
using Coordina.Api.Modules.Projects.Application;
using Coordina.Api.Modules.Projects.Contracts;

namespace Coordina.Api.Modules.Projects;

public static class ProjectsEndpoints
{
  public static IEndpointRouteBuilder MapProjectsEndpoints(
    this IEndpointRouteBuilder app)
  {
    var group = app
      .MapGroup("/workspaces/{workspaceId:guid}/projects")
      .RequireAuthorization()
      .WithTags("Projects");

    group.MapPost("/", Create)
      .WithSummary("Create project")
      .WithDescription("Creates a project inside a workspace where the user is a member.");

    group.MapGet("/", List)
      .WithSummary("List projects")
      .WithDescription("Lists projects only within the requested workspace membership scope.");

    group.MapGet("/{projectId:guid}", Get)
      .WithSummary("Get project")
      .WithDescription("Returns a project only when it belongs to the requested workspace and user membership scope.");

    group.MapPatch("/{projectId:guid}", Update)
      .WithSummary("Update project")
      .WithDescription("Updates project metadata, owner, or lifecycle status when the user is allowed to manage the project.");

    group.MapDelete("/{projectId:guid}", Delete)
      .WithSummary("Delete project")
      .WithDescription("Archives a project in workspace scope. Workspace owners or project owners can archive.");

    group.MapDelete("/{projectId:guid}/permanent", PermanentlyDelete)
      .WithSummary("Permanently delete project")
      .WithDescription("Permanently deletes a project in workspace scope. Only workspace OWNER members can delete permanently.");

    return app;
  }

  private static async Task<IResult> Create(
    Guid workspaceId,
    CreateProjectRequest request,
    ClaimsPrincipal user,
    IProjectService projectService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await projectService.CreateAsync(
      workspaceId,
      request,
      userId,
      cancellationToken);

    return result.Status switch
    {
      ProjectResultStatus.Success => Results.Created(
        $"/workspaces/{workspaceId}/projects/{result.Value!.Id}",
        result.Value),
      ProjectResultStatus.ValidationError => Results.ValidationProblem(
        result.Errors?.ToDictionary() ?? new Dictionary<string, string[]>()),
      ProjectResultStatus.NotFound => Results.NotFound(),
      _ => Results.BadRequest()
    };
  }

  private static async Task<IResult> List(
    Guid workspaceId,
    bool? includeArchived,
    bool? includeCompleted,
    ClaimsPrincipal user,
    IProjectService projectService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await projectService.ListAsync(
      workspaceId,
      includeArchived == true,
      includeCompleted == true,
      userId,
      cancellationToken);

    return result.Status switch
    {
      ProjectResultStatus.Success => Results.Ok(result.Value),
      ProjectResultStatus.NotFound => Results.NotFound(),
      _ => Results.BadRequest()
    };
  }

  private static async Task<IResult> Get(
    Guid workspaceId,
    Guid projectId,
    ClaimsPrincipal user,
    IProjectService projectService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await projectService.GetAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    return result.Status switch
    {
      ProjectResultStatus.Success => Results.Ok(result.Value),
      ProjectResultStatus.NotFound => Results.NotFound(),
      _ => Results.BadRequest()
    };
  }

  private static async Task<IResult> Delete(
    Guid workspaceId,
    Guid projectId,
    ClaimsPrincipal user,
    IProjectService projectService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await projectService.DeleteAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    return result.Status switch
    {
      ProjectResultStatus.Success => Results.NoContent(),
      ProjectResultStatus.Forbidden => Results.Forbid(),
      ProjectResultStatus.NotFound => Results.NotFound(),
      ProjectResultStatus.Conflict => Results.Conflict(new
      {
        message = result.Message
      }),
      _ => Results.BadRequest()
    };
  }

  private static async Task<IResult> Update(
    Guid workspaceId,
    Guid projectId,
    UpdateProjectRequest request,
    ClaimsPrincipal user,
    IProjectService projectService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await projectService.UpdateAsync(
      workspaceId,
      projectId,
      request,
      userId,
      cancellationToken);

    return result.Status switch
    {
      ProjectResultStatus.Success => Results.Ok(result.Value),
      ProjectResultStatus.ValidationError => Results.ValidationProblem(
        result.Errors?.ToDictionary() ?? new Dictionary<string, string[]>()),
      ProjectResultStatus.Forbidden => Results.Forbid(),
      ProjectResultStatus.NotFound => Results.NotFound(),
      ProjectResultStatus.Conflict => Results.Conflict(new
      {
        message = result.Message
      }),
      _ => Results.BadRequest()
    };
  }

  private static async Task<IResult> PermanentlyDelete(
    Guid workspaceId,
    Guid projectId,
    ClaimsPrincipal user,
    IProjectService projectService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await projectService.PermanentlyDeleteAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    return result.Status switch
    {
      ProjectResultStatus.Success => Results.NoContent(),
      ProjectResultStatus.Forbidden => Results.Forbid(),
      ProjectResultStatus.NotFound => Results.NotFound(),
      _ => Results.BadRequest()
    };
  }

  private static bool TryGetUserId(
    ClaimsPrincipal user,
    out Guid userId)
  {
    return Guid.TryParse(
      user.FindFirstValue(ClaimTypes.NameIdentifier),
      out userId);
  }
}
