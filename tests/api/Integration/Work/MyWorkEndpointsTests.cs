using System.Net;
using Coordina.Api.Tests.Support;

namespace Coordina.Api.Tests.Integration.Work;

public sealed class MyWorkEndpointsTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  [Fact]
  public async Task Ranking_MatchesTheBoardPlanningView()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var todo = scope.FirstList.Id;
    var database = await scope.AddCardAsync(todo, new { title = "Database migration", size = "XS" });
    var api = await scope.AddCardAsync(todo, new { title = "API endpoint", priority = "HIGH" });
    var ui = await scope.AddCardAsync(todo, new { title = "Frontend panel", size = "M" });
    await scope.AddDependencyAsync(api.Id, database.Id);
    await scope.AddDependencyAsync(ui.Id, api.Id);
    await scope.SendForAsync<object>(HttpMethod.Put, "/planning-preferences", new { maxSize = "S" });

    var graph = await scope.SendForAsync<BoardGraphResponse>(HttpMethod.Get, "/graph?maxSize=S");
    var work = await GetWorkAsync(scope);

    var planned = graph.PlanItems.ToDictionary(item => item.Card.Id);
    Assert.NotEmpty(work.Next);
    Assert.All(work.Next, item =>
    {
      var onBoard = planned[item.Plan.Card.Id];
      Assert.Equal(
        (onBoard.Score, onBoard.IsActionable, onBoard.FitsMaxSize, string.Join("|", onBoard.Reasons)),
        (item.Plan.Score, item.Plan.IsActionable, item.Plan.FitsMaxSize, string.Join("|", item.Plan.Reasons)));
    });
    Assert.Equal(
      graph.PlanItems.Select(item => item.Card.Id).Where(id => work.Next.Any(item => item.Plan.Card.Id == id)),
      work.Next.Select(item => item.Plan.Card.Id));
  }

  [Fact]
  public async Task Sections_FollowAssignmentProgressAndBlockers()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var lists = scope.Board.Lists.OrderBy(list => list.Position).ToArray();
    var me = new[] { scope.Owner.User.Id };
    var teammate = new[] { (await scope.AddMemberAsync()).User.Id };
    await scope.AddCardAsync(lists[1].Id, new { title = "Doing it", assigneeIds = me });
    var blocker = await scope.AddCardAsync(lists[0].Id, new { title = "Blocker", assigneeIds = teammate });
    var waiting = await scope.AddCardAsync(lists[0].Id, new { title = "Waiting", assigneeIds = me });
    await scope.AddDependencyAsync(waiting.Id, blocker.Id);
    await scope.AddCardAsync(lists[0].Id, "Anyone");
    await scope.AddCardAsync(lists[0].Id, new { title = "Theirs", assigneeIds = teammate });
    await scope.AddCardAsync(lists[2].Id, new { title = "Shipped", assigneeIds = me });

    var work = await GetWorkAsync(scope);

    Assert.Equal(["Doing it"], Titles(work.Now));
    Assert.Equal(["Anyone"], Titles(work.Next));
    Assert.Equal(["Waiting"], Titles(work.Blocked));
    Assert.True(work.Next.Single().IsUnassigned);
  }

  [Fact]
  public async Task UnassignedCards_LeaveNext_WhenThePreferencesExcludeThem()
  {
    var scope = await BoardScope.CreateAsync(Api);
    await scope.AddCardAsync(scope.FirstList.Id, "Anyone");
    await scope.AddCardAsync(scope.FirstList.Id, new { title = "Mine", assigneeIds = new[] { scope.Owner.User.Id } });
    await scope.SendForAsync<object>(HttpMethod.Put, "/planning-preferences", new { includeUnassigned = false });

    var work = await GetWorkAsync(scope);

    Assert.Equal(["Mine"], Titles(work.Next));
  }

  [Fact]
  public async Task Work_FromEveryActiveProject_IsRankedTogether()
  {
    var scope = await BoardScope.CreateAsync(Api);
    await scope.AddCardAsync(scope.FirstList.Id, new { title = "Routine", priority = "LOW" });
    var second = await AddProjectWithCardAsync(scope, "Launch", new { title = "Urgent", priority = "HIGH" });
    var archived = await AddProjectWithCardAsync(scope, "Old", new { title = "Forgotten", priority = "HIGH" });
    await Api.SendAsync(HttpMethod.Delete, $"/workspaces/{scope.Workspace.Id}/projects/{archived}", scope.Owner.AccessToken);

    var work = await GetWorkAsync(scope);

    Assert.Equal(["Urgent", "Routine"], Titles(work.Next));
    Assert.Equal(second, work.Next.First().Project.Id);
    Assert.Equal(2, work.NextTotal);
  }

  [Fact]
  public async Task Get_ForAWorkspaceTheUserIsNotIn_ReturnsNotFound()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var outsider = await Api.RegisterAsync();

    var response = await Api.SendAsync(HttpMethod.Get, $"/me/work?workspaceId={scope.Workspace.Id}", outsider.AccessToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task Get_WithoutAWorkspace_ReturnsValidationProblem()
  {
    var user = await Api.RegisterAsync();

    var response = await Api.SendAsync(HttpMethod.Get, "/me/work", user.AccessToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  private Task<MyWorkResponse> GetWorkAsync(BoardScope scope) =>
    Api.SendForAsync<MyWorkResponse>(HttpMethod.Get, $"/me/work?workspaceId={scope.Workspace.Id}", scope.Owner.AccessToken);

  private async Task<string> AddProjectWithCardAsync(BoardScope scope, string name, object card)
  {
    var project = await Api.CreateProjectAsync(scope.Owner.AccessToken, scope.Workspace.Id, name);
    var board = await Api.CreateBoardAsync(scope.Owner.AccessToken, scope.Workspace.Id, project.Id, "CUSTOM");
    var listId = board.Lists.OrderBy(list => list.Position).First().Id;
    await Api.SendForAsync<BoardResponse>(
      HttpMethod.Post,
      $"/workspaces/{scope.Workspace.Id}/projects/{project.Id}/boards/{board.Id}/lists/{listId}/cards",
      scope.Owner.AccessToken,
      card);

    return project.Id;
  }

  private static string[] Titles(IEnumerable<MyWorkItemResponse> items) =>
    [.. items.Select(item => item.Plan.Card.Title)];
}
