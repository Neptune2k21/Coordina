using Coordina.Api.Modules.Boards.Application.Graph;

namespace Coordina.Api.Unit.Tests.Boards;

public sealed class NativeGuidGraphEngineTests
{
  private readonly NativeGuidGraphEngine _graphEngine = new();

  [Fact]
  public void EvaluateEdge_WhenTargetCanReachSource_ReturnsCycle()
  {
    var backlog = Guid.NewGuid();
    var ready = Guid.NewGuid();
    var done = Guid.NewGuid();

    var result = _graphEngine.EvaluateEdge(
      [backlog, ready, done],
      [
        new GraphEdge<Guid>(backlog, ready),
        new GraphEdge<Guid>(ready, done)
      ],
      done,
      backlog);

    Assert.False(result.CanAdd);
    Assert.Equal(GraphEdgeStatus.Cycle, result.Status);
  }

  [Fact]
  public void EvaluateEdge_WhenEdgeAlreadyExists_ReturnsDuplicate()
  {
    var source = Guid.NewGuid();
    var target = Guid.NewGuid();

    var result = _graphEngine.EvaluateEdge(
      [source, target],
      [new GraphEdge<Guid>(source, target)],
      source,
      target);

    Assert.False(result.CanAdd);
    Assert.Equal(GraphEdgeStatus.Duplicate, result.Status);
  }

  [Fact]
  public void EvaluateEdge_WhenNoCycleWouldBeCreated_ReturnsAllowed()
  {
    var source = Guid.NewGuid();
    var existingTarget = Guid.NewGuid();
    var newTarget = Guid.NewGuid();

    var result = _graphEngine.EvaluateEdge(
      [source, existingTarget, newTarget],
      [new GraphEdge<Guid>(source, existingTarget)],
      source,
      newTarget);

    Assert.True(result.CanAdd);
    Assert.Equal(GraphEdgeStatus.Allowed, result.Status);
  }

  [Fact]
  public void SuggestTargets_ExcludesSelfDuplicatesAndCycleTargets()
  {
    var first = Guid.NewGuid();
    var second = Guid.NewGuid();
    var third = Guid.NewGuid();
    var fourth = Guid.NewGuid();

    var suggestions = _graphEngine.SuggestTargets(
      [first, second, third, fourth],
      [
        new GraphEdge<Guid>(first, second),
        new GraphEdge<Guid>(third, first)
      ],
      first,
      10);

    Assert.Equal([fourth], suggestions);
  }

  [Fact]
  public void SortTopologically_ReturnsDependencyFirstOrder()
  {
    var dependency = Guid.NewGuid();
    var foundation = Guid.NewGuid();
    var api = Guid.NewGuid();
    var ui = Guid.NewGuid();

    var ordered = _graphEngine.SortTopologically(
      [dependency, foundation, api, ui],
      [
        new GraphEdge<Guid>(foundation, dependency),
        new GraphEdge<Guid>(api, foundation),
        new GraphEdge<Guid>(ui, foundation)
      ]);

    Assert.Equal([dependency, foundation, api, ui], ordered);
  }

  [Fact]
  public void FindReadyNodes_ReturnsCardsWithoutDependencies()
  {
    var dependency = Guid.NewGuid();
    var api = Guid.NewGuid();
    var ui = Guid.NewGuid();

    var ready = _graphEngine.FindReadyNodes(
      [dependency, api, ui],
      [
        new GraphEdge<Guid>(api, dependency),
        new GraphEdge<Guid>(ui, dependency)
      ]);

    Assert.Equal([dependency], ready);
  }

  [Fact]
  public void FindImpactedDependents_ReturnsTransitiveDependents()
  {
    var dependency = Guid.NewGuid();
    var foundation = Guid.NewGuid();
    var api = Guid.NewGuid();
    var ui = Guid.NewGuid();

    var impacted = _graphEngine.FindImpactedDependents(
      [dependency, foundation, api, ui],
      [
        new GraphEdge<Guid>(foundation, dependency),
        new GraphEdge<Guid>(api, foundation),
        new GraphEdge<Guid>(ui, foundation)
      ],
      dependency);

    Assert.Equal([foundation, api, ui], impacted);
  }

  [Fact]
  public void AnalyzeNodes_ReturnsDependencyPressureMetrics()
  {
    var dependency = Guid.NewGuid();
    var foundation = Guid.NewGuid();
    var api = Guid.NewGuid();
    var ui = Guid.NewGuid();

    var analysis = _graphEngine.AnalyzeNodes(
      [dependency, foundation, api, ui],
      [
        new GraphEdge<Guid>(foundation, dependency),
        new GraphEdge<Guid>(api, foundation),
        new GraphEdge<Guid>(ui, foundation)
      ]).ToDictionary(node => node.Node);

    Assert.Equal(0, analysis[dependency].DependencyCount);
    Assert.Equal(1, analysis[dependency].DependentCount);
    Assert.Equal(0, analysis[dependency].DependencyDepth);
    Assert.Equal(2, analysis[dependency].DependentDepth);
    Assert.Equal(3, analysis[dependency].TransitiveDependentCount);
    Assert.Equal(1, analysis[api].DependencyCount);
    Assert.Equal(0, analysis[api].DependentCount);
    Assert.Equal(2, analysis[api].DependencyDepth);
    Assert.Equal(0, analysis[api].DependentDepth);
    Assert.Equal(0, analysis[api].TransitiveDependentCount);
  }

  [Fact]
  public void FindCriticalPath_ReturnsTheLongestChainOfOpenWork()
  {
    var dependency = Guid.NewGuid();
    var foundation = Guid.NewGuid();
    var api = Guid.NewGuid();
    var ui = Guid.NewGuid();
    Guid[] nodes = [dependency, foundation, api, ui];
    GraphEdge<Guid>[] edges =
    [
      new(foundation, dependency),
      new(api, foundation),
      new(ui, foundation)
    ];

    // The UI (L) outweighs the API (XS), though both sit one step after the foundation.
    var criticalPath = _graphEngine.FindCriticalPath(
      nodes,
      edges,
      [
        Item(dependency, 0, GraphSize.S),
        Item(foundation, 1, GraphSize.M),
        Item(api, 2, GraphSize.Xs),
        Item(ui, 3, GraphSize.L)
      ]);

    Assert.Equal([dependency, foundation, ui], criticalPath);

    // Done work takes no part in it.
    var withoutDone = _graphEngine.FindCriticalPath(
      nodes,
      edges,
      [
        Item(dependency, 0, GraphSize.S) with { IsCompleted = true },
        Item(foundation, 1, GraphSize.M),
        Item(api, 2, GraphSize.Xs),
        Item(ui, 3, GraphSize.L)
      ]);

    Assert.Equal([foundation, ui], withoutDone);
  }

  [Fact]
  public void Forecast_IsReproducibleAndPutsTheMilestoneLast()
  {
    var api = Guid.NewGuid();
    var onboarding = Guid.NewGuid();
    var review = Guid.NewGuid();
    var launch = Guid.NewGuid();
    Guid[] nodes = [api, onboarding, review, launch];
    GraphEdge<Guid>[] edges =
    [
      new(review, onboarding),
      new(launch, api),
      new(launch, review)
    ];
    GraphWorkItem<Guid>[] items =
    [
      Item(api, 0, GraphSize.L),
      Item(onboarding, 1, GraphSize.M),
      Item(review, 2),
      Item(launch, 3, GraphSize.Xs)
    ];
    GraphAssignee<Guid>[] assignees = [new(api, 0), new(onboarding, 1)];
    var options = new GraphForecastOptions<Guid>(launch, 2, 500, 7);

    var forecast = _graphEngine.Forecast(nodes, edges, items, assignees, options);
    var again = _graphEngine.Forecast(nodes, edges, items, assignees, options);

    Assert.Equal(forecast.P50Days, again.P50Days);
    Assert.Equal(forecast.Cards, again.Cards);
    Assert.True(forecast.P50Days > 0);
    Assert.True(forecast.P50Days <= forecast.P85Days);
    Assert.Equal(1, forecast.UnsizedCount);
    Assert.Equal(4, forecast.Cards.Count);
    Assert.Equal(launch, forecast.Cards.First().Node);
    Assert.Equal(1, forecast.Cards.First().Criticality);
    Assert.All(forecast.Cards, card => Assert.InRange(card.Criticality, 0, 1));
    Assert.All(forecast.Cards, card => Assert.InRange(card.Sensitivity, -1, 1));
  }

  [Fact]
  public void Forecast_WithLoopedCards_Throws()
  {
    var first = Guid.NewGuid();
    var second = Guid.NewGuid();

    var error = Assert.Throws<GraphEngineException>(() => _graphEngine.Forecast(
      [first, second],
      [new GraphEdge<Guid>(first, second), new GraphEdge<Guid>(second, first)],
      [Item(first, 0), Item(second, 1)],
      [],
      new GraphForecastOptions<Guid>(first, 1, 10, 1)));

    Assert.Equal("forecast", error.Operation);
  }

  [Fact]
  public void ProposeScope_CutsTheLeastValuableCardThatMakesTheDate()
  {
    var cheap = Guid.NewGuid();
    var valuable = Guid.NewGuid();
    var launch = Guid.NewGuid();
    Guid[] nodes = [cheap, valuable, launch];
    GraphEdge<Guid>[] edges = [new(launch, cheap), new(launch, valuable)];
    GraphWorkItem<Guid>[] items =
    [
      Item(cheap, 0, GraphSize.L) with { Priority = 1 },
      Item(valuable, 1, GraphSize.L) with { Priority = 3 },
      Item(launch, 2, GraphSize.Xs)
    ];

    // Either large card alone takes at most 24 hours, and both take at least 24.
    var cut = _graphEngine.ProposeScope(nodes, edges, items, [], [], ScopeOptions(launch, 25));
    var kept = _graphEngine.ProposeScope(nodes, edges, items, [], [cheap], ScopeOptions(launch, 25));
    var chosen = _graphEngine.EvaluateScope(nodes, edges, items, [], [valuable], ScopeOptions(launch, 25));

    Assert.Equal([cheap], cut.Cuts);
    Assert.Equal([new GraphDroppedCard<Guid>(cheap, 0)], cut.Dropped);
    Assert.Equal(0, cut.OnTimeBefore);
    Assert.Equal(1, cut.OnTimeAfter);
    Assert.True(cut.P50Days <= cut.P85Days);
    Assert.Equal([valuable], kept.Cuts);
    Assert.Equal([valuable], chosen.Cuts);
    Assert.Equal(1, chosen.OnTimeAfter);
  }

  [Fact]
  public void ProposeScope_WithAnUnknownMilestone_Throws()
  {
    var card = Guid.NewGuid();

    var error = Assert.Throws<GraphEngineException>(() => _graphEngine.ProposeScope(
      [card],
      [],
      [Item(card, 0)],
      [],
      [],
      ScopeOptions(Guid.NewGuid(), 10)));

    Assert.Equal("propose_scope", error.Operation);
  }

  [Fact]
  public void ProposeStaffing_GivesOneCardToEachIdlePerson()
  {
    var signup = Guid.NewGuid();
    var invoices = Guid.NewGuid();
    var launch = Guid.NewGuid();
    Guid[] nodes = [signup, invoices, launch];
    GraphEdge<Guid>[] edges = [new(launch, signup), new(launch, invoices)];
    GraphWorkItem<Guid>[] items = [Item(signup, 0, GraphSize.M), Item(invoices, 1, GraphSize.M), Item(launch, 2, GraphSize.Xs)];

    // Nobody is assigned, so one person nobody in particular does both cards.
    var plan = _graphEngine.ProposeStaffing(nodes, edges, items, [], [0, 1], [], StaffingOptions(launch, 2));
    var chosen = _graphEngine.EvaluateStaffing(
      nodes,
      edges,
      items,
      [],
      [new GraphAssignee<Guid>(signup, 0), new GraphAssignee<Guid>(invoices, 1)],
      StaffingOptions(launch, 2));

    Assert.Equal([0, 1], plan.Changes.Select(change => change.Person).Order());
    Assert.Equal(new[] { signup, invoices }.Order(), plan.Changes.Select(change => change.Node).Order());
    Assert.All(plan.Changes, change => Assert.Equal(0, change.StartDaysAfter));
    Assert.True(plan.P85DaysAfter < plan.P85DaysBefore);
    Assert.True(plan.P50DaysAfter <= plan.P85DaysAfter);
    Assert.Equal(plan.P85DaysAfter, chosen.P85DaysAfter);
  }

  [Fact]
  public void ProposeStaffing_WithAReceiverOutsideTheTeam_Throws()
  {
    var card = Guid.NewGuid();

    var error = Assert.Throws<GraphEngineException>(() => _graphEngine.ProposeStaffing(
      [card],
      [],
      [Item(card, 0)],
      [],
      [3],
      [],
      StaffingOptions(card, 2)));

    Assert.Equal("propose_staffing", error.Operation);
  }

  [Fact]
  public void PlanWork_RanksActionableCardsByProductImpact()
  {
    var dependency = Guid.NewGuid();
    var api = Guid.NewGuid();
    var ui = Guid.NewGuid();
    var urgentStandalone = Guid.NewGuid();

    var recommendations = _graphEngine.PlanWork(
      [dependency, api, ui, urgentStandalone],
      [
        new GraphEdge<Guid>(api, dependency),
        new GraphEdge<Guid>(ui, api)
      ],
      [
        new GraphWorkItem<Guid>(dependency, false, 1, 7, 0, 0, 0),
        new GraphWorkItem<Guid>(api, false, 3, 0, 3, 1, 1),
        new GraphWorkItem<Guid>(ui, false, 3, -1, 2, 0, 2),
        new GraphWorkItem<Guid>(urgentStandalone, false, 3, 0, 0, 0, 3)
      ]).ToArray();

    Assert.Equal(dependency, recommendations[0].Node);
    Assert.Equal(0, recommendations[0].BlockerCount);
    Assert.True(recommendations[0].IsCriticalPath);
    Assert.True(recommendations[0].UnlockScore > recommendations[1].UnlockScore);
    Assert.Equal(urgentStandalone, recommendations[1].Node);
    Assert.Equal(0, recommendations[1].BlockerCount);
    Assert.True(recommendations[2].BlockerCount > 0);
  }

  [Fact]
  public void PlanWork_PrefersRequesterWorkThatFitsTheFocusBlock()
  {
    var theirs = Guid.NewGuid();
    var longMine = Guid.NewGuid();
    var quickMine = Guid.NewGuid();

    var recommendations = _graphEngine.PlanWork(
      [theirs, longMine, quickMine],
      [],
      [
        new GraphWorkItem<Guid>(theirs, false, 0, GraphWorkItem<Guid>.NoDueDate, 0, 0, 0,
          Size: GraphSize.Xs, Assignment: GraphAssignment.Others),
        new GraphWorkItem<Guid>(longMine, false, 0, GraphWorkItem<Guid>.NoDueDate, 0, 0, 1,
          Size: GraphSize.M, Assignment: GraphAssignment.Requester),
        new GraphWorkItem<Guid>(quickMine, false, 0, GraphWorkItem<Guid>.NoDueDate, 0, 0, 2,
          Size: GraphSize.Xs, Assignment: GraphAssignment.Requester,
          IsInProgress: true, IsContextMatch: true)
      ],
      new GraphPlanningOptions(MaxSize: GraphSize.S)).ToArray();

    Assert.Equal([quickMine, longMine, theirs], recommendations.Select(item => item.Node));
    var best = recommendations[0];
    Assert.True(best.FitsMaxSize);
    Assert.True(best.IsAssignmentMatch);
    Assert.True(best.IsContextMatch);
    Assert.True(best.IsInProgress);
    Assert.True(best.EffortScore > 0);
    Assert.True(best.AssignmentScore > 0);
    Assert.True(best.MomentumScore > 0);
    Assert.True(best.ContextScore > 0);
    Assert.False(recommendations[1].FitsMaxSize);
    Assert.True(recommendations[1].EffortScore < 0);
    Assert.True(recommendations[2].AssignmentScore < 0);
  }

  [Fact]
  public void PlanWork_WithoutOptions_IsUnrestricted()
  {
    var first = Guid.NewGuid();
    var second = Guid.NewGuid();

    var recommendations = _graphEngine.PlanWork(
      [first, second],
      [],
      [
        new GraphWorkItem<Guid>(first, false, 0, GraphWorkItem<Guid>.NoDueDate, 0, 0, 0,
          Size: GraphSize.Xl),
        new GraphWorkItem<Guid>(second, false, 0, GraphWorkItem<Guid>.NoDueDate, 0, 0, 1)
      ]).ToArray();

    Assert.Equal([first, second], recommendations.Select(item => item.Node));
    Assert.All(recommendations, item => Assert.True(item.FitsMaxSize));
    Assert.All(recommendations, item => Assert.False(item.IsCriticalPath));
  }

  [Fact]
  public void AnalyzeBoard_MatchesIndividualCalls()
  {
    var foundation = Guid.NewGuid();
    var api = Guid.NewGuid();
    var ui = Guid.NewGuid();
    var docs = Guid.NewGuid();
    Guid[] nodes = [foundation, api, ui, docs];
    GraphEdge<Guid>[] edges =
    [
      new(api, foundation),
      new(ui, api)
    ];
    GraphWorkItem<Guid>[] items =
    [
      new(foundation, false, 1, 3, 0, 0, 0, Size: GraphSize.Xs),
      new(api, false, 3, 1, 2, 1, 1, Size: GraphSize.M),
      new(ui, false, 2, GraphWorkItem<Guid>.NoDueDate, 0, 0, 2),
      new(docs, false, 0, GraphWorkItem<Guid>.NoDueDate, 0, 0, 3,
        Assignment: GraphAssignment.Requester)
    ];
    var options = new GraphPlanningOptions(MaxSize: GraphSize.S);

    var analysis = _graphEngine.AnalyzeBoard(nodes, edges, items, options);

    Assert.Equal(_graphEngine.IsAcyclic(nodes, edges), analysis.IsAcyclic);
    Assert.Equal(_graphEngine.FindReadyNodes(nodes, edges), analysis.ReadyNodes);
    Assert.Equal(_graphEngine.SortTopologically(nodes, edges), analysis.DependencyOrder);
    Assert.Equal(_graphEngine.FindCriticalPath(nodes, edges, items), analysis.CriticalPath);
    Assert.Equal(_graphEngine.AnalyzeNodes(nodes, edges), analysis.NodeAnalyses);
    Assert.Equal(_graphEngine.PlanWork(nodes, edges, items, options), analysis.Recommendations);
  }

  [Fact]
  public void AnalyzeBoard_ReportsCyclesWithoutPlan()
  {
    var first = Guid.NewGuid();
    var second = Guid.NewGuid();

    var analysis = _graphEngine.AnalyzeBoard(
      [first, second],
      [new GraphEdge<Guid>(first, second), new GraphEdge<Guid>(second, first)],
      [
        new GraphWorkItem<Guid>(first, false, 0, GraphWorkItem<Guid>.NoDueDate, 0, 0, 0),
        new GraphWorkItem<Guid>(second, false, 0, GraphWorkItem<Guid>.NoDueDate, 0, 0, 1)
      ]);

    Assert.False(analysis.IsAcyclic);
    Assert.Empty(analysis.DependencyOrder);
    Assert.Empty(analysis.Recommendations);
  }

  private static GraphScopeOptions<Guid> ScopeOptions(Guid milestone, double targetHours) =>
    new(milestone, 1, targetHours, 0.85, 2000, 256, 131, 200, TimeSpan.Zero, 1);

  private static GraphStaffingOptions<Guid> StaffingOptions(Guid milestone, int personCount) =>
    new(milestone, personCount, false, 2000, 8, 133, 400, TimeSpan.Zero, 1);

  private static GraphWorkItem<Guid> Item(Guid node, int position, GraphSize size = GraphSize.None) =>
    new(node, false, 0, GraphWorkItem<Guid>.NoDueDate, 0, 0, position, Size: size);
}
