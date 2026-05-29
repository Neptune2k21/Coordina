using System.Security.Claims;
using Coordina.Api.Modules.Workspaces.Application;
using Coordina.Api.Modules.Workspaces.Contracts;

namespace Coordina.Api.Modules.Workspaces;

public static class WorkspacesEndpoints
{
  public static IEndpointRouteBuilder MapWorkspacesEndpoints(
    this IEndpointRouteBuilder app)
  {
    var group = app
      .MapGroup("/workspaces")
      .RequireAuthorization()
      .WithTags("Workspaces");

    group.MapPost("/", Create)
      .WithSummary("Create workspace")
      .WithDescription("Creates a workspace and adds the authenticated user as OWNER.");

    group.MapGet("/", List)
      .WithSummary("List workspaces")
      .WithDescription("Lists only workspaces where the authenticated user is a member.");

    group.MapGet("/{workspaceId:guid}", Get)
      .WithSummary("Get workspace")
      .WithDescription("Returns a workspace only when the user belongs to it.");

    group.MapPost("/join", Join)
      .WithSummary("Join workspace")
      .WithDescription("Adds the authenticated user as MEMBER using a one-time invitation code.");

    group.MapPost("/{workspaceId:guid}/invites", CreateInvite)
      .WithSummary("Create invitation code")
      .WithDescription("Creates a one-time invitation code. Only OWNER members can invite.");

    group.MapGet("/{workspaceId:guid}/members", ListMembers)
      .WithSummary("List workspace members")
      .WithDescription("Lists members only when the authenticated user belongs to the workspace.");

    group.MapDelete("/{workspaceId:guid}/members/{memberUserId:guid}", RemoveMember)
      .WithSummary("Remove workspace member")
      .WithDescription("Removes a MEMBER from the workspace. Only OWNER members can remove members.");

    group.MapDelete("/{workspaceId:guid}", Delete)
      .WithSummary("Delete workspace")
      .WithDescription("Deletes a workspace. Only OWNER members can delete.");

    return app;
  }

  private static async Task<IResult> Create(
    CreateWorkspaceRequest request,
    ClaimsPrincipal user,
    IWorkspaceService workspaceService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await workspaceService.CreateAsync(
      request,
      userId,
      cancellationToken);

    return result.Status switch
    {
      WorkspaceResultStatus.Success => Results.Created(
        $"/workspaces/{result.Value!.Id}",
        result.Value),
      WorkspaceResultStatus.ValidationError => Results.ValidationProblem(
        result.Errors?.ToDictionary() ?? new Dictionary<string, string[]>()),
      _ => Results.BadRequest()
    };
  }

  private static async Task<IResult> List(
    ClaimsPrincipal user,
    IWorkspaceService workspaceService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var workspaces = await workspaceService.ListAsync(userId, cancellationToken);

    return Results.Ok(workspaces);
  }

  private static async Task<IResult> Get(
    Guid workspaceId,
    ClaimsPrincipal user,
    IWorkspaceService workspaceService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await workspaceService.GetAsync(
      workspaceId,
      userId,
      cancellationToken);

    return result.Status switch
    {
      WorkspaceResultStatus.Success => Results.Ok(result.Value),
      WorkspaceResultStatus.NotFound => Results.NotFound(),
      _ => Results.BadRequest()
    };
  }

  private static async Task<IResult> Join(
    JoinWorkspaceRequest request,
    ClaimsPrincipal user,
    IWorkspaceService workspaceService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await workspaceService.JoinAsync(
      request,
      userId,
      cancellationToken);

    return result.Status switch
    {
      WorkspaceResultStatus.Success => Results.Ok(result.Value),
      WorkspaceResultStatus.ValidationError => Results.ValidationProblem(
        result.Errors?.ToDictionary() ?? new Dictionary<string, string[]>()),
      WorkspaceResultStatus.NotFound => Results.NotFound(new
      {
        message = result.Message ?? "Invitation code is invalid."
      }),
      WorkspaceResultStatus.Conflict => Results.Conflict(new
      {
        message = result.Message
      }),
      _ => Results.BadRequest()
    };
  }

  private static async Task<IResult> CreateInvite(
    Guid workspaceId,
    ClaimsPrincipal user,
    IWorkspaceService workspaceService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await workspaceService.CreateInviteAsync(
      workspaceId,
      userId,
      cancellationToken);

    return result.Status switch
    {
      WorkspaceResultStatus.Success => Results.Created(
        $"/workspaces/{workspaceId}/invites",
        result.Value),
      WorkspaceResultStatus.Forbidden => Results.Forbid(),
      WorkspaceResultStatus.NotFound => Results.NotFound(),
      _ => Results.BadRequest()
    };
  }

  private static async Task<IResult> ListMembers(
    Guid workspaceId,
    ClaimsPrincipal user,
    IWorkspaceService workspaceService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await workspaceService.ListMembersAsync(
      workspaceId,
      userId,
      cancellationToken);

    return result.Status switch
    {
      WorkspaceResultStatus.Success => Results.Ok(result.Value),
      WorkspaceResultStatus.NotFound => Results.NotFound(),
      _ => Results.BadRequest()
    };
  }

  private static async Task<IResult> RemoveMember(
    Guid workspaceId,
    Guid memberUserId,
    ClaimsPrincipal user,
    IWorkspaceService workspaceService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await workspaceService.RemoveMemberAsync(
      workspaceId,
      memberUserId,
      userId,
      cancellationToken);

    return result.Status switch
    {
      WorkspaceResultStatus.Success => Results.NoContent(),
      WorkspaceResultStatus.ValidationError => Results.ValidationProblem(
        result.Errors?.ToDictionary() ?? new Dictionary<string, string[]>()),
      WorkspaceResultStatus.Forbidden => Results.Forbid(),
      WorkspaceResultStatus.NotFound => Results.NotFound(),
      _ => Results.BadRequest()
    };
  }

  private static async Task<IResult> Delete(
    Guid workspaceId,
    ClaimsPrincipal user,
    IWorkspaceService workspaceService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(user, out var userId))
    {
      return Results.Unauthorized();
    }

    var result = await workspaceService.DeleteAsync(
      workspaceId,
      userId,
      cancellationToken);

    return result.Status switch
    {
      WorkspaceResultStatus.Success => Results.NoContent(),
      WorkspaceResultStatus.Forbidden => Results.Forbid(),
      WorkspaceResultStatus.NotFound => Results.NotFound(),
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
