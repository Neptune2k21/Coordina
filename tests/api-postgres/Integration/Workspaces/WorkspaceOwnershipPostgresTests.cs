using System.Net;
using Coordina.Api.Modules.Workspaces.Application;
using Coordina.Api.Postgres.Tests.Support;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Coordina.Api.Postgres.Tests.Integration.Workspaces;

public sealed class WorkspaceOwnershipPostgresTests(PostgresContainerFixture postgres)
  : PostgresApiTest(postgres)
{
  // Each round is a fresh pair of owners; repeating gives the race a chance
  // to show without the lock.
  private const int Rounds = 8;

  private static readonly string[] ListTitles = ["Todo"];

  [Fact]
  public async Task OwnersDemotingEachOtherTogether_LeaveOneOwner()
  {
    for (var round = 0; round < Rounds; round++)
    {
      var (first, second, workspaceId) = await CreateTwoOwnersAsync();

      var responses = await Task.WhenAll(
        Api.SendAsync(HttpMethod.Patch, $"/workspaces/{workspaceId}/members/{second.Id}", first.AccessToken, new { role = "MEMBER" }),
        Api.SendAsync(HttpMethod.Patch, $"/workspaces/{workspaceId}/members/{first.Id}", second.AccessToken, new { role = "MEMBER" }));

      Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
      Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Forbidden);
      Assert.Equal(1L, await OwnerCountAsync(workspaceId));
    }
  }

  [Fact]
  public async Task OwnersLeavingTogether_LeaveOneOwner()
  {
    for (var round = 0; round < Rounds; round++)
    {
      var (first, second, workspaceId) = await CreateTwoOwnersAsync();

      var responses = await Task.WhenAll(
        Api.SendAsync(HttpMethod.Post, $"/workspaces/{workspaceId}/leave", first.AccessToken),
        Api.SendAsync(HttpMethod.Post, $"/workspaces/{workspaceId}/leave", second.AccessToken));

      Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
      Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
      Assert.Equal(1L, await OwnerCountAsync(workspaceId));
    }
  }

  [Fact]
  public async Task Leave_DropsAssignmentsAndHandsOverProjects_InTheDatabase()
  {
    var (owner, member, workspaceId, projectId, cardId) = await CreateAssignedMemberAsync();

    using var response = await Api.SendAsync(HttpMethod.Post, $"/workspaces/{workspaceId}/leave", member.AccessToken);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Equal(0L, await Database.ScalarAsync<long>(
      $"SELECT COUNT(*) FROM board_card_assignees WHERE card_id = '{cardId}'"));
    Assert.Equal(owner.Id, await Database.ScalarAsync<Guid>(
      $"SELECT project_owner_id FROM projects WHERE id = '{projectId}'"));
  }

  [Fact]
  public async Task DepartureThatFailsHalfway_KeepsTheMemberAndTheirData()
  {
    var (_, member, workspaceId, projectId, cardId) = await CreateAssignedMemberAsync();
    using var failingClient = Factory
      .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        services.AddScoped<IWorkspaceDepartureHandler, FailingDepartureHandler>()))
      .CreateClient();

    using var response = await new ApiClient(failingClient)
      .SendAsync(HttpMethod.Post, $"/workspaces/{workspaceId}/leave", member.AccessToken);

    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    Assert.Equal(1L, await Database.ScalarAsync<long>(
      $"SELECT COUNT(*) FROM workspace_members WHERE workspace_id = '{workspaceId}' AND user_id = '{member.Id}'"));
    Assert.Equal(1L, await Database.ScalarAsync<long>(
      $"SELECT COUNT(*) FROM board_card_assignees WHERE card_id = '{cardId}'"));
    Assert.Equal(member.Id, await Database.ScalarAsync<Guid>(
      $"SELECT project_owner_id FROM projects WHERE id = '{projectId}'"));
  }

  private async Task<(TestUser First, TestUser Second, Guid WorkspaceId)> CreateTwoOwnersAsync()
  {
    var first = await Api.RegisterAsync();
    var second = await Api.RegisterAsync();
    var workspaceId = await CreateWorkspaceAsync(first);
    await JoinAsync(first, second, workspaceId);
    await Api.SendForAsync<MemberResponse>(
      HttpMethod.Patch, $"/workspaces/{workspaceId}/members/{second.Id}", first.AccessToken, new { role = "OWNER" });

    return (first, second, workspaceId);
  }

  /// <summary>A member who owns a project and is assigned to a card on its board.</summary>
  private async Task<(TestUser Owner, TestUser Member, Guid WorkspaceId, Guid ProjectId, Guid CardId)>
    CreateAssignedMemberAsync()
  {
    var owner = await Api.RegisterAsync();
    var member = await Api.RegisterAsync();
    var workspaceId = await CreateWorkspaceAsync(owner);
    await JoinAsync(owner, member, workspaceId);
    var project = await Api.SendForAsync<IdResponse>(
      HttpMethod.Post, $"/workspaces/{workspaceId}/projects", owner.AccessToken,
      new { name = "Handed over", projectOwnerId = member.Id });
    var boardPath = $"/workspaces/{workspaceId}/projects/{project.Id}/boards";
    var board = await Api.SendForAsync<BoardResponse>(
      HttpMethod.Post, boardPath, owner.AccessToken,
      new { name = "Board", template = "CUSTOM", customListTitles = ListTitles });
    var listId = board.Lists[0].Id;
    var withCard = await Api.SendForAsync<BoardResponse>(
      HttpMethod.Post, $"{boardPath}/{board.Id}/lists/{listId}/cards", owner.AccessToken,
      new { title = "Assigned", assigneeIds = new[] { member.Id } });

    return (owner, member, workspaceId, project.Id, withCard.Lists[0].Cards[0].Id);
  }

  private async Task<Guid> CreateWorkspaceAsync(TestUser owner) =>
    (await Api.SendForAsync<IdResponse>(HttpMethod.Post, "/workspaces", owner.AccessToken, new { name = "Ownership Team" })).Id;

  private async Task JoinAsync(TestUser owner, TestUser member, Guid workspaceId)
  {
    var invite = await Api.SendForAsync<InviteResponse>(
      HttpMethod.Post, $"/workspaces/{workspaceId}/invites", owner.AccessToken);
    await Api.SendForAsync<IdResponse>(
      HttpMethod.Post, "/workspaces/join", member.AccessToken, new { inviteCode = invite.Code });
  }

  private Task<long> OwnerCountAsync(Guid workspaceId) =>
    Database.ScalarAsync<long>(
      $"SELECT COUNT(*) FROM workspace_members WHERE workspace_id = '{workspaceId}' AND role = 'OWNER'");

  private sealed class FailingDepartureHandler : IWorkspaceDepartureHandler
  {
    public Task HandleAsync(WorkspaceDeparture departure, CancellationToken cancellationToken) =>
      throw new InvalidOperationException("Simulated failure after the other handlers ran.");
  }

  private sealed record IdResponse(Guid Id);

  private sealed record InviteResponse(string Code);

  private sealed record MemberResponse(Guid UserId, string Role);

  private sealed record BoardResponse(Guid Id, ListResponse[] Lists);

  private sealed record ListResponse(Guid Id, CardResponse[] Cards);

  private sealed record CardResponse(Guid Id);
}
