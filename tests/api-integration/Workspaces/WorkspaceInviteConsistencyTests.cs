using System.Net;
using Coordina.Api.Integration.Tests.Support;
using Coordina.Api.Modules.Workspaces.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Coordina.Api.Integration.Tests.Workspaces;

public sealed class WorkspaceInviteConsistencyTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  [Fact]
  public async Task ConcurrentJoin_ConsumesInviteExactlyOnce()
  {
    var owner = await Api.RegisterAsync();
    var first = await Api.RegisterAsync();
    var second = await Api.RegisterAsync();
    var workspace = await Api.CreateWorkspaceAsync(owner.AccessToken);
    var invite = await Api.CreateInviteAsync(owner.AccessToken, workspace.Id);

    var responses = await Task.WhenAll(new[] { first, second }.Select(user => Api.SendAsync(
      HttpMethod.Post, "/workspaces/join", user.AccessToken, new { inviteCode = invite.Code })));

    Assert.Single(responses, response => response.IsSuccessStatusCode);
    Assert.Single(responses, response =>
      response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict);
    Assert.Equal(2, await Database.ScalarAsync<long>(
      $"SELECT count(*) FROM workspace_members WHERE workspace_id = '{workspace.Id}'"));
    Assert.Equal(1, await Database.ScalarAsync<long>(
      $"SELECT count(*) FROM workspace_invites WHERE workspace_id = '{workspace.Id}' AND consumed_at IS NOT NULL"));
  }

  [Fact]
  public async Task MembershipInsertFailure_DoesNotConsumeInvite()
  {
    var owner = await Api.RegisterAsync();
    var member = await Api.RegisterAsync();
    var workspace = await Api.CreateWorkspaceAsync(owner.AccessToken);
    await Api.CreateInviteAsync(owner.AccessToken, workspace.Id);
    var inviteId = await Database.ScalarAsync<Guid>(
      $"SELECT id FROM workspace_invites WHERE workspace_id = '{workspace.Id}'");

    await using var scope = Factory.Services.CreateAsyncScope();
    var store = scope.ServiceProvider.GetRequiredService<IWorkspaceStore>();
    await store.AddMemberAsync(
      Guid.Parse(workspace.Id),
      Guid.Parse(member.User.Id),
      DateTimeOffset.UtcNow,
      CancellationToken.None);

    var outcome = await store.ConsumeInviteAsync(
      inviteId,
      Guid.Parse(member.User.Id),
      DateTimeOffset.UtcNow,
      CancellationToken.None);

    Assert.Equal(ConsumeInviteStatus.AlreadyMember, outcome.Status);
    Assert.Equal(0, await Database.ScalarAsync<long>(
      $"SELECT count(*) FROM workspace_invites WHERE id = '{inviteId}' AND consumed_at IS NOT NULL"));
  }
}
