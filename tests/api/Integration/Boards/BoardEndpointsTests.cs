using System.Net;
using System.Net.Http.Json;
using Coordina.Api.Tests.Support;

namespace Coordina.Api.Tests.Integration.Boards;

/// <summary>Creating and reading a board, and who may reach it.</summary>
public sealed class BoardEndpointsTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  private static readonly string[] BackendLabels = ["backend"];

  [Fact]
  public async Task CreateBoard_WithTemplate_GeneratesDefaultListsAndStarterCards()
  {
    var owner = await Api.RegisterAsync();
    var workspace = await Api.CreateWorkspaceAsync(owner.AccessToken, "Board Team");
    var project = await Api.CreateProjectAsync(owner.AccessToken, workspace.Id, "Delivery");

    var response = await Api.SendAsync(
      HttpMethod.Post,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/boards",
      owner.AccessToken,
      new { name = "Sprint board", template = "AGILE_SCRUM" });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var board = await response.Content.ReadFromJsonAsync<BoardResponse>();

    Assert.NotNull(board);
    Assert.Equal(project.Id, board.ProjectId);
    Assert.Equal("AGILE_SCRUM", board.Template);
    Assert.Equal(
      ["Backlog", "Sprint Backlog", "In Progress", "Review", "Done"],
      board.Lists.Select(list => list.Title).ToArray());
    Assert.Contains(
      board.Lists.Single(list => list.Title == "Backlog").Cards,
      card => card.Title == "Log account security events"
        && card.Priority == "MEDIUM"
        && card.Labels.Contains("story"));
  }

  [Fact]
  public async Task CreateBoard_WithCustomTemplate_UsesUserListsWithoutStarterCards()
  {
    var scope = await BoardScope.CreateAsync(Api, "CUSTOM", ["Ideas", "Build", "Launch"]);

    Assert.Equal("CUSTOM", scope.Board.Template);
    Assert.Equal(["Ideas", "Build", "Launch"], scope.Board.Lists.Select(list => list.Title).ToArray());
    Assert.All(scope.Board.Lists, list => Assert.Empty(list.Cards));
  }

  [Fact]
  public async Task CreateBoard_WhenTheProjectHasOne_ReturnsConflictAndKeepsTheFirst()
  {
    var scope = await BoardScope.CreateAsync(Api, "BASIC");

    var response = await Api.SendAsync(
      HttpMethod.Post,
      $"{scope.ProjectPath}/boards",
      scope.Owner.AccessToken,
      new { name = "Second", template = "BLANK" });
    var board = await scope.GetBoardAsync();

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Contains("already has a board", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    Assert.Equal(scope.Board.Id, board.Id);
  }

  [Fact]
  public async Task GetDefaultBoard_ReturnsFullBoardStructure()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var member = await scope.AddMemberAsync();
    await scope.AddCardAsync(
      scope.FirstList.Id,
      new { title = "Assigned card", priority = "HIGH", labels = BackendLabels, assigneeIds = new[] { member.User.Id } });

    var loaded = await scope.GetBoardAsync(member.AccessToken);

    var card = Assert.Single(loaded.Lists.First().Cards);
    Assert.Equal("Assigned card", card.Title);
    Assert.Equal("HIGH", card.Priority);
    Assert.Equal("backend", Assert.Single(card.Labels));
    Assert.Equal(member.User.Id, Assert.Single(card.Assignees).UserId);
  }

  [Fact]
  public async Task BoardMutations_OnCompletedProject_ReturnConflict()
  {
    var scope = await BoardScope.CreateAsync(Api);
    await Api.UpdateProjectAsync(scope.Owner.AccessToken, scope.Workspace.Id, scope.Project.Id, new { status = "COMPLETED" });

    var response = await scope.SendAsync(
      HttpMethod.Post, $"/lists/{scope.FirstList.Id}/cards", new { title = "Should not write" });

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
  }

  [Fact]
  public async Task BoardOperations_ForNonMember_ReturnNotFound()
  {
    var scope = await BoardScope.CreateAsync(Api, "BASIC");
    var outsider = await Api.RegisterAsync();

    var getResponse = await Api.SendAsync(HttpMethod.Get, $"{scope.ProjectPath}/board", outsider.AccessToken);
    var createResponse = await scope.SendAsync(
      HttpMethod.Post, $"/lists/{scope.FirstList.Id}/cards", new { title = "Nope" }, outsider.AccessToken);

    Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, createResponse.StatusCode);
  }

  [Fact]
  public async Task BoardOperations_WithWrongProjectScope_ReturnNotFound()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var secondProject = await Api.CreateProjectAsync(scope.Owner.AccessToken, scope.Workspace.Id, "Second");

    var response = await Api.SendAsync(
      HttpMethod.Post,
      $"/workspaces/{scope.Workspace.Id}/projects/{secondProject.Id}/boards/{scope.Board.Id}/lists/{scope.FirstList.Id}/cards",
      scope.Owner.AccessToken,
      new { title = "Leak" });

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }
}
