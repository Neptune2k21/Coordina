using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Modules.Boards.Application;

/// <summary>
/// Asks the graph engine what to take out of a milestone so it ships by its
/// target date, or what a chosen cut buys. Both run on the forecast's draws,
/// so they start from the dates the milestone shows.
/// </summary>
internal static class BoardMilestoneScopes
{
  // The commit date a milestone shows is its 85% date, so a proposal aims to
  // bring that date to the target.
  private const double Goal = 0.85;
  private const int ScreenRunCount = 256;
  private const int EvaluationLimit = 120;

  // Measured on the 1,000-card benchmark: the whole proposal fits in this,
  // and a person checking cards gets the new chance right away.
  private static readonly TimeSpan TimeLimit = TimeSpan.FromMilliseconds(450);

  public static Result<ScopeCut> Propose(
    IGraphEngine<Guid> graphEngine,
    ProjectBoard board,
    Guid milestoneId,
    IReadOnlyCollection<Guid> keep,
    DateOnly today) =>
    Run(
      board,
      milestoneId,
      today,
      (input, options) => graphEngine.ProposeScope(
        input.Graph.Nodes,
        input.Graph.Edges,
        input.WorkItems,
        input.Team.Assignees,
        keep,
        options));

  public static Result<ScopeCut> Evaluate(
    IGraphEngine<Guid> graphEngine,
    ProjectBoard board,
    Guid milestoneId,
    IReadOnlyCollection<Guid> cuts,
    DateOnly today) =>
    Run(
      board,
      milestoneId,
      today,
      (input, options) => graphEngine.EvaluateScope(
        input.Graph.Nodes,
        input.Graph.Edges,
        input.WorkItems,
        input.Team.Assignees,
        cuts,
        options));

  private static Result<ScopeCut> Run(
    ProjectBoard board,
    Guid milestoneId,
    DateOnly today,
    Func<ScopeInput, GraphScopeOptions<Guid>, GraphScopeCut<Guid>> solve)
  {
    var graph = BoardDependencyGraph.ToGraph(board);
    var milestone = graph.Cards.FirstOrDefault(card => card.Id == milestoneId);

    if (milestone?.MilestoneDate is not { } target)
    {
      return Error.NotFound();
    }

    if (milestone.IsCompleted)
    {
      return Error.Validation("milestoneId", "This milestone is done; there is nothing left to cut.");
    }

    var workingDays = WorkingDays.Until(today, target);

    if (workingDays == 0)
    {
      return Error.Validation("milestoneId", "This milestone's target date has passed.");
    }

    var team = BoardMilestoneForecasts.TeamOf(graph.Cards);
    var input = new ScopeInput(
      graph,
      BoardDependencyGraph.ToWorkItems(board, graph.Cards, today, BoardMilestoneForecasts.Nobody),
      team);
    var cut = solve(
      input,
      new GraphScopeOptions<Guid>(
        milestoneId,
        team.Size,
        workingDays * WorkingDays.FocusHoursPerDay,
        Goal,
        BoardMilestoneForecasts.RunCount,
        ScreenRunCount,
        BoardMilestoneForecasts.Seed,
        EvaluationLimit,
        TimeLimit));

    return Result.Success(new ScopeCut(milestone, target, today, cut, graph.Cards.ToDictionary(card => card.Id)));
  }

  private sealed record ScopeInput(
    BoardGraphSnapshot Graph,
    IReadOnlyCollection<GraphWorkItem<Guid>> WorkItems,
    BoardMilestoneForecasts.ForecastTeam Team);
}

/// <summary>A cut the engine proposed or measured, and the board it was measured on.</summary>
internal sealed record ScopeCut(
  ProjectBoardCard Milestone,
  DateOnly Target,
  DateOnly Today,
  GraphScopeCut<Guid> Engine,
  Dictionary<Guid, ProjectBoardCard> CardsById)
{
  /// <summary>The cards that leave the scope, which the milestone may depend on directly.</summary>
  public IReadOnlyCollection<Guid> DroppedCardIds =>
    Engine.Dropped.Select(card => card.Node).ToArray();

  public BoardMilestoneScopeResponse ToResponse() =>
    new(
      Milestone.Id,
      Target,
      Engine.OnTimeBefore,
      Engine.OnTimeAfter,
      WorkingDays.Finish(Today, Engine.P50Days),
      WorkingDays.Finish(Today, Engine.P85Days),
      Engine.Cuts
        .Select((cardId, index) => new BoardMilestoneCutResponse(
          cardId,
          CardsById[cardId].Title,
          Engine.Dropped
            .Where(card => card.Cut == index && card.Node != cardId)
            .Select(card => new BoardMilestoneCutCardResponse(card.Node, CardsById[card.Node].Title))
            .ToArray()))
        .ToArray());
}
