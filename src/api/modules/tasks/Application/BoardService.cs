using Coordina.Api.Modules.Projects.Application;
using Coordina.Api.Modules.Projects.Domain;
using Coordina.Api.Modules.Tasks.Contracts;
using Coordina.Api.Modules.Tasks.Domain;
using Coordina.Api.Modules.Workspaces.Application;
using Coordina.Api.Modules.Workspaces.Domain;

namespace Coordina.Api.Modules.Tasks.Application;

public sealed class BoardService(
  IBoardStore boards,
  IProjectStore projects,
  IWorkspaceStore workspaces) : IBoardService
{
  public async Task<TaskResult<BoardResponse>> GetDefaultAsync(
    Guid workspaceId,
    Guid projectId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var access = await FindAccessAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (access is null)
    {
      return new TaskResult<BoardResponse>(TaskResultStatus.NotFound);
    }

    var board = await boards.FindDefaultForProjectAsync(
      workspaceId,
      projectId,
      cancellationToken);

    return board is null
      ? new TaskResult<BoardResponse>(TaskResultStatus.NotFound)
      : new TaskResult<BoardResponse>(
        TaskResultStatus.Success,
        BoardResponseMapper.ToResponse(board));
  }

  public async Task<TaskResult<BoardResponse>> CreateAsync(
    Guid workspaceId,
    Guid projectId,
    CreateBoardRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var access = await FindWritableAccessAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (access.Status != TaskResultStatus.Success)
    {
      return new TaskResult<BoardResponse>(access.Status, Message: access.Message);
    }

    var template = BoardRules.ParseTemplate(request.Template, out var templateError);
    var listSeeds = template is null
      ? []
      : BoardRules.BuildListSeeds(template.Value, request.CustomListTitles);
    var errors = BoardRules.ValidateBoard(
      request.Name,
      template,
      templateError,
      listSeeds);

    if (errors.Count > 0)
    {
      return new TaskResult<BoardResponse>(
        TaskResultStatus.ValidationError,
        Errors: errors);
    }

    var now = DateTimeOffset.UtcNow;
    var board = await boards.CreateAsync(
      workspaceId,
      projectId,
      request.Name!.Trim(),
      template!.Value,
      listSeeds,
      now,
      cancellationToken);

    return new TaskResult<BoardResponse>(
      TaskResultStatus.Success,
      BoardResponseMapper.ToResponse(board));
  }

  public async Task<TaskResult<BoardResponse>> CreateListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    CreateBoardListRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var access = await FindWritableAccessAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (access.Status != TaskResultStatus.Success)
    {
      return new TaskResult<BoardResponse>(access.Status, Message: access.Message);
    }

    var errors = BoardRules.ValidateList(request.Title);

    if (errors.Count > 0)
    {
      return new TaskResult<BoardResponse>(
        TaskResultStatus.ValidationError,
        Errors: errors);
    }

    var board = await boards.CreateListAsync(
      workspaceId,
      projectId,
      boardId,
      request.Title!.Trim(),
      DateTimeOffset.UtcNow,
      cancellationToken);

    return board is null
      ? new TaskResult<BoardResponse>(TaskResultStatus.NotFound)
      : new TaskResult<BoardResponse>(
        TaskResultStatus.Success,
        BoardResponseMapper.ToResponse(board));
  }

  public async Task<TaskResult<BoardResponse>> UpdateListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    UpdateBoardListRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var access = await FindWritableAccessAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (access.Status != TaskResultStatus.Success)
    {
      return new TaskResult<BoardResponse>(access.Status, Message: access.Message);
    }

    var errors = BoardRules.ValidateList(request.Title);

    if (errors.Count > 0)
    {
      return new TaskResult<BoardResponse>(
        TaskResultStatus.ValidationError,
        Errors: errors);
    }

    var board = await boards.UpdateListAsync(
      workspaceId,
      projectId,
      boardId,
      listId,
      request.Title!.Trim(),
      DateTimeOffset.UtcNow,
      cancellationToken);

    return board is null
      ? new TaskResult<BoardResponse>(TaskResultStatus.NotFound)
      : new TaskResult<BoardResponse>(
        TaskResultStatus.Success,
        BoardResponseMapper.ToResponse(board));
  }

  public async Task<TaskResult<BoardResponse>> CreateCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    CreateBoardCardRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var mutation = await BuildCardMutationAsync(
      workspaceId,
      projectId,
      userId,
      request.Title,
      request.Description,
      request.Priority,
      request.DueDate,
      request.Labels,
      request.AssigneeIds,
      null,
      cancellationToken);

    if (mutation.Status != TaskResultStatus.Success)
    {
      return new TaskResult<BoardResponse>(
        mutation.Status,
        Errors: mutation.Errors,
        Message: mutation.Message);
    }

    var board = await boards.CreateCardAsync(
      workspaceId,
      projectId,
      boardId,
      listId,
      mutation.Value!,
      DateTimeOffset.UtcNow,
      cancellationToken);

    return board is null
      ? new TaskResult<BoardResponse>(TaskResultStatus.NotFound)
      : new TaskResult<BoardResponse>(
        TaskResultStatus.Success,
        BoardResponseMapper.ToResponse(board));
  }

  public async Task<TaskResult<BoardResponse>> UpdateCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    UpdateBoardCardRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var current = await boards.FindCardAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      cancellationToken);

    if (current is null)
    {
      return new TaskResult<BoardResponse>(TaskResultStatus.NotFound);
    }

    var mutation = await BuildCardMutationAsync(
      workspaceId,
      projectId,
      userId,
      request.Title ?? current.Title,
      request.Description ?? current.Description,
      request.Priority is null ? current.Priority?.ToString() : request.Priority,
      request.ClearDueDate ? null : request.DueDate ?? current.DueDate,
      request.Labels ?? current.Labels.ToArray(),
      request.AssigneeIds ?? current.Assignees.Select(assignee => assignee.UserId).ToArray(),
      current,
      cancellationToken);

    if (mutation.Status != TaskResultStatus.Success)
    {
      return new TaskResult<BoardResponse>(
        mutation.Status,
        Errors: mutation.Errors,
        Message: mutation.Message);
    }

    var board = await boards.UpdateCardAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      mutation.Value!,
      DateTimeOffset.UtcNow,
      cancellationToken);

    return board is null
      ? new TaskResult<BoardResponse>(TaskResultStatus.NotFound)
      : new TaskResult<BoardResponse>(
        TaskResultStatus.Success,
        BoardResponseMapper.ToResponse(board));
  }

  public async Task<TaskResult<BoardResponse>> MoveCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    MoveBoardCardRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var access = await FindWritableAccessAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (access.Status != TaskResultStatus.Success)
    {
      return new TaskResult<BoardResponse>(access.Status, Message: access.Message);
    }

    if (request.ListId == Guid.Empty)
    {
      return new TaskResult<BoardResponse>(
        TaskResultStatus.ValidationError,
        Errors: new Dictionary<string, string[]>
        {
          [nameof(request.ListId)] = ["Target list is required."]
        });
    }

    var board = await boards.MoveCardAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      request.ListId,
      DateTimeOffset.UtcNow,
      cancellationToken);

    return board is null
      ? new TaskResult<BoardResponse>(TaskResultStatus.NotFound)
      : new TaskResult<BoardResponse>(
        TaskResultStatus.Success,
        BoardResponseMapper.ToResponse(board));
  }

  public async Task<TaskResult<object>> DeleteCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var access = await FindWritableAccessAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (access.Status != TaskResultStatus.Success)
    {
      return new TaskResult<object>(access.Status, Message: access.Message);
    }

    if (!CanManageDestructiveBoardActions(access.Value!.Role, access.Value.Project, userId))
    {
      return new TaskResult<object>(
        TaskResultStatus.Forbidden,
        Message: "Only workspace owners or project owners can delete cards.");
    }

    var deleted = await boards.DeleteCardAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      cancellationToken);

    return deleted
      ? new TaskResult<object>(TaskResultStatus.Success)
      : new TaskResult<object>(TaskResultStatus.NotFound);
  }

  private async Task<TaskResult<BoardCardMutation>> BuildCardMutationAsync(
    Guid workspaceId,
    Guid projectId,
    Guid userId,
    string? title,
    string? description,
    string? priority,
    DateOnly? dueDate,
    string[]? labels,
    Guid[]? assigneeIds,
    ProjectBoardCard? current,
    CancellationToken cancellationToken)
  {
    var access = await FindWritableAccessAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (access.Status != TaskResultStatus.Success)
    {
      return new TaskResult<BoardCardMutation>(
        access.Status,
        Message: access.Message);
    }

    var parsedPriority = BoardRules.ParsePriority(priority, out var priorityError);
    var normalizedLabels = BoardRules.NormalizeLabels(labels);
    var normalizedAssignees = (assigneeIds ?? [])
      .Where(id => id != Guid.Empty)
      .Distinct()
      .ToArray();
    var errors = BoardRules.ValidateCard(
      title,
      description,
      priorityError,
      normalizedLabels,
      dueDate);

    if (normalizedAssignees.Length > 0)
    {
      var members = await workspaces.ListMembersAsync(
        workspaceId,
        cancellationToken);
      var memberIds = members.Select(member => member.UserId).ToHashSet();
      var invalidAssignees = normalizedAssignees
        .Where(assigneeId => !memberIds.Contains(assigneeId))
        .ToArray();

      if (invalidAssignees.Length > 0)
      {
        errors["AssigneeIds"] = [
          "All assignees must belong to the workspace."
        ];
      }
    }

    if (errors.Count > 0)
    {
      return new TaskResult<BoardCardMutation>(
        TaskResultStatus.ValidationError,
        Errors: errors);
    }

    return new TaskResult<BoardCardMutation>(
      TaskResultStatus.Success,
      new BoardCardMutation(
        title!.Trim(),
        description is null && current is not null
          ? current.Description
          : BoardRules.NormalizeText(description),
        parsedPriority,
        dueDate,
        normalizedLabels,
        normalizedAssignees));
  }

  private async Task<ProjectAccess?> FindAccessAsync(
    Guid workspaceId,
    Guid projectId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var role = await workspaces.FindUserRoleAsync(
      workspaceId,
      userId,
      cancellationToken);

    if (role is null)
    {
      return null;
    }

    var project = await projects.FindInWorkspaceAsync(
      workspaceId,
      projectId,
      cancellationToken);

    return project is null ? null : new ProjectAccess(role.Value, project);
  }

  private async Task<TaskResult<ProjectAccess>> FindWritableAccessAsync(
    Guid workspaceId,
    Guid projectId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var access = await FindAccessAsync(
      workspaceId,
      projectId,
      userId,
      cancellationToken);

    if (access is null)
    {
      return new TaskResult<ProjectAccess>(TaskResultStatus.NotFound);
    }

    if (access.Project.Status == ProjectStatus.Completed)
    {
      return new TaskResult<ProjectAccess>(
        TaskResultStatus.Conflict,
        Message: "Completed projects are read-only.");
    }

    return new TaskResult<ProjectAccess>(
      TaskResultStatus.Success,
      access);
  }

  private static bool CanManageDestructiveBoardActions(
    WorkspaceRole role,
    Project project,
    Guid userId) =>
    role == WorkspaceRole.Owner || project.ProjectOwnerId == userId;

  private sealed record ProjectAccess(WorkspaceRole Role, Project Project);
}
