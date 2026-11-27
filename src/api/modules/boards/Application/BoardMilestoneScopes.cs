using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Application.Planning;
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
    BoardPlan plan,
    Guid milestoneId,
    IReadOnlyCollection<Guid> keep) =>
    Run(plan, milestoneId, options => graphEngine.ProposeScope(plan.Graph, keep, options));

  public static Result<ScopeCut> Evaluate(
    IGraphEngine<Guid> graphEngine,
    BoardPlan plan,
    Guid milestoneId,
    IReadOnlyCollection<Guid> cuts) =>
    Run(plan, milestoneId, options => graphEngine.EvaluateScope(plan.Graph, cuts, options));

  private static Result<ScopeCut> Run(
    BoardPlan plan,
    Guid milestoneId,
    Func<GraphScopeOptions<Guid>, GraphScopeCut<Guid>> solve)
  {
    if (plan.FindMilestone(milestoneId) is not { MilestoneDate: { } target } milestone)
    {
      return Error.NotFound();
    }

    if (milestone.IsCompleted)
    {
      return Error.Validation("milestoneId", "This milestone is done; there is nothing left to cut.");
    }

    if (WorkingDays.Until(plan.Today, target) == 0)
    {
      return Error.Validation("milestoneId", "This milestone's target date has passed.");
    }

    var cut = solve(
      new GraphScopeOptions<Guid>(
        milestoneId,
        BoardPlan.Simulation,
        Goal,
        ScreenRunCount,
        EvaluationLimit,
        TimeLimit));

    return Result.Success(new ScopeCut(milestone, target, plan, cut));
  }
}

/// <summary>A cut the engine proposed or measured, and the board it was measured on.</summary>
internal sealed record ScopeCut(
  ProjectBoardCard Milestone,
  DateOnly Target,
  BoardPlan Plan,
  GraphScopeCut<Guid> Engine)
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
      Plan.Finish(Engine.P50Days),
      Plan.Finish(Engine.P85Days),
      Engine.Cuts
        .Select((cardId, index) => new BoardMilestoneCutResponse(
          cardId,
          Plan.Cards[cardId].Title,
          Engine.Dropped
            .Where(card => card.Cut == index && card.Node != cardId)
            .Select(card => new BoardMilestoneCutCardResponse(card.Node, Plan.Cards[card.Node].Title))
            .ToArray()))
        .ToArray());
}
