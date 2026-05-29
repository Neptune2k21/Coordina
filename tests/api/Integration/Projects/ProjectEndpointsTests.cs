using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Coordina.Api.Tests.Support;

namespace Coordina.Api.Tests;

public sealed class ProjectEndpointsTests(ApiTestApplicationFactory factory)
  : IClassFixture<ApiTestApplicationFactory>
{
  private readonly HttpClient _client = factory.CreateClient();

  [Fact]
  public async Task CreateProject_WithinWorkspace_ReturnsActiveProject()
  {
    var owner = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Product Team");

    var response = await SendAsAsync(
      HttpMethod.Post,
      $"/workspaces/{workspace.Id}/projects",
      owner.AccessToken,
      new
      {
        name = "Launch plan",
        description = "Coordinate launch work.",
        key = "app",
        icon = "🚀",
        color = "teal"
      });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var project = await response.Content.ReadFromJsonAsync<ProjectResponse>();

    Assert.NotNull(project);
    Assert.Equal("Launch plan", project.Name);
    Assert.Equal("Coordinate launch work.", project.Description);
    Assert.Equal("APP", project.Key);
    Assert.Equal("ACTIVE", project.Status);
    Assert.Equal(workspace.Id, project.WorkspaceId);
    Assert.Equal(owner.User.Id, project.ProjectOwnerId);
    Assert.Equal(project.CreatedAt, project.UpdatedAt);
    Assert.Null(project.ArchivedAt);
  }

  [Fact]
  public async Task ListProjects_FiltersByStatusInBackend()
  {
    var owner = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Product Team");
    var active = await CreateProjectAsync(owner.AccessToken, workspace.Id, "Active");
    var archived = await CreateProjectAsync(owner.AccessToken, workspace.Id, "Archived");
    var completed = await CreateProjectAsync(owner.AccessToken, workspace.Id, "Completed");
    await UpdateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      archived.Id,
      new { status = "ARCHIVED" });
    await UpdateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      completed.Id,
      new { status = "COMPLETED" });

    var defaultProjects = await ListProjectsAsync(owner.AccessToken, workspace.Id);
    var withArchived = await ListProjectsAsync(
      owner.AccessToken,
      workspace.Id,
      "?includeArchived=true");
    var withCompleted = await ListProjectsAsync(
      owner.AccessToken,
      workspace.Id,
      "?includeCompleted=true");
    var withAll = await ListProjectsAsync(
      owner.AccessToken,
      workspace.Id,
      "?includeArchived=true&includeCompleted=true");

    Assert.Contains(defaultProjects, project => project.Id == active.Id);
    Assert.DoesNotContain(defaultProjects, project => project.Id == archived.Id);
    Assert.DoesNotContain(defaultProjects, project => project.Id == completed.Id);

    Assert.Contains(withArchived, project => project.Id == archived.Id);
    Assert.DoesNotContain(withArchived, project => project.Id == completed.Id);

    Assert.Contains(withCompleted, project => project.Id == completed.Id);
    Assert.DoesNotContain(withCompleted, project => project.Id == archived.Id);

    Assert.Contains(withAll, project => project.Id == active.Id);
    Assert.Contains(withAll, project => project.Id == archived.Id);
    Assert.Contains(withAll, project => project.Id == completed.Id);
  }

  [Fact]
  public async Task DeleteProject_ArchivesByDefault_AndOwnerCanPermanentlyDelete()
  {
    var owner = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Ops Team");
    var project = await CreateProjectAsync(owner.AccessToken, workspace.Id, "Cleanup");

    var archiveResponse = await SendAsAsync(
      HttpMethod.Delete,
      $"/workspaces/{workspace.Id}/projects/{project.Id}",
      owner.AccessToken);

    Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

    var archived = await GetProjectAsync(owner.AccessToken, workspace.Id, project.Id);

    Assert.Equal("ARCHIVED", archived.Status);
    Assert.NotNull(archived.ArchivedAt);

    var defaultProjects = await ListProjectsAsync(owner.AccessToken, workspace.Id);

    Assert.DoesNotContain(defaultProjects, item => item.Id == project.Id);

    var permanentDeleteResponse = await SendAsAsync(
      HttpMethod.Delete,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/permanent",
      owner.AccessToken);

    Assert.Equal(HttpStatusCode.NoContent, permanentDeleteResponse.StatusCode);

    var getResponse = await SendAsAsync(
      HttpMethod.Get,
      $"/workspaces/{workspace.Id}/projects/{project.Id}",
      owner.AccessToken);

    Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
  }

  [Fact]
  public async Task ProjectOwner_CanEditAndArchiveProject()
  {
    var owner = await RegisterAsync();
    var member = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Launch Team");
    var invite = await CreateInviteAsync(owner.AccessToken, workspace.Id);
    await JoinWorkspaceAsync(member.AccessToken, invite.Code);
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Member owned",
      member.User.Id);

    var updateResponse = await SendAsAsync(
      HttpMethod.Patch,
      $"/workspaces/{workspace.Id}/projects/{project.Id}",
      member.AccessToken,
      new { name = "Member updated", color = "pink" });

    Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

    var updated = await updateResponse.Content.ReadFromJsonAsync<ProjectResponse>();

    Assert.NotNull(updated);
    Assert.Equal("Member updated", updated.Name);
    Assert.Equal("pink", updated.Color);

    var archiveResponse = await SendAsAsync(
      HttpMethod.Delete,
      $"/workspaces/{workspace.Id}/projects/{project.Id}",
      member.AccessToken);

    Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);
  }

  [Fact]
  public async Task Member_WhoIsNotProjectOwner_CannotModifyProject()
  {
    var owner = await RegisterAsync();
    var member = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Private Team");
    var invite = await CreateInviteAsync(owner.AccessToken, workspace.Id);
    await JoinWorkspaceAsync(member.AccessToken, invite.Code);
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Owner project");

    var updateResponse = await SendAsAsync(
      HttpMethod.Patch,
      $"/workspaces/{workspace.Id}/projects/{project.Id}",
      member.AccessToken,
      new { name = "Nope" });
    var archiveResponse = await SendAsAsync(
      HttpMethod.Delete,
      $"/workspaces/{workspace.Id}/projects/{project.Id}",
      member.AccessToken);
    var permanentDeleteResponse = await SendAsAsync(
      HttpMethod.Delete,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/permanent",
      member.AccessToken);

    Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, archiveResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, permanentDeleteResponse.StatusCode);
  }

  [Fact]
  public async Task CompletedProjects_AreReadOnly()
  {
    var owner = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Readonly Team");
    var project = await CreateProjectAsync(owner.AccessToken, workspace.Id, "Done");
    var completed = await UpdateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      new { status = "COMPLETED" });

    Assert.Equal("COMPLETED", completed.Status);

    var updateResponse = await SendAsAsync(
      HttpMethod.Patch,
      $"/workspaces/{workspace.Id}/projects/{project.Id}",
      owner.AccessToken,
      new { name = "Should not change" });
    var archiveResponse = await SendAsAsync(
      HttpMethod.Delete,
      $"/workspaces/{workspace.Id}/projects/{project.Id}",
      owner.AccessToken);

    Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, archiveResponse.StatusCode);
  }

  [Fact]
  public async Task ProjectOwner_MustBeWorkspaceMember()
  {
    var owner = await RegisterAsync();
    var outsider = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Owner Guard");

    var response = await SendAsAsync(
      HttpMethod.Post,
      $"/workspaces/{workspace.Id}/projects",
      owner.AccessToken,
      new
      {
        name = "Invalid owner",
        projectOwnerId = outsider.User.Id
      });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Projects_ForNonMember_ReturnNotFound()
  {
    var owner = await RegisterAsync();
    var outsider = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Private");
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Private project");

    var listResponse = await SendAsAsync(
      HttpMethod.Get,
      $"/workspaces/{workspace.Id}/projects",
      outsider.AccessToken);
    var getResponse = await SendAsAsync(
      HttpMethod.Get,
      $"/workspaces/{workspace.Id}/projects/{project.Id}",
      outsider.AccessToken);

    Assert.Equal(HttpStatusCode.NotFound, listResponse.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
  }

  [Fact]
  public async Task ProjectDetails_WithWrongWorkspaceScope_ReturnsNotFound()
  {
    var owner = await RegisterAsync();
    var firstWorkspace = await CreateWorkspaceAsync(owner.AccessToken, "Scoped");
    var secondWorkspace = await CreateWorkspaceAsync(owner.AccessToken, "Other");
    var project = await CreateProjectAsync(
      owner.AccessToken,
      firstWorkspace.Id,
      "Scoped project");

    var response = await SendAsAsync(
      HttpMethod.Get,
      $"/workspaces/{secondWorkspace.Id}/projects/{project.Id}",
      owner.AccessToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task Projects_WithoutToken_ReturnUnauthorized()
  {
    var response = await _client.GetAsync(
      $"/workspaces/{Guid.NewGuid()}/projects");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  private async Task<AuthResponse> RegisterAsync()
  {
    var email = $"project-{Guid.NewGuid():N}@coordina.test";

    var response = await _client.PostAsJsonAsync("/auth/register", new
    {
      name = "Project Tester",
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

  private async Task<ProjectResponse> CreateProjectAsync(
    string accessToken,
    string workspaceId,
    string name,
    string? projectOwnerId = null)
  {
    var response = await SendAsAsync(
      HttpMethod.Post,
      $"/workspaces/{workspaceId}/projects",
      accessToken,
      new { name, projectOwnerId });

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<ProjectResponse>();

    Assert.NotNull(payload);
    return payload;
  }

  private async Task<ProjectResponse> UpdateProjectAsync(
    string accessToken,
    string workspaceId,
    string projectId,
    object body)
  {
    var response = await SendAsAsync(
      HttpMethod.Patch,
      $"/workspaces/{workspaceId}/projects/{projectId}",
      accessToken,
      body);

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<ProjectResponse>();

    Assert.NotNull(payload);
    return payload;
  }

  private async Task<ProjectResponse> GetProjectAsync(
    string accessToken,
    string workspaceId,
    string projectId)
  {
    var response = await SendAsAsync(
      HttpMethod.Get,
      $"/workspaces/{workspaceId}/projects/{projectId}",
      accessToken);

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<ProjectResponse>();

    Assert.NotNull(payload);
    return payload;
  }

  private async Task<ProjectResponse[]> ListProjectsAsync(
    string accessToken,
    string workspaceId,
    string query = "")
  {
    var response = await SendAsAsync(
      HttpMethod.Get,
      $"/workspaces/{workspaceId}/projects{query}",
      accessToken);

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<ProjectResponse[]>();

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

  private sealed record ProjectResponse(
    string Id,
    string Name,
    string? Description,
    string? Key,
    string? Icon,
    string? Color,
    string WorkspaceId,
    string ProjectOwnerId,
    string? ProjectOwnerName,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ArchivedAt);

  private sealed record CreateWorkspaceInviteResponse(
    string Code,
    DateTimeOffset ExpiresAt);
}
