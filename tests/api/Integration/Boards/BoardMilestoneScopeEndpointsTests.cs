using System.Net;
using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Tests.Support;

namespace Coordina.Api.Tests.Integration.Boards;

/// <summary>Proposing, measuring and applying what to take out of a milestone.</summary>
public sealed class BoardMilestoneScopeEndpointsTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

  [Fact]
  public async Task Proposal_CutsTheLeastValuableFeatureAndWhatOnlyItNeeds()
  {
    var release = await ReleaseAsync();

    var proposal = await ProposeAsync(release.Scope, release.Launch.Id);

    var cut = Assert.Single(proposal.Cuts);
    Assert.Equal("Export PDF", cut.Title);
    Assert.Equal(["PDF templates"], cut.AlsoRemoved.Select(card => card.Title));
    Assert.InRange(proposal.OnTimeBefore, 0, 0.05);
    Assert.Equal(1, proposal.OnTimeAfter);
    Assert.True(proposal.CommitDate <= proposal.TargetDate);
    Assert.True(proposal.ForecastDate <= proposal.CommitDate);
  }

  [Fact]
  public async Task Proposal_KeepsTheCardsAskedFor()
  {
    var release = await ReleaseAsync();

    var proposal = await ProposeAsync(release.Scope, release.Launch.Id, keep: [release.Pdf.Id]);

    Assert.Equal(["SSO"], proposal.Cuts.Select(cut => cut.Title));
    Assert.Equal(1, proposal.OnTimeAfter);
  }

  [Fact]
  public async Task Evaluation_MeasuresTheChosenCardsWithoutChangingThem()
  {
    var release = await ReleaseAsync();

    var evaluation = await release.Scope.SendForAsync<BoardMilestoneScopeResponse>(
      HttpMethod.Post,
      $"/milestones/{release.Launch.Id}/scope-evaluation",
      new { cardIds = new[] { release.Docs.Id } });
    var board = await release.Scope.GetBoardAsync();

    Assert.Equal(["Docs"], evaluation.Cuts.Select(cut => cut.Title));
    Assert.True(evaluation.OnTimeAfter < 0.85);
    Assert.Equal(3, board.Card(release.Launch.Id).Dependencies.Count);
  }

  [Fact]
  public async Task Cut_DropsTheMilestoneDependenciesAndKeepsTheCards()
  {
    var release = await ReleaseAsync();

    var board = await release.Scope.SendForAsync<BoardResponse>(
      HttpMethod.Post,
      $"/milestones/{release.Launch.Id}/scope-cut",
      new { cardIds = new[] { release.Pdf.Id } });
    var milestone = Assert.Single(
      await release.Scope.SendForAsync<BoardMilestoneResponse[]>(HttpMethod.Get, "/milestones"));

    Assert.Equal(
      ["Docs", "SSO"],
      board.Card(release.Launch.Id).Dependencies.Select(dependency => dependency.Title).Order(StringComparer.Ordinal));
    Assert.Single(board.Card(release.Pdf.Id).Dependencies);
    Assert.Equal(3, milestone.OpenCardCount);
    Assert.Equal("ON_TRACK", milestone.Status);
  }

  [Fact]
  public async Task Cut_WithoutCards_IsRejected()
  {
    var release = await ReleaseAsync();

    var response = await release.Scope.SendAsync(
      HttpMethod.Post,
      $"/milestones/{release.Launch.Id}/scope-cut",
      new { cardIds = Array.Empty<string>() });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Proposal_ForADoneMilestone_IsRejected()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var launch = await scope.AddCardAsync(scope.FirstList.Id, "Launch");
    await scope.UpdateCardAsync(launch.Id, new { milestoneDate = Today.AddDays(10), isCompleted = true });

    var response = await scope.SendAsync(
      HttpMethod.Post,
      $"/milestones/{launch.Id}/scope-proposal",
      new { });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Proposal_ForACardThatIsNotAMilestone_IsNotFound()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var card = await scope.AddCardAsync(scope.FirstList.Id, "Plain card");

    var response = await scope.SendAsync(HttpMethod.Post, $"/milestones/{card.Id}/scope-proposal", new { });

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task Proposal_OfAnotherWorkspace_IsNotFound()
  {
    var release = await ReleaseAsync();
    var outsider = await Api.RegisterAsync("Outsider");

    var response = await release.Scope.SendAsync(
      HttpMethod.Post,
      $"/milestones/{release.Launch.Id}/scope-proposal",
      new { },
      outsider.AccessToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  // One person works through a launch due in five working days (30 focus
  // hours). It needs two large features, a small one, and docs; the PDF export
  // needs templates nothing else needs. Without the PDF export at most 28
  // hours are left, while all of it takes at least 26.25 and usually about 41.
  private async Task<Release> ReleaseAsync()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var todo = scope.FirstList.Id;
    var templates = await scope.AddCardAsync(todo, new { title = "PDF templates", size = "S" });
    var pdf = await scope.AddCardAsync(todo, new { title = "Export PDF", size = "L", priority = "LOW" });
    var sso = await scope.AddCardAsync(todo, new { title = "SSO", size = "L", priority = "HIGH" });
    var docs = await scope.AddCardAsync(todo, new { title = "Docs", size = "S", priority = "MEDIUM" });
    var launch = await scope.AddCardAsync(todo, new { title = "Launch", size = "XS" });
    await scope.AddDependencyAsync(pdf.Id, templates.Id);
    await scope.AddDependencyAsync(launch.Id, pdf.Id);
    await scope.AddDependencyAsync(launch.Id, sso.Id);
    await scope.AddDependencyAsync(launch.Id, docs.Id);
    await scope.UpdateCardAsync(launch.Id, new { milestoneDate = WorkingDaysAway(5) });

    return new Release(scope, launch, pdf, docs);
  }

  private static DateOnly WorkingDaysAway(int workingDays)
  {
    var target = Today.AddDays(1);

    while (WorkingDays.Until(Today, target) < workingDays)
    {
      target = target.AddDays(1);
    }

    return target;
  }

  private static Task<BoardMilestoneScopeResponse> ProposeAsync(
    BoardScope scope,
    string milestoneId,
    string[]? keep = null) =>
    scope.SendForAsync<BoardMilestoneScopeResponse>(
      HttpMethod.Post,
      $"/milestones/{milestoneId}/scope-proposal",
      new { keep });

  private sealed record Release(
    BoardScope Scope,
    BoardCardResponse Launch,
    BoardCardResponse Pdf,
    BoardCardResponse Docs);
}
