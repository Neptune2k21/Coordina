using System.Net.Http.Json;
using Coordina.Testing;

namespace Coordina.Api.Tests.Support;

/// <summary>
/// HTTP helpers and scenario builders shared by integration tests. Tests
/// describe behavior; setup such as "a user owns a workspace with a project"
/// stays a one-liner here.
/// </summary>
public sealed class ApiTestClient(HttpClient http)
{
  public const string Password = "Password123!";

  /// <summary>Lists used for CUSTOM boards when a test does not care.</summary>
  public static readonly string[] DefaultListTitles = ["To Do", "In Progress", "Done"];

  /// <summary>Raw client for anonymous or deliberately malformed requests.</summary>
  public HttpClient Http => http;

  public static string UniqueEmail(string prefix = "user") =>
    $"{prefix}-{Guid.NewGuid():N}@coordina.test";

  public async Task<AuthResponse> RegisterAsync(string name = "Test User")
  {
    var response = await http.PostAsJsonAsync("/auth/register", new
    {
      name,
      email = UniqueEmail(),
      password = Password
    });

    return await AuthorizedRequests.ReadSuccessAsync<AuthResponse>(response);
  }

  public Task<HttpResponseMessage> SendAsync(
    HttpMethod method,
    string path,
    string accessToken,
    object? body = null) =>
    http.SendAuthorizedAsync(method, path, accessToken, body);

  /// <summary>Sends a request that must succeed and returns its JSON body.</summary>
  public Task<T> SendForAsync<T>(
    HttpMethod method,
    string path,
    string accessToken,
    object? body = null) =>
    http.SendAuthorizedForAsync<T>(method, path, accessToken, body);

  public Task<WorkspaceResponse> CreateWorkspaceAsync(
    string accessToken,
    string name = "Test Team") =>
    SendForAsync<WorkspaceResponse>(
      HttpMethod.Post,
      "/workspaces",
      accessToken,
      new { name });

  public Task<CreateWorkspaceInviteResponse> CreateInviteAsync(
    string accessToken,
    string workspaceId) =>
    SendForAsync<CreateWorkspaceInviteResponse>(
      HttpMethod.Post,
      $"/workspaces/{workspaceId}/invites",
      accessToken);

  public Task<WorkspaceResponse> JoinWorkspaceAsync(
    string accessToken,
    string inviteCode) =>
    SendForAsync<WorkspaceResponse>(
      HttpMethod.Post,
      "/workspaces/join",
      accessToken,
      new { inviteCode });

  /// <summary>Registers a new user and adds them to the workspace as MEMBER.</summary>
  public async Task<AuthResponse> AddMemberAsync(
    string ownerAccessToken,
    string workspaceId,
    string name = "Teammate")
  {
    var member = await RegisterAsync(name);
    var invite = await CreateInviteAsync(ownerAccessToken, workspaceId);
    await JoinWorkspaceAsync(member.AccessToken, invite.Code);

    return member;
  }

  public Task<ProjectResponse> CreateProjectAsync(
    string accessToken,
    string workspaceId,
    string name = "Test project",
    string? projectOwnerId = null) =>
    SendForAsync<ProjectResponse>(
      HttpMethod.Post,
      $"/workspaces/{workspaceId}/projects",
      accessToken,
      new { name, projectOwnerId });

  public Task<ProjectResponse> GetProjectAsync(
    string accessToken,
    string workspaceId,
    string projectId) =>
    SendForAsync<ProjectResponse>(
      HttpMethod.Get,
      $"/workspaces/{workspaceId}/projects/{projectId}",
      accessToken);

  public Task<ProjectResponse[]> ListProjectsAsync(
    string accessToken,
    string workspaceId,
    string query = "") =>
    SendForAsync<ProjectResponse[]>(
      HttpMethod.Get,
      $"/workspaces/{workspaceId}/projects{query}",
      accessToken);

  public Task<ProjectResponse> UpdateProjectAsync(
    string accessToken,
    string workspaceId,
    string projectId,
    object input) =>
    SendForAsync<ProjectResponse>(
      HttpMethod.Patch,
      $"/workspaces/{workspaceId}/projects/{projectId}",
      accessToken,
      input);

  public Task<BoardResponse> CreateBoardAsync(
    string accessToken,
    string workspaceId,
    string projectId,
    string template,
    string[]? customListTitles = null,
    string name = "Project board") =>
    SendForAsync<BoardResponse>(
      HttpMethod.Post,
      $"/workspaces/{workspaceId}/projects/{projectId}/boards",
      accessToken,
      new { name, template, customListTitles = customListTitles ?? DefaultListTitles });

  public Task<BoardResponse> GetDefaultBoardAsync(
    string accessToken,
    string workspaceId,
    string projectId) =>
    SendForAsync<BoardResponse>(
      HttpMethod.Get,
      $"/workspaces/{workspaceId}/projects/{projectId}/board",
      accessToken);
}
