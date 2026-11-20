using System.Net;
using Coordina.Api.Tests.Support;

namespace Coordina.Api.Tests.Integration.Boards;

/// <summary>Marking cards as milestones and forecasting when they ship.</summary>
public sealed class BoardMilestoneEndpointsTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

  [Fact]
  public async Task MarkAndUnmark_SetsAndClearsTheMilestoneDate()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var card = await scope.AddCardAsync(scope.FirstList.Id, "Launch v1");
    var target = Today.AddDays(60);

    var marked = (await scope.UpdateCardAsync(card.Id, new { milestoneDate = target })).Card(card.Id);
    var listed = await GetMilestonesAsync(scope);
    var renamed = (await scope.UpdateCardAsync(card.Id, new { title = "Launch" })).Card(card.Id);
    var unmarked = (await scope.UpdateCardAsync(card.Id, new { clearMilestoneDate = true })).Card(card.Id);

    Assert.Equal(target, marked.MilestoneDate);
    Assert.Equal(target, Assert.Single(listed).TargetDate);
    Assert.Equal(target, renamed.MilestoneDate);
    Assert.Null(unmarked.MilestoneDate);
    Assert.Empty(await GetMilestonesAsync(scope));
  }

  [Fact]
  public async Task MilestoneDate_InThePast_IsRejected()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var card = await scope.AddCardAsync(scope.FirstList.Id, "Demo day");

    var response = await scope.SendAsync(
      HttpMethod.Patch,
      $"/cards/{card.Id}",
      new { milestoneDate = Today.AddDays(-1) });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Contains("MilestoneDate", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
  }

  [Fact]
  public async Task Forecast_CoversEverythingTheMilestoneDependsOn()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var todo = scope.FirstList.Id;
    var owner = new[] { scope.Owner.User.Id };
    var payment = await scope.AddCardAsync(todo, new { title = "Payment API", size = "L", assigneeIds = owner });
    var onboarding = await scope.AddCardAsync(todo, new { title = "Onboarding", assigneeIds = owner });
    await scope.AddCardAsync(todo, new { title = "Unrelated", size = "XXL" });
    var launch = await scope.AddCardAsync(todo, new { title = "Launch v1", size = "XS" });
    await scope.AddDependencyAsync(launch.Id, payment.Id);
    await scope.AddDependencyAsync(launch.Id, onboarding.Id);
    await scope.UpdateCardAsync(launch.Id, new { milestoneDate = Today.AddDays(90) });

    var milestone = Assert.Single(await GetMilestonesAsync(scope));

    Assert.Equal(launch.Id, milestone.CardId);
    Assert.Equal("ON_TRACK", milestone.Status);
    Assert.Equal(3, milestone.OpenCardCount);
    Assert.Equal(1, milestone.UnsizedCardCount);
    Assert.NotNull(milestone.ForecastDate);
    Assert.True(milestone.ForecastDate >= Today);
    Assert.True(milestone.CommitDate >= milestone.ForecastDate);
    Assert.Equal(milestone.ForecastDate!.Value.DayNumber - milestone.TargetDate.DayNumber, milestone.SlipDays);
    // One person does everything in a row, so both cards always set the date.
    Assert.Equal(
      ["Onboarding", "Payment API"],
      milestone.Drivers.Select(driver => driver.Title).Order(StringComparer.Ordinal));
    Assert.All(milestone.Drivers, driver => Assert.Equal(1.0, driver.Criticality));
    Assert.All(milestone.Drivers, driver => Assert.InRange(driver.Sensitivity, 0.0, 1.0));
    // The L card's wider range moves the sum more than the unsized one (an M).
    Assert.True(
      milestone.Drivers.Single(driver => driver.Title == "Payment API").Sensitivity
        > milestone.Drivers.Single(driver => driver.Title == "Onboarding").Sensitivity);
  }

  [Fact]
  public async Task Forecast_IsTheSameOnEveryLoad()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var todo = scope.FirstList.Id;
    var api = await scope.AddCardAsync(todo, new { title = "API", size = "XL" });
    var beta = await scope.AddCardAsync(todo, new { title = "Beta", size = "S" });
    await scope.AddDependencyAsync(beta.Id, api.Id);
    await scope.UpdateCardAsync(beta.Id, new { milestoneDate = Today.AddDays(30) });

    var first = Assert.Single(await GetMilestonesAsync(scope));
    var second = Assert.Single(await GetMilestonesAsync(scope));

    Assert.Equal(first.ForecastDate, second.ForecastDate);
    Assert.Equal(first.CommitDate, second.CommitDate);
    Assert.Equal(first.Drivers, second.Drivers);
  }

  [Fact]
  public async Task Milestone_DueBeforeItsWork_IsLate()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var todo = scope.FirstList.Id;
    var store = await scope.AddCardAsync(todo, new { title = "Store review", size = "XL" });
    var launch = await scope.AddCardAsync(todo, new { title = "Launch", size = "XS" });
    await scope.AddDependencyAsync(launch.Id, store.Id);
    await scope.UpdateCardAsync(launch.Id, new { milestoneDate = Today });

    var milestone = Assert.Single(await GetMilestonesAsync(scope));

    Assert.Equal("LATE", milestone.Status);
    Assert.True(milestone.SlipDays > 0);
    Assert.Equal("Store review", Assert.Single(milestone.Drivers).Title);
  }

  [Fact]
  public async Task CompletedMilestone_HasNoForecast()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var card = await scope.AddCardAsync(scope.FirstList.Id, "Shipped");
    await scope.UpdateCardAsync(card.Id, new { milestoneDate = Today.AddDays(7) });
    await scope.UpdateCardAsync(card.Id, new { isCompleted = true });

    var milestone = Assert.Single(await GetMilestonesAsync(scope));

    Assert.Equal("COMPLETED", milestone.Status);
    Assert.Null(milestone.ForecastDate);
    Assert.Null(milestone.CommitDate);
    Assert.Null(milestone.SlipDays);
    Assert.Empty(milestone.Drivers);
  }

  [Fact]
  public async Task Milestones_AreSortedByTargetDate()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var later = await scope.AddCardAsync(scope.FirstList.Id, "Launch");
    var sooner = await scope.AddCardAsync(scope.FirstList.Id, "Beta");
    await scope.UpdateCardAsync(later.Id, new { milestoneDate = Today.AddDays(40) });
    await scope.UpdateCardAsync(sooner.Id, new { milestoneDate = Today.AddDays(10) });

    var milestones = await GetMilestonesAsync(scope);

    Assert.Equal(["Beta", "Launch"], milestones.Select(milestone => milestone.Title));
  }

  [Fact]
  public async Task Milestones_OfAnotherWorkspace_AreNotFound()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var outsider = await Api.RegisterAsync();

    var response = await scope.SendAsync(HttpMethod.Get, "/milestones", accessToken: outsider.AccessToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  private static Task<BoardMilestoneResponse[]> GetMilestonesAsync(BoardScope scope) =>
    scope.SendForAsync<BoardMilestoneResponse[]>(HttpMethod.Get, "/milestones");
}
