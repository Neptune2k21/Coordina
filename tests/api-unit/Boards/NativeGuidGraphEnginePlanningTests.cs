using Coordina.Api.Modules.Boards.Application.Graph;

namespace Coordina.Api.Unit.Tests.Boards;

/// <summary>The planning calls across the C ABI; the native tests cover the algorithms.</summary>
public sealed class NativeGuidGraphEnginePlanningTests
{
  private static readonly GraphSimulation Simulation = new(7, 500);

  private readonly NativeGuidGraphEngine _graphEngine = new();

  private readonly Guid _api = Guid.NewGuid();
  private readonly Guid _onboarding = Guid.NewGuid();
  private readonly Guid _launch = Guid.NewGuid();

  [Fact]
  public void Forecast_IsReproducibleAndPutsTheMilestoneLast()
  {
    var board = Board([new(_api, 0), new(_onboarding, 1)], 2);

    var forecast = _graphEngine.Forecast(board, Simulation);
    var again = _graphEngine.Forecast(board, Simulation);
    var milestone = Assert.Single(forecast.Milestones);

    Assert.Equal(milestone.P50Days, Assert.Single(again.Milestones).P50Days);
    Assert.Equal(milestone.Cards, Assert.Single(again.Milestones).Cards);
    Assert.Equal(_launch, milestone.Node);
    Assert.True(milestone.P10Days < milestone.P50Days);
    Assert.True(milestone.P50Days < milestone.P85Days);
    Assert.True(milestone.P85Days < milestone.P95Days);
    Assert.InRange(milestone.OnTime, 0, 1);
    Assert.True(milestone.ChainDays > 0);
    Assert.Equal(3, milestone.CardCount);
    Assert.Equal(1, milestone.UnsizedCount);
    Assert.Equal(_launch, milestone.Cards[0].Node);
    Assert.Equal(1, milestone.Cards[0].Criticality);
    Assert.All(milestone.Cards, card => Assert.InRange(card.Sensitivity, -1, 1));
    Assert.Equal(3, forecast.Plan.Count);
    Assert.Equal(0, forecast.Plan.Single(card => card.Node == _api).Person);
    Assert.Equal(1, forecast.Plan.Single(card => card.Node == _onboarding).Person);
  }

  [Fact]
  public void Forecast_WithNobodyAssigned_NamesNobody()
  {
    var forecast = _graphEngine.Forecast(Board([], 2), Simulation);

    Assert.All(forecast.Plan, card => Assert.Null(card.Person));
  }

  [Fact]
  public void Forecast_LearnsFromTheHistory()
  {
    var done = Enumerable.Range(0, 40).Select(_ => Guid.NewGuid()).ToArray();
    var board = Board([], 1);
    var learned = board with
    {
      Nodes = [.. board.Nodes, .. done],
      WorkItems = [.. board.WorkItems, .. done.Select(node => Item(node, 9, GraphSize.M) with { IsCompleted = true })],
      History = [.. done.Select(node => new GraphFinishedCard<Guid>(node, 14, 3))]
    };

    var before = Assert.Single(_graphEngine.Forecast(board, Simulation).Milestones);
    var after = Assert.Single(_graphEngine.Forecast(learned, Simulation).Milestones);

    // The team takes more than twice what an M says.
    Assert.True(after.P50Days > before.P50Days * 1.5);
  }

  [Fact]
  public void Forecast_WithLoopedCards_SaysWhy()
  {
    var board = Board([], 1);
    var looped = board with { Edges = [.. board.Edges, new GraphEdge<Guid>(_api, _launch)] };

    var error = Assert.Throws<GraphEngineException>(() => _graphEngine.Forecast(looped, Simulation));

    Assert.Equal("forecast", error.Operation);
    Assert.Contains("loop", error.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void Forecast_WithANumberThatIsNotOne_SaysWhy()
  {
    var board = Board([], 1) with { Milestones = [new GraphMilestone<Guid>(_launch, double.NaN)] };

    var error = Assert.Throws<GraphEngineException>(() => _graphEngine.Forecast(board, Simulation));

    Assert.Contains("finite", error.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void ProposeScope_CutsTheLeastValuableCardThatMakesTheDate()
  {
    var board = Board([], 1, 40) with
    {
      WorkItems =
      [
        Item(_api, 0, GraphSize.L) with { Priority = 1 },
        Item(_onboarding, 1, GraphSize.L) with { Priority = 3 },
        Item(_launch, 2, GraphSize.Xs)
      ]
    };
    var options = new GraphScopeOptions<Guid>(_launch, Simulation, 0.85, 128, 50, TimeSpan.Zero);

    var cut = _graphEngine.ProposeScope(board, [], options);
    var kept = _graphEngine.ProposeScope(board, [_api], options);
    var chosen = _graphEngine.EvaluateScope(board, [_onboarding], options);

    Assert.Equal([_api], cut.Cuts);
    Assert.Equal([new GraphDroppedCard<Guid>(_api, 0)], cut.Dropped);
    Assert.True(cut.OnTimeBefore < 0.85);
    Assert.True(cut.OnTimeAfter >= 0.85);
    Assert.True(cut.P50Days <= cut.P85Days);
    Assert.Equal([_onboarding], kept.Cuts);
    Assert.Equal([_onboarding], chosen.Cuts);
  }

  [Fact]
  public void ProposeScope_WithACardThatIsNotAMilestone_Throws()
  {
    var error = Assert.Throws<GraphEngineException>(() => _graphEngine.ProposeScope(
      Board([], 1),
      [],
      new GraphScopeOptions<Guid>(_api, Simulation, 0.85, 128, 50, TimeSpan.Zero)));

    Assert.Equal("propose_scope", error.Operation);
  }

  [Fact]
  public void ProposeStaffing_GivesOneCardToEachIdlePerson()
  {
    var board = Board([], 2);
    var options = new GraphStaffingOptions<Guid>(_launch, Simulation, false, 8, 2000, TimeSpan.Zero);

    // Nobody is assigned, so one person nobody in particular does both cards.
    var plan = _graphEngine.ProposeStaffing(board, [0, 1], [], options);
    var chosen = _graphEngine.EvaluateStaffing(
      board,
      plan.Changes.Select(change => new GraphAssignee<Guid>(change.Node, change.Person)).ToArray(),
      options);

    Assert.Equal([0, 1], plan.Changes.Select(change => change.Person).Order());
    Assert.All(plan.Changes, change => Assert.Equal(0, change.StartDaysAfter));
    Assert.True(plan.P85DaysAfter < plan.P85DaysBefore);
    Assert.Equal(plan.P85DaysAfter, chosen.P85DaysAfter);
    Assert.Equal(0, plan.OthersLateDaysAfter);
  }

  [Fact]
  public void ProposeStaffing_WithAReceiverOutsideTheTeam_Throws()
  {
    var error = Assert.Throws<GraphEngineException>(() => _graphEngine.ProposeStaffing(
      Board([], 2),
      [3],
      [],
      new GraphStaffingOptions<Guid>(_launch, Simulation, false, 8, 100, TimeSpan.Zero)));

    Assert.Contains("person", error.Message, StringComparison.Ordinal);
  }

  private GraphBoard<Guid> Board(GraphAssignee<Guid>[] assignees, int people, double targetHours = 60) =>
    new(
      [_api, _onboarding, _launch],
      [new(_launch, _api), new(_launch, _onboarding)],
      [Item(_api, 0, GraphSize.L), Item(_onboarding, 1), Item(_launch, 2, GraphSize.Xs)],
      assignees,
      [],
      [new GraphMilestone<Guid>(_launch, targetHours)],
      [],
      GraphScopeGrowth.None,
      people);

  private static GraphWorkItem<Guid> Item(Guid node, int position, GraphSize size = GraphSize.None) =>
    new(node, false, 0, GraphWorkItem<Guid>.NoDueDate, 0, 0, position, size);
}
