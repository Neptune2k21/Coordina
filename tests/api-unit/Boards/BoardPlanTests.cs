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

  private static BoardPlan Build(ProjectBoard board, params Guid[] newcomers) =>
    BoardPlans.Build(board, newcomers, Now);
}
