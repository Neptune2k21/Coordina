using System.Net;
using Coordina.Api.Tests.Support;

namespace Coordina.Api.Tests.Integration.Boards;

/// <summary>Moving, editing and collaborating on cards, and their dependencies.</summary>
public sealed class BoardCardEndpointsTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  private static readonly string[] DetailedLabels = ["backend", "review"];

  [Fact]
  public async Task MoveCard_ChangesListForDragAndDrop()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var lists = scope.Board.Lists.OrderBy(list => list.Position).ToArray();
    var card = await scope.AddCardAsync(lists[0].Id, "Drag me");

    var moved = await scope.SendForAsync<BoardResponse>(
      HttpMethod.Patch, $"/cards/{card.Id}/move", new { listId = lists[2].Id });

    Assert.Empty(moved.List(lists[0].Id).Cards);
    Assert.Contains(
      moved.List(lists[2].Id).Cards,
      item => item.Title == "Drag me" && item.ListId == lists[2].Id);
  }

  [Fact]
  public async Task UpdateCard_WithPartialPayload_PreservesExistingDetails()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var member = await scope.AddMemberAsync();
    var dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
    var card = await scope.AddCardAsync(scope.FirstList.Id, new
    {
      title = "Detailed card",
      description = "Keep these details.",
      priority = "MEDIUM",
      dueDate,
      labels = DetailedLabels,
      assigneeIds = new[] { member.User.Id }
    });

    var updated = (await scope.UpdateCardAsync(card.Id, new { title = "Renamed card" })).Card(card.Id);

    Assert.Equal("Renamed card", updated.Title);
    Assert.Equal("Keep these details.", updated.Description);
    Assert.Equal("MEDIUM", updated.Priority);
    Assert.Equal(dueDate, updated.DueDate);
    Assert.Equal(["backend", "review"], updated.Labels.ToArray());
    Assert.Equal(member.User.Id, Assert.Single(updated.Assignees).UserId);
  }

  [Fact]
  public async Task UpdateCard_WithNewAssignee_AssignsWorkspaceMember()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var member = await scope.AddMemberAsync();
    var card = await scope.AddCardAsync(scope.FirstList.Id, "Needs ownership");
    Assert.Empty(card.Assignees);

    var updated = (await scope.UpdateCardAsync(card.Id, new { assigneeIds = new[] { member.User.Id } })).Card(card.Id);

    Assert.Equal(member.User.Id, Assert.Single(updated.Assignees).UserId);
  }

  [Fact]
  public async Task UpdateCard_WithClearDueDate_RemovesExistingDueDate()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var card = await scope.AddCardAsync(
      scope.FirstList.Id,
      new { title = "Dated card", dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)) });

    var updated = await scope.UpdateCardAsync(card.Id, new { title = "Dated card", clearDueDate = true });

    Assert.Null(updated.Card(card.Id).DueDate);
  }

  [Fact]
  public async Task CardCollaborationEndpoints_PersistCompletionCommentsSubtasksAndDependencies()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var first = await scope.AddCardAsync(scope.FirstList.Id, "Blocked by copy");
    var second = await scope.AddCardAsync(scope.FirstList.Id, "Dependency target");

    await scope.UpdateCardAsync(first.Id, new { title = first.Title, isCompleted = true });
    await scope.SendForAsync<BoardResponse>(
      HttpMethod.Post, $"/cards/{first.Id}/comments", new { body = "Ship it after QA signs off." });
    var withSubtask = await scope.SendForAsync<BoardResponse>(
      HttpMethod.Post, $"/cards/{first.Id}/subtasks", new { title = "QA checklist" });
    var subtask = withSubtask.Card(first.Id).Subtasks.Single();
    await scope.SendForAsync<BoardResponse>(
      HttpMethod.Patch, $"/cards/{first.Id}/subtasks/{subtask.Id}", new { isCompleted = true });
    await scope.AddDependencyAsync(first.Id, second.Id);

    var loaded = await scope.GetBoardAsync();
    var card = loaded.Card(first.Id);

    Assert.True(card.IsCompleted);
    Assert.Equal(loaded.Lists.OrderBy(item => item.Position).Last().Id, card.ListId);
    Assert.Equal(scope.Owner.User.Id, card.CompletedBy?.UserId);
    Assert.Equal("Ship it after QA signs off.", Assert.Single(card.Comments).Body);
    Assert.Equal(scope.Owner.User.Id, Assert.Single(card.Comments).Author.UserId);
    Assert.True(Assert.Single(card.Subtasks).IsCompleted);
    Assert.Equal(scope.Owner.User.Id, Assert.Single(card.Subtasks).CompletedBy?.UserId);
    Assert.Equal(second.Id, Assert.Single(card.Dependencies).CardId);
    Assert.Equal("Dependency target", Assert.Single(card.Dependencies).Title);
  }

  [Fact]
  public async Task AddCardDependency_WhenItWouldCreateCycle_ReturnsConflict()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var first = await scope.AddCardAsync(scope.FirstList.Id, "Ship API");
    var second = await scope.AddCardAsync(scope.FirstList.Id, "Prepare database");
    await scope.AddDependencyAsync(first.Id, second.Id);

    var cycleResponse = await scope.SendAsync(
      HttpMethod.Post, $"/cards/{second.Id}/dependencies", new { dependsOnCardId = first.Id });

    Assert.Equal(HttpStatusCode.Conflict, cycleResponse.StatusCode);
  }

  [Fact]
  public async Task DependencyGraphEndpoints_ReturnPlanningSignals()
  {
    var (scope, database, api, ui) = await CreateDependencyChainAsync();

    var graph = await scope.SendForAsync<BoardGraphResponse>(HttpMethod.Get, "/graph");

    Assert.True(graph.IsAcyclic);
    Assert.Equal(database.Id, Assert.Single(graph.ReadyCards).Id);
    Assert.Equal([database.Id, api.Id, ui.Id], graph.DependencyOrder.Select(card => card.Id).ToArray());
    Assert.Equal([database.Id, api.Id, ui.Id], graph.CriticalPath.Select(card => card.Id).ToArray());
    var nextCard = Assert.Single(graph.NextCards);
    Assert.Equal(database.Id, nextCard.Id);
    Assert.Equal(2, nextCard.TransitiveDependentCount);
    Assert.True(nextCard.IsCriticalPath);
    var firstPlanItem = graph.PlanItems.First();
    Assert.Equal(database.Id, firstPlanItem.Card.Id);
    Assert.True(firstPlanItem.IsActionable);
    Assert.Equal(0, firstPlanItem.BlockerCount);
    Assert.Contains("Ready now", firstPlanItem.Reasons);
    Assert.Contains("Critical path", firstPlanItem.Reasons);
    Assert.Contains("Unlocks 2", firstPlanItem.Reasons);
  }

  [Fact]
  public async Task CardDependencyAnalysis_DescribesWhatBlocksAndWhatItUnlocks()
  {
    var (scope, database, api, ui) = await CreateDependencyChainAsync();

    var analysis = await scope.SendForAsync<BoardCardDependencyAnalysisResponse>(
      HttpMethod.Get, $"/cards/{api.Id}/dependency-analysis");

    Assert.False(analysis.IsStructurallyReady);
    Assert.False(analysis.IsUnblocked);
    Assert.True(analysis.IsOnCriticalPath);
    Assert.Equal(1, analysis.DependencyDepth);
    Assert.Equal(1, analysis.DependentDepth);
    Assert.Equal(1, analysis.TransitiveDependentCount);
    Assert.Equal(database.Id, Assert.Single(analysis.BlockingDependencies).Id);
    Assert.DoesNotContain(analysis.SuggestedDependencies, candidate => candidate.Id == ui.Id);
    Assert.Equal(ui.Id, Assert.Single(analysis.ImpactedDependents).Id);
    Assert.Equal(ui.Id, Assert.Single(analysis.DirectlyUnlockedDependents).Id);
  }

  [Fact]
  public async Task AssigningNonMember_ReturnsValidationProblem()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var outsider = await Api.RegisterAsync();

    var response = await scope.SendAsync(
      HttpMethod.Post,
      $"/lists/{scope.FirstList.Id}/cards",
      new { title = "Invalid assignment", assigneeIds = new[] { outsider.User.Id } });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task CreatingCard_WithPastDueDate_ReturnsValidationProblem()
  {
    var scope = await BoardScope.CreateAsync(Api);

    var response = await scope.SendAsync(
      HttpMethod.Post,
      $"/lists/{scope.FirstList.Id}/cards",
      new { title = "Past due", dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Member_CannotDeleteCard_WhenNotProjectOwner()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var member = await scope.AddMemberAsync();
    var card = await scope.AddCardAsync(scope.FirstList.Id, "Protected");

    var response = await scope.SendAsync(HttpMethod.Delete, $"/cards/{card.Id}", accessToken: member.AccessToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // Frontend panel depends on API endpoint, which depends on Database migration.
  private async Task<(BoardScope Scope, BoardCardResponse Database, BoardCardResponse Api, BoardCardResponse Ui)>
    CreateDependencyChainAsync()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var database = await scope.AddCardAsync(scope.FirstList.Id, "Database migration");
    var api = await scope.AddCardAsync(scope.FirstList.Id, "API endpoint");
    var ui = await scope.AddCardAsync(scope.FirstList.Id, "Frontend panel");
    await scope.AddDependencyAsync(api.Id, database.Id);
    await scope.AddDependencyAsync(ui.Id, api.Id);

    return (scope, database, api, ui);
  }
}
