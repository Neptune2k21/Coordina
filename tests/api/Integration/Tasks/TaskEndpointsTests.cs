using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Coordina.Api.Tests.Support;

namespace Coordina.Api.Tests;

public sealed class TaskEndpointsTests(ApiTestApplicationFactory factory)
  : IClassFixture<ApiTestApplicationFactory>
{
  private readonly HttpClient _client = factory.CreateClient();

  [Fact]
  public async Task CreateTask_WithinProject_ReturnsTodoTask()
  {
    var owner = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Tasks Team");
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Board");

    var response = await SendAsAsync(
      HttpMethod.Post,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/tasks",
      owner.AccessToken,
      new
      {
        title = "Design task model",
        description = "Add the first project-scoped task.",
        priority = "HIGH"
      });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var task = await response.Content.ReadFromJsonAsync<TaskResponse>();

    Assert.NotNull(task);
    Assert.Equal(project.Id, task.ProjectId);
    Assert.Equal("Design task model", task.Title);
    Assert.Equal("Add the first project-scoped task.", task.Description);
    Assert.Equal("TODO", task.Status);
    Assert.Equal("HIGH", task.Priority);
    Assert.Equal(task.CreatedAt, task.UpdatedAt);
  }

  [Fact]
  public async Task ListTasks_IsScopedByProject()
  {
    var owner = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Scoped Team");
    var firstProject = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "First");
    var secondProject = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Second");
    var firstTask = await CreateTaskAsync(
      owner.AccessToken,
      workspace.Id,
      firstProject.Id,
      "First task");
    var secondTask = await CreateTaskAsync(
      owner.AccessToken,
      workspace.Id,
      secondProject.Id,
      "Second task");

    var firstTasks = await ListTasksAsync(
      owner.AccessToken,
      workspace.Id,
      firstProject.Id);
    var secondTasks = await ListTasksAsync(
      owner.AccessToken,
      workspace.Id,
      secondProject.Id);

    Assert.Contains(firstTasks, task => task.Id == firstTask.Id);
    Assert.DoesNotContain(firstTasks, task => task.Id == secondTask.Id);
    Assert.Contains(secondTasks, task => task.Id == secondTask.Id);
    Assert.DoesNotContain(secondTasks, task => task.Id == firstTask.Id);
  }

  [Fact]
  public async Task ChangeTaskStatus_UpdatesKanbanColumn()
  {
    var owner = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Status Team");
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Board");
    var task = await CreateTaskAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      "Move card");

    var updated = await ChangeStatusAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      task.Id,
      "IN_PROGRESS");

    Assert.Equal("IN_PROGRESS", updated.Status);
    Assert.True(updated.UpdatedAt > task.UpdatedAt);
  }

  [Fact]
  public async Task UpdateTask_AllowsWorkspaceMembers()
  {
    var owner = await RegisterAsync();
    var member = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Collab Team");
    var invite = await CreateInviteAsync(owner.AccessToken, workspace.Id);
    await JoinWorkspaceAsync(member.AccessToken, invite.Code);
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Board");
    var task = await CreateTaskAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      "Draft");

    var response = await SendAsAsync(
      HttpMethod.Patch,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/tasks/{task.Id}",
      member.AccessToken,
      new
      {
        title = "Reviewed",
        priority = "LOW"
      });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var updated = await response.Content.ReadFromJsonAsync<TaskResponse>();

    Assert.NotNull(updated);
    Assert.Equal("Reviewed", updated.Title);
    Assert.Equal("LOW", updated.Priority);
  }

  [Fact]
  public async Task Tasks_ForNonMember_ReturnNotFound()
  {
    var owner = await RegisterAsync();
    var outsider = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Private Team");
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Private project");
    var task = await CreateTaskAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      "Private task");

    var listResponse = await SendAsAsync(
      HttpMethod.Get,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/tasks",
      outsider.AccessToken);
    var updateResponse = await SendAsAsync(
      HttpMethod.Patch,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/tasks/{task.Id}",
      outsider.AccessToken,
      new { title = "Nope" });

    Assert.Equal(HttpStatusCode.NotFound, listResponse.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
  }

  [Fact]
  public async Task TaskOperations_WithWrongProjectScope_ReturnNotFound()
  {
    var owner = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Scope Team");
    var firstProject = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "First");
    var secondProject = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Second");
    var task = await CreateTaskAsync(
      owner.AccessToken,
      workspace.Id,
      firstProject.Id,
      "Scoped task");

    var response = await SendAsAsync(
      HttpMethod.Patch,
      $"/workspaces/{workspace.Id}/projects/{secondProject.Id}/tasks/{task.Id}",
      owner.AccessToken,
      new { title = "Leak" });

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task Member_WhoIsNotProjectOwner_CannotDeleteTask()
  {
    var owner = await RegisterAsync();
    var member = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Delete Team");
    var invite = await CreateInviteAsync(owner.AccessToken, workspace.Id);
    await JoinWorkspaceAsync(member.AccessToken, invite.Code);
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Owner project");
    var task = await CreateTaskAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      "Protected task");

    var response = await SendAsAsync(
      HttpMethod.Delete,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/tasks/{task.Id}",
      member.AccessToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task ProjectOwner_CanDeleteTask()
  {
    var owner = await RegisterAsync();
    var member = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Owner Team");
    var invite = await CreateInviteAsync(owner.AccessToken, workspace.Id);
    await JoinWorkspaceAsync(member.AccessToken, invite.Code);
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Member owned",
      member.User.Id);
    var task = await CreateTaskAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      "Owned task");

    var response = await SendAsAsync(
      HttpMethod.Delete,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/tasks/{task.Id}",
      member.AccessToken);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

    var tasks = await ListTasksAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id);

    Assert.DoesNotContain(tasks, item => item.Id == task.Id);
  }

  [Fact]
  public async Task InvalidTaskStatus_ReturnsValidationProblem()
  {
    var owner = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Validation Team");
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Board");
    var task = await CreateTaskAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      "Move");

    var response = await SendAsAsync(
      HttpMethod.Patch,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/tasks/{task.Id}/status",
      owner.AccessToken,
      new { status = "BLOCKED" });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  private async Task<AuthResponse> RegisterAsync()
  {
    var email = $"task-{Guid.NewGuid():N}@coordina.test";

    var response = await _client.PostAsJsonAsync("/auth/register", new
    {
      name = "Task Tester",
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

  private async Task<TaskResponse> CreateTaskAsync(
    string accessToken,
    string workspaceId,
    string projectId,
    string title)
  {
    var response = await SendAsAsync(
      HttpMethod.Post,
      $"/workspaces/{workspaceId}/projects/{projectId}/tasks",
      accessToken,
      new { title });

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<TaskResponse>();

    Assert.NotNull(payload);
    return payload;
  }

  private async Task<TaskResponse> ChangeStatusAsync(
    string accessToken,
    string workspaceId,
    string projectId,
    string taskId,
    string status)
  {
    var response = await SendAsAsync(
      HttpMethod.Patch,
      $"/workspaces/{workspaceId}/projects/{projectId}/tasks/{taskId}/status",
      accessToken,
      new { status });

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<TaskResponse>();

    Assert.NotNull(payload);
    return payload;
  }

  private async Task<TaskResponse[]> ListTasksAsync(
    string accessToken,
    string workspaceId,
    string projectId)
  {
    var response = await SendAsAsync(
      HttpMethod.Get,
      $"/workspaces/{workspaceId}/projects/{projectId}/tasks",
      accessToken);

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<TaskResponse[]>();

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

  private sealed record TaskResponse(
    string Id,
    string ProjectId,
    string Title,
    string? Description,
    string Status,
    string? Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

  private sealed record CreateWorkspaceInviteResponse(
    string Code,
    DateTimeOffset ExpiresAt);
}
