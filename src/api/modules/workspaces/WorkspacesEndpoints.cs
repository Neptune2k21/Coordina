using Coordina.Api.Core.Http;
using Coordina.Api.Core.Security;
using Coordina.Api.Modules.Workspaces.Application;
using Coordina.Api.Modules.Workspaces.Contracts;

namespace Coordina.Api.Modules.Workspaces;

public static class WorkspacesEndpoints
{
  public static IEndpointRouteBuilder MapWorkspacesEndpoints(
    this IEndpointRouteBuilder app)
  {
    var group = app.MapApiGroup("/workspaces", "Workspaces");

    group.MapPost("/", Create)
      .Produces<WorkspaceResponse>(StatusCodes.Status201Created)
      .WithSummary("Create workspace")
      .WithDescription("Creates a workspace and adds the authenticated user as OWNER.");

    group.MapGet("/", List)
      .Produces<IReadOnlyCollection<WorkspaceResponse>>(StatusCodes.Status200OK)
      .WithSummary("List workspaces")
      .WithDescription("Lists only workspaces where the authenticated user is a member.");

    group.MapGet("/{workspaceId:guid}", Get)
      .Produces<WorkspaceResponse>(StatusCodes.Status200OK)
      .WithSummary("Get workspace")
      .WithDescription("Returns a workspace only when the user belongs to it.");

    group.MapPost("/join", Join)
      .Produces<WorkspaceResponse>(StatusCodes.Status200OK)
      .WithSummary("Join workspace")
      .WithDescription("Adds the authenticated user as MEMBER using a one-time invitation code.");

    group.MapPost("/{workspaceId:guid}/invites", CreateInvite)
      .Produces<CreateWorkspaceInviteResponse>(StatusCodes.Status201Created)
      .WithSummary("Create invitation code")
      .WithDescription("Creates a one-time invitation code. Only OWNER members can invite.");

    group.MapGet("/{workspaceId:guid}/members", ListMembers)
      .Produces<IReadOnlyCollection<WorkspaceMemberResponse>>(StatusCodes.Status200OK)
      .WithSummary("List workspace members")
      .WithDescription("Lists members only when the authenticated user belongs to the workspace.");

    group.MapPatch("/{workspaceId:guid}/members/{memberUserId:guid}", ChangeMemberRole)
      .Produces<WorkspaceMemberResponse>(StatusCodes.Status200OK)
      .WithSummary("Change a member's role")
      .WithDescription("Makes a member an OWNER or a MEMBER. Only OWNER members can change roles, and the last OWNER cannot be demoted (409). To hand a workspace over, make another member OWNER, then step down or leave.");

    group.MapPost("/{workspaceId:guid}/leave", Leave)
      .Produces(StatusCodes.Status204NoContent)
      .WithSummary("Leave workspace")
      .WithDescription("Removes the authenticated user from the workspace. Their card assignments are dropped and their projects go to the longest-standing OWNER. The last OWNER cannot leave (409).");

    group.MapDelete("/{workspaceId:guid}/members/{memberUserId:guid}", RemoveMember)
      .Produces(StatusCodes.Status204NoContent)
      .WithSummary("Remove workspace member")
      .WithDescription("Removes a MEMBER from the workspace. Only OWNER members can remove members. Their card assignments are dropped and their projects go to the OWNER who removed them.");

    group.MapDelete("/{workspaceId:guid}", Delete)
      .Produces(StatusCodes.Status204NoContent)
      .WithSummary("Delete workspace")
      .WithDescription("Deletes a workspace. Only OWNER members can delete.");

    return app;
  }

  private static async Task<IResult> Create(
    CreateWorkspaceRequest request,
    CurrentUser user,
    IWorkspaceService workspaceService,
    CancellationToken cancellationToken)
  {
    var result = await workspaceService.CreateAsync(
      request,
      user.Id,
      cancellationToken);

    return result.ToCreatedResult(workspace => $"/workspaces/{workspace.Id}");
  }

  private static async Task<IResult> List(
    CurrentUser user,
    IWorkspaceService workspaceService,
    CancellationToken cancellationToken)
  {
    var workspaces = await workspaceService.ListAsync(user.Id, cancellationToken);

    return TypedResults.Ok(workspaces);
  }

  private static async Task<IResult> Get(
    Guid workspaceId,
    CurrentUser user,
    IWorkspaceService workspaceService,
    CancellationToken cancellationToken)
  {
    var result = await workspaceService.GetAsync(
      workspaceId,
      user.Id,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> Join(
    JoinWorkspaceRequest request,
    CurrentUser user,
    IWorkspaceService workspaceService,
    CancellationToken cancellationToken)
  {
    var result = await workspaceService.JoinAsync(
      request,
      user.Id,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> CreateInvite(
    Guid workspaceId,
    CurrentUser user,
    IWorkspaceService workspaceService,
    CancellationToken cancellationToken)
  {
    var result = await workspaceService.CreateInviteAsync(
      workspaceId,
      user.Id,
      cancellationToken);

    return result.ToCreatedResult(_ => $"/workspaces/{workspaceId}/invites");
  }

  private static async Task<IResult> ListMembers(
    Guid workspaceId,
    CurrentUser user,
    IWorkspaceService workspaceService,
    CancellationToken cancellationToken)
  {
    var result = await workspaceService.ListMembersAsync(
      workspaceId,
      user.Id,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> ChangeMemberRole(
    Guid workspaceId,
    Guid memberUserId,
    ChangeMemberRoleRequest request,
    CurrentUser user,
    IWorkspaceService workspaceService,
    CancellationToken cancellationToken)
  {
    var result = await workspaceService.ChangeMemberRoleAsync(
      workspaceId,
      memberUserId,
      request,
      user.Id,
      cancellationToken);

    return result.ToOkResult();
  }

  private static async Task<IResult> Leave(
    Guid workspaceId,
    CurrentUser user,
    IWorkspaceService workspaceService,
    CancellationToken cancellationToken)
  {
    var result = await workspaceService.LeaveAsync(
      workspaceId,
      user.Id,
      cancellationToken);

    return result.ToNoContentResult();
  }

  private static async Task<IResult> RemoveMember(
    Guid workspaceId,
    Guid memberUserId,
    CurrentUser user,
    IWorkspaceService workspaceService,
    CancellationToken cancellationToken)
  {
    var result = await workspaceService.RemoveMemberAsync(
      workspaceId,
      memberUserId,
      user.Id,
      cancellationToken);

    return result.ToNoContentResult();
  }

  private static async Task<IResult> Delete(
    Guid workspaceId,
    CurrentUser user,
    IWorkspaceService workspaceService,
    CancellationToken cancellationToken)
  {
    var result = await workspaceService.DeleteAsync(
      workspaceId,
      user.Id,
      cancellationToken);

    return result.ToNoContentResult();
  }
}
