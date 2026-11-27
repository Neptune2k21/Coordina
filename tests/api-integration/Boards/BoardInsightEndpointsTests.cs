using System.Net;
using Coordina.Api.Integration.Tests.Support;
using Coordina.Api.Modules.Boards.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Coordina.Api.Integration.Tests.Boards;

/// <summary>What the engine makes of a board: its plan, what to do, what to ask, what a change would do.</summary>
public sealed class BoardInsightEndpointsTests(ApiTestApplicationFactory factory) : ApiTest(factory)
{
  private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
  private static readonly string[] Backend = ["backend"];

  [Fact]
  public async Task Plan_SaysWhoTakesWhatAndWhatItAssumes()
  {
    var release = await ReleaseAsync();

    var plan = await release.Scope.SendForAsync<BoardPlanResponse>(HttpMethod.Get, "/plan");

    Assert.Equal(release.Launch.Id, Assert.Single(plan.Milestones).CardId);
    Assert.Equal(4, plan.Cards.Count);
    Assert.Equal([1, 2, 3, 4], plan.Cards.Select(card => card.Order));
    Assert.All(plan.Cards, card => Assert.Equal([release.Launch.Id], card.MilestoneIds));
    Assert.All(plan.Cards, card => Assert.True(card.Start <= card.Finish));
    Assert.Equal(release.Launch.Id, plan.Cards.Last().CardId);
    Assert.Equal(release.Scope.Owner.User.Id, plan.Cards.Single(card => card.CardId == release.Api.Id).UserId);
    Assert.Equal(0, plan.Assumptions.LearnedFromCards);
    Assert.Equal(6, plan.Assumptions.FocusHoursPerDay);
  }

  [Fact]
  public async Task Plan_StartsWithWhatIsInProgress()
  {
    var release = await ReleaseAsync("BASIC");
    var doing = release.Scope.Board.Lists.Single(list => list.TaskStatus == "IN_PROGRESS");

    await release.Scope.SendForAsync<BoardResponse>(
      HttpMethod.Patch,
      $"/cards/{release.Docs.Id}/move",
      new { listId = doing.Id });
    var plan = await release.Scope.SendForAsync<BoardPlanResponse>(HttpMethod.Get, "/plan");

    Assert.Equal(release.Docs.Id, plan.Cards.First().CardId);
    Assert.True(plan.Cards.First().IsInProgress);
  }

  [Fact]
  public async Task Actions_AreRankedAndMeasured()
  {
    var release = await ReleaseAsync(targetDays: 4);
    var member = await release.Scope.AddMemberAsync();
    await release.Scope.UpdateCardAsync(release.Docs.Id, new { assigneeIds = new[] { member.User.Id } });

    var actions = await release.Scope.SendForAsync<BoardMilestoneActionResponse[]>(
      HttpMethod.Get,
      $"/milestones/{release.Launch.Id}/actions");

    Assert.NotEmpty(actions);
    Assert.Equal(
      actions.OrderByDescending(NetChance),
      actions);
    Assert.All(actions, action => Assert.True(action.CommitDateAfter <= action.CommitDateBefore));
    Assert.Contains(actions, action => action.Kind == "ASSIGN_CARD" && action.UserId == member.User.Id);
    Assert.Contains(actions, action => action.Kind == "CUT_CARD" && action.Title is not null);
  }

  [Fact]
  public async Task Questions_PointAtTheUnsizedCardAndWeighAHint()
  {
    var release = await ReleaseAsync();

    var questions = await release.Scope.SendForAsync<BoardMilestoneQuestionsResponse>(
      HttpMethod.Post,
      $"/milestones/{release.Launch.Id}/questions",
      new
      {
        dependencyHints = new[]
        {
          new { cardId = release.Review.Id, dependsOnCardId = release.Api.Id, probability = 0.5 }
        }
      });

    Assert.True(questions.MarginDays > 0);
    Assert.Contains(questions.Questions, question => question.Kind == "SIZE" && question.CardId == release.Review.Id);
    Assert.All(questions.Questions, question => Assert.InRange(question.MarginDaysSaved, 0, questions.MarginDays));
    // A question changes nothing.
    Assert.Empty((await release.Scope.GetBoardAsync()).Card(release.Review.Id).Dependencies);
  }

  [Theory]
  [InlineData(1.5)]
  [InlineData(-0.1)]
  public async Task Questions_WithAnImpossibleProbability_AreRejected(double probability)
  {
    var release = await ReleaseAsync();

    var response = await release.Scope.SendAsync(
      HttpMethod.Post,
      $"/milestones/{release.Launch.Id}/questions",
      new
      {
        dependencyHints = new[]
        {
          new { cardId = release.Review.Id, dependsOnCardId = release.Api.Id, probability }
        }
      });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task WhatIf_AnnouncesExactlyWhatApplyingShows()
  {
    var release = await ReleaseAsync();

    var tried = await TryAsync(release.Scope, new { kind = "SET_SIZE", cardId = release.Api.Id, size = "XS" });
    var untouched = Assert.Single(await GetMilestonesAsync(release.Scope));
    await release.Scope.UpdateCardAsync(release.Api.Id, new { size = "XS" });
    var applied = Assert.Single(await GetMilestonesAsync(release.Scope));

    Assert.Equal(untouched.CommitDate, Assert.Single(tried.Before).CommitDate);
    Assert.Equal(applied.CommitDate, Assert.Single(tried.After).CommitDate);
    Assert.Equal(applied.OnTime, Assert.Single(tried.After).OnTime);
    Assert.True(applied.CommitDate < untouched.CommitDate);
  }

  [Fact]
  public async Task WhatIf_CombinesChanges()
  {
    var release = await ReleaseAsync();

    var tried = await TryAsync(
      release.Scope,
      new { kind = "REMOVE_DEPENDENCY", cardId = release.Launch.Id, dependsOnCardId = release.Api.Id },
      new { kind = "COMPLETE", cardId = release.Review.Id },
      new { kind = "SET_TARGET", cardId = release.Launch.Id, date = Today.AddDays(90) },
      new { kind = "ADD_PEOPLE", count = 1 });

    var after = Assert.Single(tried.After);
    Assert.Equal(2, after.OpenCardCount);
    Assert.Equal(Today.AddDays(90), after.TargetDate);
    Assert.True(after.ForecastDate < Assert.Single(tried.Before).ForecastDate);
  }

  [Fact]
  public async Task WhatIf_ThatLoopsOrMakesNoSense_IsRefused()
  {
    var release = await ReleaseAsync();
    var loop = await release.Scope.SendAsync(
      HttpMethod.Post,
      "/what-if",
      Changes(new { kind = "ADD_DEPENDENCY", cardId = release.Api.Id, dependsOnCardId = release.Launch.Id }));
    var nonsense = await release.Scope.SendAsync(
      HttpMethod.Post,
      "/what-if",
      Changes(new { kind = "SET_SIZE", cardId = release.Api.Id, size = "HUGE" }));
    var nothing = await release.Scope.SendAsync(HttpMethod.Post, "/what-if", new { });

    Assert.Equal(HttpStatusCode.Conflict, loop.StatusCode);
    Assert.Equal(HttpStatusCode.BadRequest, nonsense.StatusCode);
    Assert.Contains("Changes[0]", await nonsense.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    Assert.Equal(HttpStatusCode.BadRequest, nothing.StatusCode);
  }

  [Fact]
  public async Task Learning_ComesFromTheTimeCardsWereInProgress()
  {
    var scope = await BoardScope.CreateAsync(Api, "BASIC");
    var lists = scope.Board.Lists.ToDictionary(list => list.TaskStatus);

    for (var index = 0; index < 8; index++)
    {
      var card = await scope.AddCardAsync(
        lists["TODO"].Id,
        new { title = $"Card {index}", size = "S", labels = Backend });
      await scope.SendForAsync<BoardResponse>(
        HttpMethod.Patch,
        $"/cards/{card.Id}/move",
        new { listId = lists["IN_PROGRESS"].Id });
      await scope.SendForAsync<BoardResponse>(
        HttpMethod.Patch,
        $"/cards/{card.Id}/move",
        new { listId = lists["DONE"].Id });
    }

    // As if each had been started a full day before it was done.
    await Database.ExecuteAsync($"""
      UPDATE board_card_events SET occurred_at = occurred_at - interval '7 days'
      WHERE board_id = '{scope.Board.Id}' AND to_value = 'IN_PROGRESS'
      """);

    var learning = await scope.SendForAsync<BoardLearningResponse>(HttpMethod.Get, "/learning");

    // Thirty focus hours for a card of two: the team is slower than it says.
    Assert.Equal(8, learning.Team.CardCount);
    Assert.True(learning.Team.Median > 1.2);
    Assert.Equal("S", Assert.Single(learning.Sizes).Name);
    Assert.Equal("backend", Assert.Single(learning.Labels).Name);
    Assert.Equal(3, learning.Cards!.CardCount);
    Assert.Null(learning.Milestones);
  }

  [Fact]
  public async Task Snapshots_AreTakenOnceADayAndCheckedWhenTheMilestoneIsDone()
  {
    var release = await ReleaseAsync();
    await RecordAsync();
    await RecordAsync();

    var taken = await Database.ScalarAsync<long>(
      $"SELECT count(*) FROM board_forecast_snapshots WHERE board_id = '{release.Scope.Board.Id}'");
    var before = await release.Scope.SendForAsync<BoardLearningResponse>(HttpMethod.Get, "/learning");
    await release.Scope.UpdateCardAsync(release.Launch.Id, new { isCompleted = true });
    var after = await release.Scope.SendForAsync<BoardLearningResponse>(HttpMethod.Get, "/learning");

    Assert.Equal(1, taken);
    Assert.Null(before.Milestones);
    Assert.Equal(1, after.Milestones!.MilestoneCount);
    Assert.Equal(1, after.Milestones.SnapshotCount);
    // Done today, long before either date.
    Assert.Equal(1, after.Milestones.CommitMet);
    Assert.True(after.Milestones.MeanSlipDays < 0);
  }

  [Theory]
  [InlineData("GET", "/plan")]
  [InlineData("GET", "/learning")]
  public async Task Insights_OfAnotherWorkspace_AreNotFound(string method, string path)
  {
    var scope = await BoardScope.CreateAsync(Api);
    var outsider = await Api.RegisterAsync("Outsider");

    var response = await scope.SendAsync(new HttpMethod(method), path, null, outsider.AccessToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task Insights_OfAMilestoneThatIsNotOne_AreNotFound()
  {
    var release = await ReleaseAsync();

    var actions = await release.Scope.SendAsync(HttpMethod.Get, $"/milestones/{release.Api.Id}/actions");
    var questions = await release.Scope.SendAsync(
      HttpMethod.Post,
      $"/milestones/{release.Api.Id}/questions",
      new { });

    Assert.Equal(HttpStatusCode.NotFound, actions.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, questions.StatusCode);
  }

  // Each unit of value given up has to bring two points of chance.
  private static double NetChance(BoardMilestoneActionResponse action) =>
    Math.Round(action.OnTimeAfter - (0.02 * action.ValueLost), 9);

  private async Task RecordAsync()
  {
    await using var scope = Factory.Services.CreateAsyncScope();
    var recorder = scope.ServiceProvider.GetRequiredService<ForecastSnapshotRecorder>();
    Guid? last = Guid.Empty;

    while (last is { } after)
    {
      last = await recorder.RecordAsync(after, CancellationToken.None);
    }
  }

  private static object Changes(params object[] changes) => new { changes };

  private static Task<BoardWhatIfResponse> TryAsync(BoardScope scope, params object[] changes) =>
    scope.SendForAsync<BoardWhatIfResponse>(HttpMethod.Post, "/what-if", Changes(changes));

  private static Task<BoardMilestoneResponse[]> GetMilestonesAsync(BoardScope scope) =>
    scope.SendForAsync<BoardMilestoneResponse[]>(HttpMethod.Get, "/milestones");

  // A launch that needs an API, a review nobody has sized and docs, all on
  // one person.
  private async Task<Release> ReleaseAsync(string template = "CUSTOM", int targetDays = 30)
  {
    var scope = await BoardScope.CreateAsync(Api, template);
    var todo = scope.FirstList.Id;
    var owner = new[] { scope.Owner.User.Id };
    var api = await scope.AddCardAsync(todo, new { title = "API", size = "L", assigneeIds = owner });
    var review = await scope.AddCardAsync(todo, new { title = "Security review", assigneeIds = owner });
    var docs = await scope.AddCardAsync(todo, new { title = "Docs", size = "M", assigneeIds = owner });
    var launch = await scope.AddCardAsync(todo, new { title = "Launch", size = "XS", assigneeIds = owner });

    foreach (var dependency in new[] { api, review, docs })
    {
      await scope.AddDependencyAsync(launch.Id, dependency.Id);
    }

    await scope.UpdateCardAsync(launch.Id, new { milestoneDate = Today.AddDays(targetDays) });

    return new Release(scope, launch, api, review, docs);
  }

  private sealed record Release(
    BoardScope Scope,
    BoardCardResponse Launch,
    BoardCardResponse Api,
    BoardCardResponse Review,
    BoardCardResponse Docs);
}
