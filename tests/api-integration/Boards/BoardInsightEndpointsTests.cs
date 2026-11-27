using System.Net;
using Coordina.Api.Integration.Tests.Support;
using Coordina.Api.Modules.Boards.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Coordina.Api.Integration.Tests.Boards;

/// <summary>What the engine has learned from a board, and the forecasts it records.</summary>
public sealed class BoardInsightEndpointsTests(ApiTestApplicationFactory factory) : ApiTest(factory)
{
  private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
  private static readonly string[] Backend = ["backend"];

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
  [InlineData("GET", "/learning")]
  [InlineData("GET", "/events")]
  public async Task Insights_OfAnotherWorkspace_AreNotFound(string method, string path)
  {
    var scope = await BoardScope.CreateAsync(Api);
    var outsider = await Api.RegisterAsync("Outsider");

    var response = await scope.SendAsync(new HttpMethod(method), path, null, outsider.AccessToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

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
