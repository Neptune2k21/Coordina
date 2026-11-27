using Coordina.Api.Core.Results;
using Coordina.Api.Core.Time;
using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Application.Planning;
using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Modules.Projects.Application;
using Coordina.Api.Modules.Workspaces.Application;

namespace Coordina.Api.Modules.Boards.Application;

internal sealed class BoardMilestoneStaffingService(
  IBoardStore boards,
  BoardPlanReader plans,
  IProjectAccessGuard projectAccess,
  IWorkspaceStore workspaces,
  IGraphEngine<Guid> graphEngine,
  TimeProvider clock) : IBoardMilestoneStaffingService
{
  private const string NotMembers = "Everyone in the plan must belong to the workspace.";

  public async Task<Result<BoardMilestoneStaffingResponse>> ProposeAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid milestoneId,
    BoardMilestoneStaffingRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var target = await ReadAsync(
      new BoardAccess(workspaceId, projectId, boardId, milestoneId, userId),
      cancellationToken);

    if (!target.IsSuccess)
    {
      return target.Error;
    }

    var receivers = ReceiversOf(target.Value, request.People);

    if (receivers.Any(person => !target.Value.Members.ContainsKey(person)))
    {
      return Error.Validation(nameof(request.People), NotMembers);
    }

    var plan = BoardMilestoneStaffings.Propose(graphEngine, target.Value, receivers, request.AllowReassignment);

    return plan.IsSuccess ? Result.Success(plan.Value.ToResponse()) : plan.Error;
  }

  public async Task<Result<BoardMilestoneStaffingResponse>> EvaluateAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid milestoneId,
    BoardMilestoneStaffChangesRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var target = await ReadAsync(
      new BoardAccess(workspaceId, projectId, boardId, milestoneId, userId),
      cancellationToken);

    if (!target.IsSuccess)
    {
      return target.Error;
    }

    var changes = ChangesOf(request);

    if (changes.Any(change => !target.Value.Members.ContainsKey(change.UserId)))
    {
      return Error.Validation(nameof(request.Changes), NotMembers);
    }

    var plan = BoardMilestoneStaffings.Evaluate(graphEngine, target.Value, changes);

    return plan.IsSuccess ? Result.Success(plan.Value.ToResponse()) : plan.Error;
  }

  public async Task<Result<BoardResponse>> ApplyAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid milestoneId,
    BoardMilestoneStaffChangesRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var access = await projectAccess.AuthorizeWriteAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (!access.IsSuccess)
    {
      return access.Error;
    }

    var changes = ChangesOf(request);

    if (changes.Length == 0)
    {
      return Error.Validation(nameof(request.Changes), "Choose at least one card to give to someone.");
    }

    var members = await MembersAsync(workspaceId, cancellationToken);

    if (changes.Any(change => !members.ContainsKey(change.UserId)))
    {
      return Error.Validation(nameof(request.Changes), NotMembers);
    }

    return await boards.RunExclusiveAsync<Result<BoardResponse>>(
      boardId,
      async exclusiveToken =>
      {
        var board = await boards.FindInProjectAsync(workspaceId, projectId, boardId, exclusiveToken);
        var cards = board?.Lists.SelectMany(list => list.Cards).ToDictionary(card => card.Id);

        if (cards is null || cards.GetValueOrDefault(milestoneId)?.MilestoneDate is null)
        {
          return Error.NotFound();
        }

        if (changes.Any(change => cards.GetValueOrDefault(change.CardId) is not { IsCompleted: false }))
        {
          return Error.Validation(nameof(request.Changes), "Only open cards of this board can be given to someone.");
        }

        await boards.ReassignCardsAsync(
          workspaceId,
          projectId,
          boardId,
          changes,
          clock.GetUtcNow(),
          exclusiveToken);
        var updated = await boards.FindInProjectAsync(workspaceId, projectId, boardId, exclusiveToken);

        return updated is null ? Error.NotFound() : Result.Success(BoardResponseMapper.ToResponse(updated));
      },
      cancellationToken);
  }

  // Nobody named means the people assigned to open cards of the board, or
  // the whole workspace when nobody is.
  private static Guid[] ReceiversOf(StaffingBoard target, IReadOnlyCollection<Guid>? people)
  {
    if (people is { Count: > 0 })
    {
      return people.Distinct().ToArray();
    }

    var team = BoardMilestoneStaffings.TeamOf(target.Plan.Board)
      .Where(target.Members.ContainsKey)
      .ToArray();

    return team.Length > 0 ? team : target.Members.Keys.ToArray();
  }

  // A card given twice goes to the last person named.
  private static BoardCardReassignment[] ChangesOf(BoardMilestoneStaffChangesRequest request) =>
    (request.Changes ?? [])
      .GroupBy(change => change.CardId)
      .Select(group => new BoardCardReassignment(group.Key, group.Last().UserId))
      .ToArray();

  // Measuring a plan changes nothing, so reading the board is enough.
  private async Task<Result<StaffingBoard>> ReadAsync(
    BoardAccess target,
    CancellationToken cancellationToken)
  {
    var members = await MembersAsync(target.WorkspaceId, cancellationToken);
    var plan = await plans.ReadAsync(
      new BoardTarget(target.WorkspaceId, target.ProjectId, target.BoardId, target.UserId),
      members.Keys,
      cancellationToken);

    return plan.IsSuccess
      ? Result.Success(new StaffingBoard(plan.Value, target.MilestoneId, members))
      : plan.Error;
  }

  private async Task<Dictionary<Guid, BoardCardAssigneeResponse>> MembersAsync(
    Guid workspaceId,
    CancellationToken cancellationToken)
  {
    var members = await workspaces.ListMembersAsync(workspaceId, cancellationToken);

    return members.ToDictionary(
      member => member.UserId,
      member => new BoardCardAssigneeResponse(member.UserId, member.Name, member.Email));
  }

  private sealed record BoardAccess(
    Guid WorkspaceId,
    Guid ProjectId,
    Guid BoardId,
    Guid MilestoneId,
    Guid UserId);
}
