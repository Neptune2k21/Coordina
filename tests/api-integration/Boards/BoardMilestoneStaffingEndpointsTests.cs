using System.Net;
using Coordina.Api.Integration.Tests.Support;

namespace Coordina.Api.Integration.Tests.Boards;

/// <summary>Proposing, measuring and applying who takes which card of a milestone.</summary>
public sealed class BoardMilestoneStaffingEndpointsTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
  private static readonly string[] Backend = ["backend"];

  [Fact]
  public async Task Proposal_KeepsCardsThatHaveAssignees()
  {
    var release = await OverloadedAsync();

    var proposal = await ProposeAsync(release.Scope, release.Launch.Id, new { });

    Assert.Empty(proposal.Changes);
    Assert.Equal(proposal.CommitDateBefore, proposal.CommitDateAfter);
    Assert.Equal(
      new[] { release.Ada.User.Id, release.Grace.User.Id }.Order(StringComparer.Ordinal),
      proposal.People.Select(person => person.UserId).Order(StringComparer.Ordinal));
  }

  [Fact]
  public async Task Proposal_AllowedToReassign_TakesWorkOffTheBusiestPerson()
  {
    var release = await OverloadedAsync();

    var proposal = await ProposeAsync(release.Scope, release.Launch.Id, new { allowReassignment = true });

    // Grace takes the side work or the start of the chain; either way it no
    // longer waits for Ada.
    var change = Assert.Single(proposal.Changes);
    Assert.Equal(release.Grace.User.Id, change.To.UserId);
    Assert.Equal([release.Ada.User.Id], change.From.Select(person => person.UserId));
    Assert.True(change.StartAfter <= change.StartBefore);
    Assert.True(proposal.CommitDateAfter < proposal.CommitDateBefore);
    Assert.True(proposal.ForecastDateAfter <= proposal.ForecastDateBefore);
  }

  [Fact]
  public async Task Apply_MakesThePersonTheOnlyAssigneeAndTheMilestoneShowsTheDatesAfter()
  {
    var release = await OverloadedAsync();
    var proposal = await ProposeAsync(release.Scope, release.Launch.Id, new { allowReassignment = true });
    var change = Assert.Single(proposal.Changes);

    var board = await release.Scope.SendForAsync<BoardResponse>(
      HttpMethod.Post,
      $"/milestones/{release.Launch.Id}/staffing",
      new { changes = new[] { new { cardId = change.CardId, userId = change.To.UserId } } });
    var milestone = Assert.Single(
      await release.Scope.SendForAsync<BoardMilestoneResponse[]>(HttpMethod.Get, "/milestones"));

    Assert.Equal([release.Grace.User.Id], board.Card(change.CardId).Assignees.Select(person => person.UserId));
    Assert.Equal(proposal.ForecastDateAfter, milestone.ForecastDate);
    Assert.Equal(proposal.CommitDateAfter, milestone.CommitDate);
  }

  [Fact]
  public async Task Apply_Twice_LeavesEachCardWithOneAssignee()
  {
    var release = await OverloadedAsync();
    var unassigned = await release.Scope.AddCardAsync(release.Scope.FirstList.Id, "Changelog");
    var changes = new[] { release.Side.Id, unassigned.Id }
      .Select(cardId => new { cardId, userId = release.Grace.User.Id })
      .ToArray();

    for (var attempt = 0; attempt < 2; attempt++)
    {
      await release.Scope.SendForAsync<BoardResponse>(
        HttpMethod.Post, $"/milestones/{release.Launch.Id}/staffing", new { changes });
    }

    var board = await release.Scope.GetBoardAsync();
    Assert.All(
      [board.Card(release.Side.Id), board.Card(unassigned.Id)],
      card => Assert.Equal(release.Grace.User.Id, Assert.Single(card.Assignees).UserId));
  }

  [Fact]
  public async Task Evaluation_MeasuresTheChosenChangesWithoutApplyingThem()
  {
    var release = await OverloadedAsync();

    var evaluation = await release.Scope.SendForAsync<BoardMilestoneStaffingResponse>(
      HttpMethod.Post,
      $"/milestones/{release.Launch.Id}/staffing-evaluation",
      new { changes = new[] { new { cardId = release.Side.Id, userId = release.Grace.User.Id } } });
    var board = await release.Scope.GetBoardAsync();

    Assert.Equal(["Billing emails"], evaluation.Changes.Select(change => change.Title));
    Assert.True(evaluation.CommitDateAfter < evaluation.CommitDateBefore);
    Assert.Equal([release.Ada.User.Id], board.Card(release.Side.Id).Assignees.Select(person => person.UserId));
  }

  [Fact]
  public async Task Proposal_OnABoardNobodyIsAssignedOn_SpreadsTheWorkOverTheWorkspace()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var member = await scope.AddMemberAsync();
    var launch = await scope.AddCardAsync(scope.FirstList.Id, new { title = "Launch", size = "XS" });

    foreach (var title in new[] { "Pricing page", "Signup", "Onboarding", "Invoices" })
    {
      var card = await scope.AddCardAsync(scope.FirstList.Id, new { title, size = "M" });
      await scope.AddDependencyAsync(launch.Id, card.Id);
    }

    await scope.UpdateCardAsync(launch.Id, new { milestoneDate = Today.AddDays(30) });

    var proposal = await ProposeAsync(scope, launch.Id, new { });

    Assert.Equal(2, proposal.People.Count);
    Assert.Equal(
      new[] { scope.Owner.User.Id, member.User.Id }.Order(StringComparer.Ordinal),
      proposal.Changes.Select(change => change.To.UserId).Distinct().Order(StringComparer.Ordinal));
    Assert.True(proposal.CommitDateAfter < proposal.CommitDateBefore);
  }

  [Fact]
  public async Task Proposal_PrefersSomeoneWhoHasCompletedCardsWithTheSameLabel()
  {
    var release = await OverloadedAsync();
    var linus = await release.Scope.AddMemberAsync();
    var done = await release.Scope.AddCardAsync(
      release.Scope.FirstList.Id,
      new { title = "Webhook retries", size = "S", labels = Backend, assigneeIds = new[] { linus.User.Id } });
    await release.Scope.UpdateCardAsync(done.Id, new { isCompleted = true });
    foreach (var card in new[] { release.First, release.Second, release.Side })
    {
      await release.Scope.UpdateCardAsync(card.Id, new { labels = Backend });
    }

    var proposal = await ProposeAsync(
      release.Scope,
      release.Launch.Id,
      new { people = new[] { release.Ada.User.Id, release.Grace.User.Id, linus.User.Id }, allowReassignment = true });

    var change = Assert.Single(proposal.Changes);
    Assert.Equal(linus.User.Id, change.To.UserId);
    Assert.Equal(Backend, change.MatchingLabels);
  }

  [Fact]
  public async Task Proposal_WithSomeoneOutsideTheWorkspace_IsRejected()
  {
    var release = await OverloadedAsync();
    var outsider = await Api.RegisterAsync("Outsider");

    var response = await release.Scope.SendAsync(
      HttpMethod.Post,
      $"/milestones/{release.Launch.Id}/staffing-proposal",
      new { people = new[] { outsider.User.Id } });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Apply_WithoutChangesOrToADoneCard_IsRejected()
  {
    var release = await OverloadedAsync();
    var done = await release.Scope.AddCardAsync(release.Scope.FirstList.Id, "Kickoff");
    await release.Scope.UpdateCardAsync(done.Id, new { isCompleted = true });

    var empty = await release.Scope.SendAsync(
      HttpMethod.Post,
      $"/milestones/{release.Launch.Id}/staffing",
      new { changes = Array.Empty<object>() });
    var toDone = await release.Scope.SendAsync(
      HttpMethod.Post,
      $"/milestones/{release.Launch.Id}/staffing",
      new { changes = new[] { new { cardId = done.Id, userId = release.Grace.User.Id } } });

    Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    Assert.Equal(HttpStatusCode.BadRequest, toDone.StatusCode);
  }

  [Fact]
  public async Task Apply_FromOutsideTheWorkspace_IsNotFound()
  {
    var release = await OverloadedAsync();
    var outsider = await Api.RegisterAsync("Outsider");

    var response = await release.Scope.SendAsync(
      HttpMethod.Post,
      $"/milestones/{release.Launch.Id}/staffing",
      new { changes = new[] { new { cardId = release.Side.Id, userId = release.Grace.User.Id } } },
      outsider.AccessToken);
    var board = await release.Scope.GetBoardAsync();

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal([release.Ada.User.Id], board.Card(release.Side.Id).Assignees.Select(person => person.UserId));
  }

  [Fact]
  public async Task Apply_OnACompletedProject_IsAConflictButTheProposalStillReads()
  {
    var release = await OverloadedAsync();
    var scope = release.Scope;
    await Api.UpdateProjectAsync(scope.Owner.AccessToken, scope.Workspace.Id, scope.Project.Id, new { status = "COMPLETED" });

    var proposal = await scope.SendAsync(
      HttpMethod.Post,
      $"/milestones/{release.Launch.Id}/staffing-proposal",
      new { allowReassignment = true });
    var apply = await scope.SendAsync(
      HttpMethod.Post,
      $"/milestones/{release.Launch.Id}/staffing",
      new { changes = new[] { new { cardId = release.Side.Id, userId = release.Grace.User.Id } } });

    Assert.Equal(HttpStatusCode.OK, proposal.StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, apply.StatusCode);
  }

  [Fact]
  public async Task Proposal_ForACardThatIsNotAMilestone_IsNotFound()
  {
    var release = await OverloadedAsync();

    var response = await release.Scope.SendAsync(
      HttpMethod.Post,
      $"/milestones/{release.Side.Id}/staffing-proposal",
      new { });

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task Proposal_OfAnotherWorkspace_IsNotFound()
  {
    var release = await OverloadedAsync();
    var outsider = await Api.RegisterAsync("Outsider");

    var response = await release.Scope.SendAsync(
      HttpMethod.Post,
      $"/milestones/{release.Launch.Id}/staffing-proposal",
      new { },
      outsider.AccessToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  // Ada (the owner) has a six-day chain to the launch and three days of side
  // work, about nine days in all; Grace has a small card and time to spare,
  // so taking work off Ada saves about three days.
  private async Task<Release> OverloadedAsync()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var grace = await scope.AddMemberAsync();
    var todo = scope.FirstList.Id;
    var ada = new[] { scope.Owner.User.Id };
    var first = await scope.AddCardAsync(todo, new { title = "Payments API", size = "L", assigneeIds = ada });
    var second = await scope.AddCardAsync(todo, new { title = "Checkout", size = "L", assigneeIds = ada });
    var side = await scope.AddCardAsync(todo, new { title = "Billing emails", size = "L", assigneeIds = ada });
    var small = await scope.AddCardAsync(
      todo,
      new { title = "Receipt copy", size = "S", assigneeIds = new[] { grace.User.Id } });
    var launch = await scope.AddCardAsync(todo, new { title = "Launch", size = "XS" });
    await scope.AddDependencyAsync(second.Id, first.Id);
    await scope.AddDependencyAsync(launch.Id, second.Id);
    await scope.AddDependencyAsync(launch.Id, side.Id);
    await scope.AddDependencyAsync(launch.Id, small.Id);
    await scope.UpdateCardAsync(launch.Id, new { milestoneDate = Today.AddDays(30) });

    return new Release(scope, scope.Owner, grace, launch, first, second, side);
  }

  private static Task<BoardMilestoneStaffingResponse> ProposeAsync(
    BoardScope scope,
    string milestoneId,
    object request) =>
    scope.SendForAsync<BoardMilestoneStaffingResponse>(
      HttpMethod.Post,
      $"/milestones/{milestoneId}/staffing-proposal",
      request);

  private sealed record Release(
    BoardScope Scope,
    AuthResponse Ada,
    AuthResponse Grace,
    BoardCardResponse Launch,
    BoardCardResponse First,
    BoardCardResponse Second,
    BoardCardResponse Side);
}
