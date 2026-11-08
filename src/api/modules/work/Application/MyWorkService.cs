using Coordina.Api.Core.Results;
using Coordina.Api.Core.Time;
using Coordina.Api.Modules.Boards.Application;
using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Projects.Application;
using Coordina.Api.Modules.Projects.Contracts;
using Coordina.Api.Modules.Work.Contracts;

namespace Coordina.Api.Modules.Work.Application;

/// <summary>
/// Gathers the user's work from every active project of a workspace. Each
/// board is analysed exactly as its planning view does it, with the user's
/// saved preferences for that board, so a card keeps its score and reasons;
/// this service only chooses which cards to show and in which section.
/// </summary>
public sealed class MyWorkService(
  IProjectService projects,
  IBoardStore boards,
  IBoardPlanningPreferenceStore preferences,
  IGraphEngine<Guid> graphEngine,
  TimeProvider clock) : IMyWorkService
{
  /// <summary>Next lists this many cards; the total says how many more are ready.</summary>
  public const int NextLimit = 20;

  private static readonly BoardPlanningRequest DefaultPlanning = new();

  public async Task<Result<MyWorkResponse>> GetAsync(
    Guid workspaceId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    // Archived and completed projects have no work left to plan.
    var visible = await projects.ListAsync(
      workspaceId,
      includeArchived: false,
      includeCompleted: false,
      userId,
      cancellationToken);

    if (!visible.IsSuccess)
    {
      return visible.Error;
    }

    var items = new List<(Section Section, MyWorkItemResponse Item)>();

    foreach (var project in visible.Value.OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase))
    {
      items.AddRange(await RankProjectAsync(project, userId, cancellationToken));
    }

    var next = Ranked(items, Section.Next);

    return Result.Success(new MyWorkResponse(
      workspaceId,
      Ranked(items, Section.Now),
      [.. next.Take(NextLimit)],
      next.Length,
      Ranked(items, Section.Blocked)));
  }

  private async Task<IEnumerable<(Section, MyWorkItemResponse)>> RankProjectAsync(
    ProjectResponse project,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var board = await boards.FindDefaultForProjectAsync(project.WorkspaceId, project.Id, cancellationToken);

    if (board is null)
    {
      return [];
    }

    var planning = await preferences.FindAsync(board.Id, userId, cancellationToken)
      ?? BoardPlanningPreferences.Parse(DefaultPlanning, userId).Value;
    var analysis = BoardDependencyGraph.AnalyzeBoard(graphEngine, board, planning, clock.GetUtcToday());
    var summary = new MyWorkProjectResponse(project.Id, project.Name, project.Key, project.Icon, project.Color);

    return analysis.PlanItems
      .Select(item => (Section: SectionOf(item, userId, planning), item))
      .Where(entry => entry.Section is not Section.None)
      .Select(entry => (entry.Section, new MyWorkItemResponse(
        summary,
        board.Id,
        entry.item.Card.DueDate,
        entry.item.Card.Assignees.Count == 0,
        BoardGraphResponseMapper.ToPlanItem(entry.item, analysis))))
      .ToArray();
  }

  // Now: what I am working on. Next: what I can start, mine or nobody's
  // when unassigned cards are included. Blocked: mine, waiting on others.
  private static Section SectionOf(
    BoardWorkRecommendation item,
    Guid userId,
    BoardPlanningPreferences planning)
  {
    if (item.Card.IsCompleted)
    {
      return Section.None;
    }

    var isMine = item.Card.Assignees.Any(assignee => assignee.UserId == userId);
    var isOpenToMe = isMine || (item.Card.Assignees.Count == 0 && planning.IncludeUnassigned);

    return (isMine, item.IsInProgress, item.IsActionable) switch
    {
      (true, true, _) => Section.Now,
      (_, false, true) when isOpenToMe => Section.Next,
      (true, false, false) => Section.Blocked,
      _ => Section.None
    };
  }

  // Scores come from the same engine for every board, so they compare
  // across projects; ties keep project and board order.
  private static MyWorkItemResponse[] Ranked(
    IEnumerable<(Section Section, MyWorkItemResponse Item)> items,
    Section section) =>
    [.. items
      .Where(entry => entry.Section == section)
      .Select(entry => entry.Item)
      .OrderByDescending(item => item.Plan.Score)];

  private enum Section
  {
    None,
    Now,
    Next,
    Blocked
  }
}
