using System.Net;
using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Shared.Enums;
using Coordina.Api.Tests.Support;

namespace Coordina.Api.Tests.Integration.Boards;

public sealed class BoardTemplateEndpointsTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  private static readonly string[] CustomListTitles = ["Open", "Closed"];

  public static TheoryData<string> Templates =>
    new(BoardTemplateCatalog.All.Select(template => EnumCode.Format(template.Id)));

  [Fact]
  public async Task ListTemplates_WithoutToken_ReturnsUnauthorized()
  {
    using var client = Factory.CreateClient();

    using var response = await client.GetAsync(new Uri("/board-templates", UriKind.Relative));

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task ListTemplates_ReturnsTheCatalogWithoutCustom()
  {
    var user = await Api.RegisterAsync();

    var templates = await ListTemplatesAsync(user.AccessToken);

    Assert.Equal(
      ["BASIC", "AGILE_SCRUM", "BUG_TRACKING", "PRODUCT_ROADMAP"],
      templates.Select(template => template.Id).ToArray());
    Assert.All(templates, template =>
    {
      Assert.NotEmpty(template.Name);
      Assert.NotEmpty(template.Summary);
      Assert.NotEmpty(template.Lists);
    });
  }

  [Theory]
  [MemberData(nameof(Templates))]
  public async Task CreateBoard_MatchesWhatTheChooserPreviews(string templateId)
  {
    var scope = await CreateScopeAsync();
    var templates = await ListTemplatesAsync(scope.AccessToken);
    var template = templates.Single(item => item.Id == templateId);

    var board = await CreateBoardAsync(scope, new { name = "Board", template = templateId });

    var createdOn = DateOnly.FromDateTime(board.CreatedAt.UtcDateTime);
    var titleByKey = template.Lists
      .SelectMany(list => list.Cards)
      .ToDictionary(card => card.Key, card => card.Title);

    Assert.Equal(template.Lists.Count, board.Lists.Count);

    foreach (var (expected, actual) in template.Lists.Zip(board.Lists))
    {
      Assert.Equal(expected.Title, actual.Title);
      Assert.Equal(expected.TaskStatus, actual.TaskStatus);
      Assert.Equal(expected.Cards.Count, actual.Cards.Count);

      foreach (var (expectedCard, card) in expected.Cards.Zip(actual.Cards))
      {
        var isDone = actual.TaskStatus == "DONE";

        Assert.Equal(expectedCard.Title, card.Title);
        Assert.Equal(expectedCard.Description, card.Description);
        Assert.Equal(expectedCard.Priority, card.Priority);
        Assert.Equal(expectedCard.EstimatedMinutes, card.EstimatedMinutes);
        Assert.Equal(
          expectedCard.DueInDays is { } days ? createdOn.AddDays(days) : null,
          card.DueDate);
        Assert.Equal(expectedCard.Labels, card.Labels);
        Assert.Equal(isDone, card.IsCompleted);
        Assert.Equal(
          expectedCard.Subtasks,
          card.Subtasks.OrderBy(subtask => subtask.Position).Select(subtask => subtask.Title));
        Assert.All(card.Subtasks, subtask => Assert.Equal(isDone, subtask.IsCompleted));
        Assert.Equal(
          expectedCard.DependsOn.Select(key => titleByKey[key]).Order(StringComparer.Ordinal),
          card.Dependencies.Select(dependency => dependency.Title).Order(StringComparer.Ordinal));
      }
    }
  }

  [Theory]
  [MemberData(nameof(Templates))]
  public async Task CreateBoard_ShowsNextWorkOnDayOne(string templateId)
  {
    var scope = await CreateScopeAsync();

    var board = await CreateBoardAsync(scope, new { name = "Board", template = templateId });
    var graph = await GetGraphAsync(scope, board);

    Assert.True(graph.IsAcyclic);
    Assert.NotEmpty(graph.NextCards);
    Assert.All(graph.NextCards, card => Assert.False(card.IsCompleted));
    Assert.Contains(graph.PlanItems, item => item.IsActionable);
    Assert.Contains(graph.PlanItems, item => !item.IsActionable);
    Assert.True(graph.CriticalPath.Count > 1, "The critical path should span several cards.");
  }

  [Theory]
  [MemberData(nameof(Templates))]
  public async Task CreateBoard_ListsOnly_KeepsListsWithoutCards(string templateId)
  {
    var scope = await CreateScopeAsync();
    var templates = await ListTemplatesAsync(scope.AccessToken);
    var template = templates.Single(item => item.Id == templateId);

    var board = await CreateBoardAsync(scope, new
    {
      name = "Board",
      template = templateId,
      includeExampleCards = false
    });
    var graph = await GetGraphAsync(scope, board);

    Assert.Equal(
      template.Lists.Select(list => (list.Title, list.TaskStatus)),
      board.Lists.Select(list => (list.Title, list.TaskStatus)));
    Assert.All(board.Lists, list => Assert.Empty(list.Cards));
    Assert.Empty(graph.NextCards);
  }

  [Fact]
  public async Task CreateBoard_CustomWithExampleCards_StillHasNoCards()
  {
    var scope = await CreateScopeAsync();

    var board = await CreateBoardAsync(scope, new
    {
      name = "Board",
      template = "CUSTOM",
      customListTitles = CustomListTitles,
      includeExampleCards = true
    });

    Assert.All(board.Lists, list => Assert.Empty(list.Cards));
  }

  [Fact]
  public async Task CreateBoard_Blank_StartsEmptyAndGrowsWithItsOwnLists()
  {
    var scope = await CreateScopeAsync();

    var board = await CreateBoardAsync(scope, new { name = "Board", template = "BLANK" });

    Assert.Equal("BLANK", board.Template);
    Assert.Empty(board.Lists);

    var boardPath = $"/workspaces/{scope.WorkspaceId}/projects/{scope.ProjectId}/boards/{board.Id}";
    var graph = await GetGraphAsync(scope, board);
    Assert.True(graph.IsAcyclic);
    Assert.Empty(graph.NextCards);

    await Api.SendForAsync<BoardResponse>(
      HttpMethod.Post,
      $"{boardPath}/lists",
      scope.AccessToken,
      new { title = "Doing" });
    var withLists = await Api.SendForAsync<BoardResponse>(
      HttpMethod.Post,
      $"{boardPath}/lists",
      scope.AccessToken,
      new { title = "Closed", taskStatus = "DONE" });
    var doing = withLists.Lists.Single(list => list.Title == "Doing");
    var withCard = await Api.SendForAsync<BoardResponse>(
      HttpMethod.Post,
      $"{boardPath}/lists/{doing.Id}/cards",
      scope.AccessToken,
      new { title = "Ship it" });
    var card = withCard.Lists.SelectMany(list => list.Cards).Single();

    var completed = await Api.SendForAsync<BoardResponse>(
      HttpMethod.Patch,
      $"{boardPath}/cards/{card.Id}",
      scope.AccessToken,
      new { isCompleted = true });

    var closed = completed.Lists.Single(list => list.Title == "Closed");
    Assert.True(Assert.Single(closed.Cards).IsCompleted);
  }

  private Task<BoardTemplateResponse[]> ListTemplatesAsync(string accessToken) =>
    Api.SendForAsync<BoardTemplateResponse[]>(
      HttpMethod.Get,
      "/board-templates",
      accessToken);

  private Task<BoardResponse> CreateBoardAsync(TestScope scope, object body) =>
    Api.SendForAsync<BoardResponse>(
      HttpMethod.Post,
      $"/workspaces/{scope.WorkspaceId}/projects/{scope.ProjectId}/boards",
      scope.AccessToken,
      body);

  private Task<BoardGraphResponse> GetGraphAsync(TestScope scope, BoardResponse board) =>
    Api.SendForAsync<BoardGraphResponse>(
      HttpMethod.Get,
      $"/workspaces/{scope.WorkspaceId}/projects/{scope.ProjectId}/boards/{board.Id}/graph",
      scope.AccessToken);

  private async Task<TestScope> CreateScopeAsync()
  {
    var owner = await Api.RegisterAsync();
    var workspace = await Api.CreateWorkspaceAsync(owner.AccessToken, "Template Team");
    var project = await Api.CreateProjectAsync(owner.AccessToken, workspace.Id, "Templates");

    return new TestScope(owner.AccessToken, workspace.Id, project.Id);
  }

  private sealed record TestScope(
    string AccessToken,
    string WorkspaceId,
    string ProjectId);
}
