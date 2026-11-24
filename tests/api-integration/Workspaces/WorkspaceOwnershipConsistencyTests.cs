using System.Net;
using Coordina.Api.Integration.Tests.Support;
using Coordina.Api.Modules.Workspaces.Application;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Coordina.Api.Integration.Tests.Workspaces;

/// <summary>A workspace keeps an owner, and a departure is all or nothing, whatever happens meanwhile.</summary>
public sealed class WorkspaceOwnershipConsistencyTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  // Each round is a fresh pair of owners; repeating gives the race a chance
  // to show without the lock.
  private const int Rounds = 8;

  [Fact]
  public async Task OwnersDemotingEachOtherTogether_LeaveOneOwner()
  {
    for (var round = 0; round < Rounds; round++)
    {
      var (first, second, workspaceId) = await CreateTwoOwnersAsync();

      var responses = await Task.WhenAll(
        ChangeRoleAsync(workspaceId, second, "MEMBER", first),
        ChangeRoleAsync(workspaceId, first, "MEMBER", second));

      Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
      Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Forbidden);
      Assert.Equal(1, await OwnerCountAsync(workspaceId));
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
      Assert.Equal(1, await OwnerCountAsync(workspaceId));
    }
  }

  [Fact]
  public async Task DepartureThatFailsHalfway_KeepsTheMemberAndTheirData()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var member = await scope.AddMemberAsync();
    var project = await Api.CreateProjectAsync(
      scope.Owner.AccessToken, scope.Workspace.Id, "Member's project", member.User.Id);
    var card = await scope.AddCardAsync(
      scope.FirstList.Id, new { title = "Assigned", assigneeIds = new[] { member.User.Id } });
    using var failingClient = Factory
      .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        services.AddScoped<IWorkspaceDepartureHandler, FailingDepartureHandler>()))
      .CreateClient();

    using var response = await new ApiTestClient(failingClient)
      .SendAsync(HttpMethod.Post, $"/workspaces/{scope.Workspace.Id}/leave", member.AccessToken);

    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    Assert.Equal(1, await Database.ScalarAsync<long>(
      $"SELECT count(*) FROM workspace_members WHERE workspace_id = '{scope.Workspace.Id}' AND user_id = '{member.User.Id}'"));
    Assert.Equal(
      member.User.Id,
      Assert.Single((await scope.GetBoardAsync()).Card(card.Id).Assignees).UserId);
    Assert.Equal(
      member.User.Id,
      (await Api.GetProjectAsync(scope.Owner.AccessToken, scope.Workspace.Id, project.Id)).ProjectOwnerId);
  }

  private async Task<(AuthResponse First, AuthResponse Second, string WorkspaceId)> CreateTwoOwnersAsync()
  {
    var first = await Api.RegisterAsync();
    var workspace = await Api.CreateWorkspaceAsync(first.AccessToken);
    var second = await Api.AddMemberAsync(first.AccessToken, workspace.Id);
    using var promotion = await ChangeRoleAsync(workspace.Id, second, "OWNER", first);
    promotion.EnsureSuccessStatusCode();

    return (first, second, workspace.Id);
  }

  private Task<HttpResponseMessage> ChangeRoleAsync(
    string workspaceId,
    AuthResponse member,
    string role,
    AuthResponse changedBy) =>
    Api.SendAsync(
      HttpMethod.Patch, $"/workspaces/{workspaceId}/members/{member.User.Id}", changedBy.AccessToken, new { role });

  private Task<long> OwnerCountAsync(string workspaceId) =>
    Database.ScalarAsync<long>(
      $"SELECT count(*) FROM workspace_members WHERE workspace_id = '{workspaceId}' AND role = 'OWNER'");

  private sealed class FailingDepartureHandler : IWorkspaceDepartureHandler
  {
    public Task HandleAsync(WorkspaceDeparture departure, CancellationToken cancellationToken) =>
      throw new InvalidOperationException("Simulated failure after the other handlers ran.");
  }
}
