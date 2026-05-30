using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Coordina.Api.Tests.Support;

namespace Coordina.Api.Tests;

public sealed class BoardEndpointsTests(ApiTestApplicationFactory factory)
  : IClassFixture<ApiTestApplicationFactory>
{
  private static readonly string[] BackendLabels = ["backend"];
  private static readonly string[] DetailedLabels = ["backend", "review"];

  private readonly HttpClient _client = factory.CreateClient();

  [Fact]
  public async Task CreateBoard_WithTemplate_GeneratesDefaultListsAndStarterCards()
  {
    var owner = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Board Team");
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Delivery");

    var response = await SendAsAsync(
      HttpMethod.Post,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/boards",
      owner.AccessToken,
      new
      {
        name = "Sprint board",
        template = "AGILE_SCRUM"
      });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var board = await response.Content.ReadFromJsonAsync<BoardResponse>();

    Assert.NotNull(board);
    Assert.Equal(project.Id, board.ProjectId);
    Assert.Equal("AGILE_SCRUM", board.Template);
    Assert.Equal(
      ["Backlog", "Sprint", "In Progress", "Review", "Done"],
      board.Lists.Select(list => list.Title).ToArray());
    Assert.Contains(
      board.Lists.Single(list => list.Title == "Backlog").Cards,
      card => card.Title == "Define user story"
        && card.Priority == "MEDIUM"
        && card.Labels.Contains("story"));
  }

  [Fact]
  public async Task CreateBoard_WithCustomTemplate_UsesUserListsWithoutStarterCards()
  {
    var owner = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Custom Team");
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Custom Board");

    var board = await CreateBoardAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      "CUSTOM",
      ["Ideas", "Build", "Launch"]);

    Assert.Equal("CUSTOM", board.Template);
    Assert.Equal(
      ["Ideas", "Build", "Launch"],
      board.Lists.Select(list => list.Title).ToArray());
    Assert.All(board.Lists, list => Assert.Empty(list.Cards));
  }

  [Fact]
  public async Task GetDefaultBoard_ReturnsFullBoardStructure()
  {
    var owner = await RegisterAsync();
    var member = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Full Team");
    var invite = await CreateInviteAsync(owner.AccessToken, workspace.Id);
    await JoinWorkspaceAsync(member.AccessToken, invite.Code);
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Board");
    var board = await CreateBoardAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      "CUSTOM");
    var firstList = board.Lists.OrderBy(list => list.Position).First();
    await CreateCardAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      board.Id,
      firstList.Id,
      "Assigned card",
      member.User.Id);

    var loaded = await GetDefaultBoardAsync(
      member.AccessToken,
      workspace.Id,
      project.Id);

    var card = Assert.Single(loaded.Lists.First().Cards);
    Assert.Equal("Assigned card", card.Title);
    Assert.Equal("HIGH", card.Priority);
    Assert.Equal("backend", Assert.Single(card.Labels));
    Assert.Equal(member.User.Id, Assert.Single(card.Assignees).UserId);
  }

  [Fact]
  public async Task MoveCard_ChangesListForDragAndDrop()
  {
    var owner = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Move Team");
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Board");
    var board = await CreateBoardAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      "CUSTOM");
    var lists = board.Lists.OrderBy(list => list.Position).ToArray();
    var created = await CreateCardAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      board.Id,
      lists[0].Id,
      "Drag me");
    var card = created.Lists
      .SelectMany(list => list.Cards)
      .Single();

    var moved = await MoveCardAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      board.Id,
      card.Id,
      lists[2].Id);

    Assert.Empty(moved.Lists.Single(list => list.Id == lists[0].Id).Cards);
    Assert.Contains(
      moved.Lists.Single(list => list.Id == lists[2].Id).Cards,
      card => card.Title == "Drag me" && card.ListId == lists[2].Id);
  }

  [Fact]
  public async Task UpdateCard_WithPartialPayload_PreservesExistingDetails()
  {
    var owner = await RegisterAsync();
    var member = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Patch Team");
    var invite = await CreateInviteAsync(owner.AccessToken, workspace.Id);
    await JoinWorkspaceAsync(member.AccessToken, invite.Code);
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Board");
    var board = await CreateBoardAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      "CUSTOM");
    var list = board.Lists.First();
    var dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
    var created = await SendAsAsync(
      HttpMethod.Post,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/boards/{board.Id}/lists/{list.Id}/cards",
      owner.AccessToken,
      new
      {
        title = "Detailed card",
        description = "Keep these details.",
        priority = "MEDIUM",
        dueDate,
        labels = DetailedLabels,
        assigneeIds = new[] { member.User.Id }
      });
    created.EnsureSuccessStatusCode();
    var withCard = await created.Content.ReadFromJsonAsync<BoardResponse>();

    Assert.NotNull(withCard);
    var card = withCard.Lists.Single(item => item.Id == list.Id).Cards.Single();

    var updateResponse = await SendAsAsync(
      HttpMethod.Patch,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/boards/{board.Id}/cards/{card.Id}",
      owner.AccessToken,
      new { title = "Renamed card" });

    Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

    var updatedBoard = await updateResponse.Content.ReadFromJsonAsync<BoardResponse>();

    Assert.NotNull(updatedBoard);
    var updated = updatedBoard.Lists
      .Single(item => item.Id == list.Id)
      .Cards
      .Single();
    Assert.Equal("Renamed card", updated.Title);
    Assert.Equal("Keep these details.", updated.Description);
    Assert.Equal("MEDIUM", updated.Priority);
    Assert.Equal(dueDate, updated.DueDate);
    Assert.Equal(["backend", "review"], updated.Labels.ToArray());
    Assert.Equal(member.User.Id, Assert.Single(updated.Assignees).UserId);
  }

  [Fact]
  public async Task UpdateCard_WithClearDueDate_RemovesExistingDueDate()
  {
    var owner = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Clear Date Team");
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Board");
    var board = await CreateBoardAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      "CUSTOM");
    var list = board.Lists.First();
    var dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
    var created = await SendAsAsync(
      HttpMethod.Post,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/boards/{board.Id}/lists/{list.Id}/cards",
      owner.AccessToken,
      new
      {
        title = "Dated card",
        dueDate
      });
    created.EnsureSuccessStatusCode();
    var withCard = await created.Content.ReadFromJsonAsync<BoardResponse>();

    Assert.NotNull(withCard);
    var card = withCard.Lists.Single(item => item.Id == list.Id).Cards.Single();

    var updateResponse = await SendAsAsync(
      HttpMethod.Patch,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/boards/{board.Id}/cards/{card.Id}",
      owner.AccessToken,
      new
      {
        title = "Dated card",
        clearDueDate = true
      });

    Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

    var updatedBoard = await updateResponse.Content.ReadFromJsonAsync<BoardResponse>();

    Assert.NotNull(updatedBoard);
    Assert.Null(updatedBoard.Lists.Single(item => item.Id == list.Id).Cards.Single().DueDate);
  }

  [Fact]
  public async Task BoardMutations_OnCompletedProject_ReturnConflict()
  {
    var owner = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Readonly Team");
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Completed board");
    var board = await CreateBoardAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      "CUSTOM");
    var list = board.Lists.First();
    await UpdateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      new { status = "COMPLETED" });

    var response = await SendAsAsync(
      HttpMethod.Post,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/boards/{board.Id}/lists/{list.Id}/cards",
      owner.AccessToken,
      new { title = "Should not write" });

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
  }

  [Fact]
  public async Task BoardOperations_ForNonMember_ReturnNotFound()
  {
    var owner = await RegisterAsync();
    var outsider = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Private Team");
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Private");
    var board = await CreateBoardAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      "BASIC");
    var list = board.Lists.First();

    var getResponse = await SendAsAsync(
      HttpMethod.Get,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/board",
      outsider.AccessToken);
    var createResponse = await SendAsAsync(
      HttpMethod.Post,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/boards/{board.Id}/lists/{list.Id}/cards",
      outsider.AccessToken,
      new { title = "Nope" });

    Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, createResponse.StatusCode);
  }

  [Fact]
  public async Task BoardOperations_WithWrongProjectScope_ReturnNotFound()
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
    var board = await CreateBoardAsync(
      owner.AccessToken,
      workspace.Id,
      firstProject.Id,
      "CUSTOM");
    var list = board.Lists.First();

    var response = await SendAsAsync(
      HttpMethod.Post,
      $"/workspaces/{workspace.Id}/projects/{secondProject.Id}/boards/{board.Id}/lists/{list.Id}/cards",
      owner.AccessToken,
      new { title = "Leak" });

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task AssigningNonMember_ReturnsValidationProblem()
  {
    var owner = await RegisterAsync();
    var outsider = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Assign Team");
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Board");
    var board = await CreateBoardAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      "CUSTOM");
    var list = board.Lists.First();

    var response = await SendAsAsync(
      HttpMethod.Post,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/boards/{board.Id}/lists/{list.Id}/cards",
      owner.AccessToken,
      new
      {
        title = "Invalid assignment",
        assigneeIds = new[] { outsider.User.Id }
      });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task CreatingCard_WithPastDueDate_ReturnsValidationProblem()
  {
    var owner = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Date Team");
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Board");
    var board = await CreateBoardAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      "CUSTOM");
    var list = board.Lists.First();

    var response = await SendAsAsync(
      HttpMethod.Post,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/boards/{board.Id}/lists/{list.Id}/cards",
      owner.AccessToken,
      new
      {
        title = "Past due",
        dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))
      });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Member_CannotDeleteCard_WhenNotProjectOwner()
  {
    var owner = await RegisterAsync();
    var member = await RegisterAsync();
    var workspace = await CreateWorkspaceAsync(owner.AccessToken, "Delete Team");
    var invite = await CreateInviteAsync(owner.AccessToken, workspace.Id);
    await JoinWorkspaceAsync(member.AccessToken, invite.Code);
    var project = await CreateProjectAsync(
      owner.AccessToken,
      workspace.Id,
      "Board");
    var board = await CreateBoardAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      "CUSTOM");
    var list = board.Lists.First();
    var withCard = await CreateCardAsync(
      owner.AccessToken,
      workspace.Id,
      project.Id,
      board.Id,
      list.Id,
      "Protected");
    var card = withCard.Lists.First().Cards.First();

    var response = await SendAsAsync(
      HttpMethod.Delete,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/boards/{board.Id}/cards/{card.Id}",
      member.AccessToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  private async Task<AuthResponse> RegisterAsync()
  {
    var email = $"board-{Guid.NewGuid():N}@coordina.test";

    var response = await _client.PostAsJsonAsync("/auth/register", new
    {
      name = "Board Tester",
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
    object input)
  {
    var response = await SendAsAsync(
      HttpMethod.Patch,
      $"/workspaces/{workspaceId}/projects/{projectId}",
      accessToken,
      input);

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<ProjectResponse>();

    Assert.NotNull(payload);
    return payload;
  }

  private async Task<BoardResponse> CreateBoardAsync(
    string accessToken,
    string workspaceId,
    string projectId,
    string template,
    string[]? customListTitles = null)
  {
    var response = await SendAsAsync(
      HttpMethod.Post,
      $"/workspaces/{workspaceId}/projects/{projectId}/boards",
      accessToken,
      new
      {
        name = "Project board",
        template,
        customListTitles = customListTitles ?? ["To Do", "In Progress", "Done"]
      });

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<BoardResponse>();

    Assert.NotNull(payload);
    return payload;
  }

  private async Task<BoardResponse> GetDefaultBoardAsync(
    string accessToken,
    string workspaceId,
    string projectId)
  {
    var response = await SendAsAsync(
      HttpMethod.Get,
      $"/workspaces/{workspaceId}/projects/{projectId}/board",
      accessToken);

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<BoardResponse>();

    Assert.NotNull(payload);
    return payload;
  }

  private async Task<BoardResponse> CreateCardAsync(
    string accessToken,
    string workspaceId,
    string projectId,
    string boardId,
    string listId,
    string title,
    string? assigneeId = null)
  {
    var response = await SendAsAsync(
      HttpMethod.Post,
      $"/workspaces/{workspaceId}/projects/{projectId}/boards/{boardId}/lists/{listId}/cards",
      accessToken,
      new
      {
        title,
        priority = "HIGH",
        labels = BackendLabels,
        assigneeIds = assigneeId is null ? [] : new[] { assigneeId }
      });

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<BoardResponse>();

    Assert.NotNull(payload);
    return payload;
  }

  private async Task<BoardResponse> MoveCardAsync(
    string accessToken,
    string workspaceId,
    string projectId,
    string boardId,
    string cardId,
    string listId)
  {
    var response = await SendAsAsync(
      HttpMethod.Patch,
      $"/workspaces/{workspaceId}/projects/{projectId}/boards/{boardId}/cards/{cardId}/move",
      accessToken,
      new { listId });

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<BoardResponse>();

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

  private sealed record BoardResponse(
    string Id,
    string ProjectId,
    string Name,
    string Template,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<BoardListResponse> Lists);

  private sealed record BoardListResponse(
    string Id,
    string BoardId,
    string Title,
    int Position,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<BoardCardResponse> Cards);

  private sealed record BoardCardResponse(
    string Id,
    string BoardId,
    string ListId,
    string Title,
    string? Description,
    string? Priority,
    DateOnly? DueDate,
    IReadOnlyCollection<string> Labels,
    int Position,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<BoardCardAssigneeResponse> Assignees);

  private sealed record BoardCardAssigneeResponse(
    string UserId,
    string? Name,
    string? Email);

  private sealed record CreateWorkspaceInviteResponse(
    string Code,
    DateTimeOffset ExpiresAt);
}
