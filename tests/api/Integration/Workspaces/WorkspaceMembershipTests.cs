using System.Net;
using System.Net.Http.Json;
using Coordina.Api.Core.Http;
using Coordina.Api.Tests.Support;
using Microsoft.AspNetCore.Mvc;

namespace Coordina.Api.Tests.Integration.Workspaces;

public sealed class WorkspaceMembershipTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  private const string LastOwnerMessage =
    "A workspace needs at least one owner. Make another member an owner first.";

  [Fact]
  public async Task ChangeRole_ByOwner_MakesTheMemberAnOwner()
  {
    var (owner, workspace) = await CreateWorkspaceAsync();
    var member = await Api.AddMemberAsync(owner.AccessToken, workspace.Id);

    var changed = await Api.SendForAsync<WorkspaceMemberResponse>(
      HttpMethod.Patch, MemberPath(workspace, member), owner.AccessToken, new { role = "OWNER" });

    Assert.Equal("OWNER", changed.Role);
    Assert.Equal(member.User.Id, changed.UserId);
    Assert.Equal("OWNER", (await GetWorkspaceAsync(workspace, member)).Role);
  }

  [Fact]
  public async Task ChangeRole_ToTheSameRole_ChangesNothing()
  {
    var (owner, workspace) = await CreateWorkspaceAsync();
    var member = await Api.AddMemberAsync(owner.AccessToken, workspace.Id);

    var unchanged = await Api.SendForAsync<WorkspaceMemberResponse>(
      HttpMethod.Patch, MemberPath(workspace, member), owner.AccessToken, new { role = "MEMBER" });

    Assert.Equal("MEMBER", unchanged.Role);
  }

  [Fact]
  public async Task ChangeRole_ByMember_IsForbidden_AndByOutsider_NotFound()
  {
    var (owner, workspace) = await CreateWorkspaceAsync();
    var member = await Api.AddMemberAsync(owner.AccessToken, workspace.Id);
    var outsider = await Api.RegisterAsync("Outsider");

    using var byMember = await Api.SendAsync(
      HttpMethod.Patch, MemberPath(workspace, owner), member.AccessToken, new { role = "MEMBER" });
    using var byOutsider = await Api.SendAsync(
      HttpMethod.Patch, MemberPath(workspace, member), outsider.AccessToken, new { role = "OWNER" });

    Assert.Equal(HttpStatusCode.Forbidden, byMember.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, byOutsider.StatusCode);
  }

  [Fact]
  public async Task ChangeRole_OfSomeoneOutsideTheWorkspace_ReturnsNotFound()
  {
    var (owner, workspace) = await CreateWorkspaceAsync();
    var outsider = await Api.RegisterAsync("Outsider");

    using var response = await Api.SendAsync(
      HttpMethod.Patch, MemberPath(workspace, outsider), owner.AccessToken, new { role = "OWNER" });

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task ChangeRole_WithAnUnknownRole_ReturnsValidationProblem()
  {
    var (owner, workspace) = await CreateWorkspaceAsync();
    var member = await Api.AddMemberAsync(owner.AccessToken, workspace.Id);

    using var response = await Api.SendAsync(
      HttpMethod.Patch, MemberPath(workspace, member), owner.AccessToken, new { role = "ADMIN" });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
    Assert.Contains("role", problem!.Errors.Keys);
  }

  [Fact]
  public async Task ChangeRole_OfTheLastOwner_IsRejected()
  {
    var (owner, workspace) = await CreateWorkspaceAsync();
    await Api.AddMemberAsync(owner.AccessToken, workspace.Id);

    using var response = await Api.SendAsync(
      HttpMethod.Patch, MemberPath(workspace, owner), owner.AccessToken, new { role = "MEMBER" });

    await AssertConflictAsync(response, LastOwnerMessage);
    Assert.Equal("OWNER", (await GetWorkspaceAsync(workspace, owner)).Role);
  }

  [Fact]
  public async Task Ownership_IsTransferredByPromotingThenSteppingDown()
  {
    var (owner, workspace) = await CreateWorkspaceAsync();
    var successor = await Api.AddMemberAsync(owner.AccessToken, workspace.Id, "Successor");

    await Api.SendForAsync<WorkspaceMemberResponse>(
      HttpMethod.Patch, MemberPath(workspace, successor), owner.AccessToken, new { role = "OWNER" });
    var steppedDown = await Api.SendForAsync<WorkspaceMemberResponse>(
      HttpMethod.Patch, MemberPath(workspace, owner), owner.AccessToken, new { role = "MEMBER" });

    Assert.Equal("MEMBER", steppedDown.Role);
    Assert.Equal("OWNER", (await GetWorkspaceAsync(workspace, successor)).Role);

    using var invite = await Api.SendAsync(
      HttpMethod.Post, $"/workspaces/{workspace.Id}/invites", owner.AccessToken);
    Assert.Equal(HttpStatusCode.Forbidden, invite.StatusCode);
  }

  [Fact]
  public async Task Leave_AsMember_RemovesTheMembership()
  {
    var (owner, workspace) = await CreateWorkspaceAsync();
    var member = await Api.AddMemberAsync(owner.AccessToken, workspace.Id);

    using var leave = await LeaveAsync(workspace, member);
    using var after = await Api.SendAsync(HttpMethod.Get, $"/workspaces/{workspace.Id}", member.AccessToken);

    Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
  }

  [Fact]
  public async Task Leave_AsTheLastOwner_IsRejected()
  {
    var (owner, workspace) = await CreateWorkspaceAsync();
    await Api.AddMemberAsync(owner.AccessToken, workspace.Id);

    using var response = await LeaveAsync(workspace, owner);

    await AssertConflictAsync(response, LastOwnerMessage);
  }

  [Fact]
  public async Task Leave_AsTheOnlyMember_SuggestsDeletingTheWorkspace()
  {
    var (owner, workspace) = await CreateWorkspaceAsync();

    using var response = await LeaveAsync(workspace, owner);

    await AssertConflictAsync(response, "You are the only member. Delete the workspace instead of leaving it.");
  }

  [Fact]
  public async Task Leave_AsOneOfTwoOwners_IsAllowed()
  {
    var (owner, workspace) = await CreateWorkspaceAsync();
    var coOwner = await Api.AddMemberAsync(owner.AccessToken, workspace.Id);
    await Api.SendForAsync<WorkspaceMemberResponse>(
      HttpMethod.Patch, MemberPath(workspace, coOwner), owner.AccessToken, new { role = "OWNER" });

    using var response = await LeaveAsync(workspace, owner);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Equal("OWNER", (await GetWorkspaceAsync(workspace, coOwner)).Role);
  }

  [Fact]
  public async Task Leave_OutsideTheWorkspace_ReturnsNotFound()
  {
    var (_, workspace) = await CreateWorkspaceAsync();
    var outsider = await Api.RegisterAsync("Outsider");

    using var response = await LeaveAsync(workspace, outsider);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task Leave_UnassignsTheLeaver_AndGivesTheirProjectsToTheLongestStandingOwner()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var member = await scope.AddMemberAsync();
    var laterOwner = await scope.AddMemberAsync();
    await PromoteAsync(scope, laterOwner);
    var card = await scope.AddCardAsync(scope.FirstList.Id, new
    {
      title = "Shared work",
      assigneeIds = new[] { member.User.Id, scope.Owner.User.Id }
    });
    var project = await Api.CreateProjectAsync(
      scope.Owner.AccessToken, scope.Workspace.Id, "Member's project", member.User.Id);

    using var leave = await LeaveAsync(scope.Workspace, member);

    Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);
    var assignees = (await scope.GetBoardAsync()).Card(card.Id).Assignees;
    Assert.Equal(scope.Owner.User.Id, Assert.Single(assignees).UserId);
    var handedOver = await Api.GetProjectAsync(scope.Owner.AccessToken, scope.Workspace.Id, project.Id.ToString());
    Assert.Equal(scope.Owner.User.Id, handedOver.ProjectOwnerId.ToString());
  }

  [Fact]
  public async Task Remove_GivesTheMembersProjectsToTheOwnerWhoRemovedThem()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var coOwner = await scope.AddMemberAsync();
    await PromoteAsync(scope, coOwner);
    var member = await scope.AddMemberAsync();
    var card = await scope.AddCardAsync(scope.FirstList.Id, new
    {
      title = "Assigned work",
      assigneeIds = new[] { member.User.Id }
    });
    var project = await Api.CreateProjectAsync(
      scope.Owner.AccessToken, scope.Workspace.Id, "Member's project", member.User.Id);

    using var remove = await Api.SendAsync(
      HttpMethod.Delete, MemberPath(scope.Workspace, member), coOwner.AccessToken);

    Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
    Assert.Empty((await scope.GetBoardAsync()).Card(card.Id).Assignees);
    var handedOver = await Api.GetProjectAsync(scope.Owner.AccessToken, scope.Workspace.Id, project.Id.ToString());
    Assert.Equal(coOwner.User.Id, handedOver.ProjectOwnerId.ToString());
  }

  [Fact]
  public async Task Remove_AnOwner_IsForbidden()
  {
    var (owner, workspace) = await CreateWorkspaceAsync();
    var coOwner = await Api.AddMemberAsync(owner.AccessToken, workspace.Id);
    await Api.SendForAsync<WorkspaceMemberResponse>(
      HttpMethod.Patch, MemberPath(workspace, coOwner), owner.AccessToken, new { role = "OWNER" });

    using var response = await Api.SendAsync(HttpMethod.Delete, MemberPath(workspace, coOwner), owner.AccessToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  private async Task<(AuthResponse Owner, WorkspaceResponse Workspace)> CreateWorkspaceAsync()
  {
    var owner = await Api.RegisterAsync("Owner");
    var workspace = await Api.CreateWorkspaceAsync(owner.AccessToken);

    return (owner, workspace);
  }

  private Task<WorkspaceMemberResponse> PromoteAsync(BoardScope scope, AuthResponse member) =>
    Api.SendForAsync<WorkspaceMemberResponse>(
      HttpMethod.Patch, MemberPath(scope.Workspace, member), scope.Owner.AccessToken, new { role = "OWNER" });

  private Task<WorkspaceResponse> GetWorkspaceAsync(WorkspaceResponse workspace, AuthResponse user) =>
    Api.SendForAsync<WorkspaceResponse>(HttpMethod.Get, $"/workspaces/{workspace.Id}", user.AccessToken);

  private Task<HttpResponseMessage> LeaveAsync(WorkspaceResponse workspace, AuthResponse user) =>
    Api.SendAsync(HttpMethod.Post, $"/workspaces/{workspace.Id}/leave", user.AccessToken);

  private static string MemberPath(WorkspaceResponse workspace, AuthResponse member) =>
    $"/workspaces/{workspace.Id}/members/{member.User.Id}";

  private static async Task AssertConflictAsync(HttpResponseMessage response, string message)
  {
    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal(message, (await response.Content.ReadFromJsonAsync<ErrorResponse>())!.Message);
  }
}
