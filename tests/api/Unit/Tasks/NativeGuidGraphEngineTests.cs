using Coordina.Api.Modules.Tasks.Application.Graph;

namespace Coordina.Api.Tests.Unit.Tasks;

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
}
