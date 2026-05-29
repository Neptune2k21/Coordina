using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Coordina.Api.Tests.Support;

namespace Coordina.Api.Tests;

public sealed class WorkspaceEndpointsTests(ApiTestApplicationFactory factory)
  : IClassFixture<ApiTestApplicationFactory>
{
  private readonly HttpClient _client = factory.CreateClient();

  [Fact]
  public async Task CreateWorkspace_AddsCreatorAsOwner()
  {
    var auth = await RegisterAsync();

    var response = await SendAsAsync(
      HttpMethod.Post,
      "/workspaces",
      auth.AccessToken,
      new { name = "Product Team" });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();
    Assert.NotNull(workspace);
    Assert.Equal("Product Team", workspace.Name);
    Assert.Equal("OWNER", workspace.Role);
  }

  [Fact]
  public async Task GetWorkspace_ForNonMember_ReturnsNotFound()
  {
    var owner = await RegisterAsync();
    var outsider = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Private Team");

    var response = await SendAsAsync(
      HttpMethod.Get,
      $"/workspaces/{workspace.Id}",
      outsider.AccessToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task DeleteWorkspace_ByMember_ReturnsForbidden()
  {
    var owner = await RegisterAsync();
    var member = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Launch Team");
    var invite = await CreateInviteAsync(owner.AccessToken, workspace.Id);
    await JoinWorkspaceAsync(member.AccessToken, invite.Code);

    var response = await SendAsAsync(
      HttpMethod.Delete,
      $"/workspaces/{workspace.Id}",
      member.AccessToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task DeleteWorkspace_ByOwner_RemovesWorkspace()
  {
    var owner = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Ops Team");

    var deleteResponse = await SendAsAsync(
      HttpMethod.Delete,
      $"/workspaces/{workspace.Id}",
      owner.AccessToken);

    Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

    var getResponse = await SendAsAsync(
      HttpMethod.Get,
      $"/workspaces/{workspace.Id}",
      owner.AccessToken);

    Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
  }

  [Fact]
  public async Task Workspaces_WithoutToken_ReturnsUnauthorized()
  {
    var response = await _client.GetAsync("/workspaces");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task ListWorkspaces_ReturnsOnlyUserMemberships()
  {
    var firstUser = await RegisterAsync();
    var secondUser = await RegisterAsync();
    var shared = await CreateWorkspaceAsync(firstUser.AccessToken, "Shared");
    var privateWorkspace = await CreateWorkspaceAsync(
      firstUser.AccessToken,
      "Private");
    await CreateWorkspaceAsync(secondUser.AccessToken, "Other");
    var invite = await CreateInviteAsync(firstUser.AccessToken, shared.Id);
    await JoinWorkspaceAsync(secondUser.AccessToken, invite.Code);

    var firstResponse = await SendAsAsync(
      HttpMethod.Get,
      "/workspaces",
      firstUser.AccessToken);
    var secondResponse = await SendAsAsync(
      HttpMethod.Get,
      "/workspaces",
      secondUser.AccessToken);

    Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
    Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

    var firstList = await firstResponse.Content
      .ReadFromJsonAsync<WorkspaceResponse[]>();
    var secondList = await secondResponse.Content
      .ReadFromJsonAsync<WorkspaceResponse[]>();

    Assert.NotNull(firstList);
    Assert.NotNull(secondList);
    Assert.Contains(firstList, workspace => workspace.Id == shared.Id);
    Assert.Contains(firstList, workspace => workspace.Id == privateWorkspace.Id);
    Assert.DoesNotContain(secondList, workspace => workspace.Id == privateWorkspace.Id);
    Assert.Contains(secondList, workspace => workspace.Id == shared.Id
      && workspace.Role == "MEMBER");
  }

  [Fact]
  public async Task JoinWorkspace_WithWorkspaceId_ReturnsValidationProblem()
  {
    var owner = await RegisterAsync();
    var member = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Locked");

    var response = await SendAsAsync(
      HttpMethod.Post,
      "/workspaces/join",
      member.AccessToken,
      new { inviteCode = workspace.Id });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task InvitationCode_IsOneTimeUse()
  {
    var owner = await RegisterAsync();
    var firstMember = await RegisterAsync();
    var secondMember = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Invite Only");
    var invite = await CreateInviteAsync(owner.AccessToken, workspace.Id);

    var firstJoin = await SendAsAsync(
      HttpMethod.Post,
      "/workspaces/join",
      firstMember.AccessToken,
      new { inviteCode = invite.Code });
    var secondJoin = await SendAsAsync(
      HttpMethod.Post,
      "/workspaces/join",
      secondMember.AccessToken,
      new { inviteCode = invite.Code });

    Assert.Equal(HttpStatusCode.OK, firstJoin.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, secondJoin.StatusCode);
  }

  [Fact]
  public async Task CreateInvite_ByMember_ReturnsForbidden()
  {
    var owner = await RegisterAsync();
    var member = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Owner Invites");
    var invite = await CreateInviteAsync(owner.AccessToken, workspace.Id);
    await JoinWorkspaceAsync(member.AccessToken, invite.Code);

    var response = await SendAsAsync(
      HttpMethod.Post,
      $"/workspaces/{workspace.Id}/invites",
      member.AccessToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Members_CanBeListedAndRemovedByOwner()
  {
    var owner = await RegisterAsync();
    var member = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Members");
    var invite = await CreateInviteAsync(owner.AccessToken, workspace.Id);
    await JoinWorkspaceAsync(member.AccessToken, invite.Code);

    var listResponse = await SendAsAsync(
      HttpMethod.Get,
      $"/workspaces/{workspace.Id}/members",
      owner.AccessToken);

    Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

    var members = await listResponse.Content
      .ReadFromJsonAsync<WorkspaceMemberResponse[]>();

    Assert.NotNull(members);
    Assert.Contains(members, item => item.UserId == owner.User.Id
      && item.Role == "OWNER");
    Assert.Contains(members, item => item.UserId == member.User.Id
      && item.Role == "MEMBER");

    var removeResponse = await SendAsAsync(
      HttpMethod.Delete,
      $"/workspaces/{workspace.Id}/members/{member.User.Id}",
      owner.AccessToken);

    Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);

    var memberGetResponse = await SendAsAsync(
      HttpMethod.Get,
      $"/workspaces/{workspace.Id}",
      member.AccessToken);

    Assert.Equal(HttpStatusCode.NotFound, memberGetResponse.StatusCode);
  }

  private async Task<AuthResponse> RegisterAsync()
  {
    var email = $"workspace-{Guid.NewGuid():N}@coordina.test";

    var response = await _client.PostAsJsonAsync("/auth/register", new
    {
      name = "Workspace Tester",
      email,
      password = "Password123!"
    });

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();

    Assert.NotNull(payload);
    return payload;
  }

  private async Task<WorkspaceResponse> CreateWorkspaceAsync(
    string accessToken,
    string name)
  {
    var response = await SendAsAsync(
      HttpMethod.Post,
      "/workspaces",
      accessToken,
      new { name });

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();

    Assert.NotNull(payload);
    return payload;
  }

  private async Task<WorkspaceResponse> JoinWorkspaceAsync(
    string accessToken,
    string inviteCode)
  {
    var response = await SendAsAsync(
      HttpMethod.Post,
      "/workspaces/join",
      accessToken,
      new { inviteCode });

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();

    Assert.NotNull(payload);
    return payload;
  }

  private async Task<CreateWorkspaceInviteResponse> CreateInviteAsync(
    string accessToken,
    string workspaceId)
  {
    var response = await SendAsAsync(
      HttpMethod.Post,
      $"/workspaces/{workspaceId}/invites",
      accessToken);

    response.EnsureSuccessStatusCode();

    var payload = await response.Content
      .ReadFromJsonAsync<CreateWorkspaceInviteResponse>();

    Assert.NotNull(payload);
    return payload;
  }

  private async Task<HttpResponseMessage> SendAsAsync(
    HttpMethod method,
    string path,
    string accessToken,
    object? body = null)
  {
    using var request = new HttpRequestMessage(method, path);
    request.Headers.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      accessToken);

    if (body is not null)
    {
      request.Content = JsonContent.Create(body);
    }

    return await _client.SendAsync(request);
  }

  private sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    CurrentUserResponse User);

  private sealed record CurrentUserResponse(
    string Id,
    string Email,
    string Name);

  private sealed record WorkspaceResponse(
    string Id,
    string Name,
    string Role,
    DateTimeOffset CreatedAt);

  private sealed record CreateWorkspaceInviteResponse(
    string Code,
    DateTimeOffset ExpiresAt);

  private sealed record WorkspaceMemberResponse(
    string UserId,
    string? Name,
    string? Email,
    string Role,
    DateTimeOffset JoinedAt);
}
