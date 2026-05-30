using System.Security.Claims;
using Coordina.Api.Modules.Tasks.Application;
using Coordina.Api.Modules.Tasks.Contracts;

namespace Coordina.Api.Modules.Tasks;

public static class TasksEndpoints
{
  public static IEndpointRouteBuilder MapTasksEndpoints(
    this IEndpointRouteBuilder app)
  {
    var group = app
      .MapGroup("/workspaces/{workspaceId:guid}/projects/{projectId:guid}/tasks")
      .RequireAuthorization()
      .WithTags("Tasks");

    group.MapPost("/", Create)
      .WithSummary("Create task")
      .WithDescription("Creates a task inside a project the user can access.");

    group.MapGet("/", List)
      .WithSummary("List project tasks")
      .WithDescription("Lists tasks only inside the requested workspace and project scope.");

    group.MapPatch("/{taskId:guid}", Update)
      .WithSummary("Update task")
      .WithDescription("Updates task fields for project members.");

    group.MapPatch("/{taskId:guid}/status", ChangeStatus)
      .WithSummary("Change task status")
      .WithDescription("Moves a task between TODO, IN_PROGRESS, and DONE.");

    group.MapDelete("/{taskId:guid}", Delete)
      .WithSummary("Delete task")
      .WithDescription("Deletes a task. Workspace owners or project owners can delete tasks.");

    return app;
  }

  private static async Task<IResult> Create(
    Guid workspaceId,
    Guid projectId,
    CreateTaskRequest request,
    ClaimsPrincipal user,
    ITaskService taskService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await taskService.CreateAsync(
      workspaceId,
      projectId,
      request,
      userId,
      cancellationToken);

    return result.Status switch
    {
      TaskResultStatus.Success => Results.Created(
        $"/workspaces/{workspaceId}/projects/{projectId}/tasks/{result.Value!.Id}",
        result.Value),
      TaskResultStatus.ValidationError => Results.ValidationProblem(
        result.Errors?.ToDictionary() ?? new Dictionary<string, string[]>()),
      TaskResultStatus.NotFound => Results.NotFound(),
      TaskResultStatus.Conflict => Results.Conflict(new
      {
        message = result.Message
      }),
      _ => Results.BadRequest()
    };
  }

  private static async Task<IResult> List(
    Guid workspaceId,
    Guid projectId,
    ClaimsPrincipal user,
    ITaskService taskService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await taskService.ListAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    return result.Status switch
    {
      TaskResultStatus.Success => Results.Ok(result.Value),
      TaskResultStatus.NotFound => Results.NotFound(),
      _ => Results.BadRequest()
    };
  }

  private static async Task<IResult> Update(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
    UpdateTaskRequest request,
    ClaimsPrincipal user,
    ITaskService taskService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await taskService.UpdateAsync(
      workspaceId,
      projectId,
      taskId,
      request,
      userId,
      cancellationToken);

    return result.Status switch
    {
      TaskResultStatus.Success => Results.Ok(result.Value),
      TaskResultStatus.ValidationError => Results.ValidationProblem(
        result.Errors?.ToDictionary() ?? new Dictionary<string, string[]>()),
      TaskResultStatus.NotFound => Results.NotFound(),
      TaskResultStatus.Conflict => Results.Conflict(new
      {
        message = result.Message
      }),
      _ => Results.BadRequest()
    };
  }

  private static async Task<IResult> ChangeStatus(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
    ChangeTaskStatusRequest request,
    ClaimsPrincipal user,
    ITaskService taskService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await taskService.ChangeStatusAsync(
      workspaceId,
      projectId,
      taskId,
      request,
      userId,
      cancellationToken);

    return result.Status switch
    {
      TaskResultStatus.Success => Results.Ok(result.Value),
      TaskResultStatus.ValidationError => Results.ValidationProblem(
        result.Errors?.ToDictionary() ?? new Dictionary<string, string[]>()),
      TaskResultStatus.NotFound => Results.NotFound(),
      TaskResultStatus.Conflict => Results.Conflict(new
      {
        message = result.Message
      }),
      _ => Results.BadRequest()
    };
  }

  private static async Task<IResult> Delete(
    Guid workspaceId,
    Guid projectId,
    Guid taskId,
    ClaimsPrincipal user,
    ITaskService taskService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await taskService.DeleteAsync(
      workspaceId,
      projectId,
      taskId,
      userId,
      cancellationToken);

    return result.Status switch
    {
      TaskResultStatus.Success => Results.NoContent(),
      TaskResultStatus.Forbidden => Results.Forbid(),
      TaskResultStatus.NotFound => Results.NotFound(),
      TaskResultStatus.Conflict => Results.Conflict(new
      {
        message = result.Message
      }),
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
