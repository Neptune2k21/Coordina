using Coordina.Api.Modules.Boards.Application.Planning;
using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Unit.Tests.Boards;

public sealed class BoardPlanTests
{
  // Wednesday 18 November 2026.
  private static readonly DateTimeOffset Now = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);
  private static readonly DateOnly Today = new(2026, 11, 18);

  private readonly Guid _ada = Guid.NewGuid();
  private readonly Guid _grace = Guid.NewGuid();

  [Theory]
  [InlineData(0, 6)]
  [InlineData(2, 18)]
  [InlineData(5, 24)]
  [InlineData(-1, 0)]
  [InlineData(-2, -6)]
  [InlineData(-7, -24)]
  public void TargetHours_CountWorkingDaysBothWays(int days, double hours)
  {
    Assert.Equal(hours, BoardPlans.TargetHours(Today, Today.AddDays(days)));
  }

  [Fact]
  public void Build_GivesTheEngineOpenMilestonesSoonestFirst()
  {
    var later = PlanningBoards.Card("Later") with { MilestoneDate = Today.AddDays(20) };
    var sooner = PlanningBoards.Card("Sooner") with
    {
      MilestoneDate = Today.AddDays(5),
      Priority = BoardCardPriority.High
    };
    var done = PlanningBoards.Card("Done") with { MilestoneDate = Today, IsCompleted = true };

    var plan = Build(PlanningBoards.Board(later, sooner, done));

    Assert.Equal([sooner.Id, later.Id], plan.Graph.Milestones.Select(milestone => milestone.Node));
    Assert.Equal([4, 1], plan.Graph.Milestones.Select(milestone => milestone.Weight));
    Assert.NotNull(plan.FindMilestone(done.Id));
    Assert.Null(plan.FindMilestone(Guid.NewGuid()));
  }

  [Fact]
  public void Build_NamesWhoDidACompletedCardNobodyWasAssignedTo()
  {
    var card = PlanningBoards.Card("Done") with
    {
      IsCompleted = true,
      CompletedAt = Now.AddDays(-2),
      CompletedBy = new ProjectBoardCardUser(_grace, null, null)
    };

    var plan = Build(PlanningBoards.Board(card, PlanningBoards.Card("Open", _ada)), [_grace]);

    Assert.Equal(new[] { _ada, _grace }.Order(), plan.People);
    Assert.Contains(
      plan.Graph.Assignees,
      assignee => assignee.Node == card.Id && assignee.Person == plan.PersonOf(_grace));
  }

  [Fact]
  public void Build_LearnsFromCompletedCardsThatWereInProgress()
  {
    var worked = PlanningBoards.Card("Worked", _ada) with { IsCompleted = true, CompletedAt = Now.AddDays(-1) };
    var moved = PlanningBoards.Card("Moved", _ada) with { IsCompleted = true, CompletedAt = Now.AddDays(-1) };
    var board = PlanningBoards.Board(worked, moved);

    var plan = BoardPlans.Build(
      board,
      [
        Event(1, worked.Id, BoardTaskStatus.InProgress, Now.AddDays(-1).AddHours(-4)),
        Event(2, worked.Id, BoardTaskStatus.Done, Now.AddDays(-1))
      ],
      [],
      Now);

    var learned = Assert.Single(plan.Graph.History);
    Assert.Equal(worked.Id, learned.Node);
    Assert.Equal(4, learned.Hours, 6);
    Assert.Equal(1, learned.AgeDays, 6);
  }

  [Fact]
  public void Build_CountsTheWorkFoundOnceAMilestoneStarted()
  {
    var first = PlanningBoards.Card("First") with { IsCompleted = true, CompletedAt = Now };
    var found = PlanningBoards.Card("Found") with { CreatedAt = Now.AddDays(-1) };
    var unrelated = PlanningBoards.Card("Unrelated") with { CreatedAt = Now.AddDays(-1) };
    var launch = (PlanningBoards.Card("Launch") with { MilestoneDate = Today.AddDays(9) })
      .DependingOn(first, found);

    var plan = BoardPlans.Build(
      PlanningBoards.Board(first, found, unrelated, launch),
      [Event(1, first.Id, BoardTaskStatus.InProgress, Now.AddDays(-3))],
      [],
      Now);

    Assert.Equal(1, plan.Graph.Growth.Discovered);
    Assert.Equal(1, plan.Graph.Growth.Finished);
  }

  [Fact]
  public void CardTokens_IgnoreCaseAccentsAndPlurals()
  {
    var left = PlanningBoards.Card("Détails des FACTURES");
    var right = PlanningBoards.Card("facture : detail");

    var tokens = CardTokens.Of([left, right]).ToLookup(token => token.Node, token => token.Token);

    Assert.Equal(tokens[right.Id].Order(), tokens[left.Id].Distinct().Intersect(tokens[right.Id]).Order());
  }

  private static BoardPlan Build(ProjectBoard board, params Guid[] newcomers) =>
    BoardPlans.Build(board, [], newcomers, Now);

  private static BoardCardEvent Event(long id, Guid cardId, BoardTaskStatus status, DateTimeOffset at) =>
    new(
      id,
      cardId,
      BoardCardEventKind.StatusChanged,
      null,
      status is BoardTaskStatus.InProgress ? "IN_PROGRESS" : "DONE",
      at);
}
