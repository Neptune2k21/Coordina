using Coordina.Api.Infrastructure.Persistence;
using Coordina.Api.Modules.Tasks.Application;
using Coordina.Api.Modules.Tasks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Coordina.Api.Modules.Tasks.Infrastructure;

public sealed class PostgresBoardStore(CoordinaDbContext dbContext)
  : IBoardStore
{
  public async Task<ProjectBoard?> FindDefaultForProjectAsync(
    Guid workspaceId,
    Guid projectId,
    CancellationToken cancellationToken)
  {
    var boardId = await dbContext.Boards
      .AsNoTracking()
      .Where(board => board.WorkspaceId == workspaceId
        && board.ProjectId == projectId)
      .OrderBy(board => board.CreatedAt)
      .Select(board => (Guid?)board.Id)
      .FirstOrDefaultAsync(cancellationToken);

    return boardId is null
      ? null
      : await FindInProjectAsync(
        workspaceId,
        projectId,
        boardId.Value,
        cancellationToken);
  }

  public async Task<ProjectBoard?> FindInProjectAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    CancellationToken cancellationToken)
  {
    var board = await dbContext.Boards
      .AsNoTracking()
      .Where(board => board.WorkspaceId == workspaceId
        && board.ProjectId == projectId
        && board.Id == boardId)
      .SingleOrDefaultAsync(cancellationToken);

    if (board is null)
    {
      return null;
    }

    var lists = await dbContext.BoardLists
      .AsNoTracking()
      .Where(list => list.WorkspaceId == workspaceId
        && list.ProjectId == projectId
        && list.BoardId == boardId)
      .OrderBy(list => list.Position)
      .ToArrayAsync(cancellationToken);

    var cards = await dbContext.BoardCards
      .AsNoTracking()
      .Where(card => card.WorkspaceId == workspaceId
        && card.ProjectId == projectId
        && card.BoardId == boardId)
      .OrderBy(card => card.Position)
      .ToArrayAsync(cancellationToken);

    var cardIds = cards.Select(card => card.Id).ToArray();
    var assignees = await dbContext.BoardCardAssignees
      .AsNoTracking()
      .Where(assignee => cardIds.Contains(assignee.CardId))
      .Join(
        dbContext.AuthUsers.AsNoTracking(),
        assignee => assignee.UserId,
        user => user.Id,
        (assignee, user) => new
        {
          assignee.CardId,
          assignee.UserId,
          user.Name,
          user.Email
        })
      .ToArrayAsync(cancellationToken);

    var cardsByList = cards
      .GroupBy(card => card.ListId)
      .ToDictionary(
        group => group.Key,
        group => group
          .OrderBy(card => card.Position)
          .Select(card => ToCard(
            card,
            assignees
              .Where(assignee => assignee.CardId == card.Id)
              .Select(assignee => new ProjectBoardCardAssignee(
                assignee.UserId,
                assignee.Name,
                assignee.Email))
              .ToArray()))
          .ToArray());

    return new ProjectBoard(
      board.Id,
      board.ProjectId,
      board.WorkspaceId,
      board.Name,
      board.Template,
      board.CreatedAt,
      board.UpdatedAt,
      lists
        .Select(list => ToList(
          list,
          cardsByList.TryGetValue(list.Id, out var listCards)
            ? listCards
            : []))
        .ToArray());
  }

  public async Task<ProjectBoard> CreateAsync(
    Guid workspaceId,
    Guid projectId,
    string name,
    BoardTemplate boardTemplate,
    IReadOnlyCollection<BoardListSeed> listSeeds,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken)
  {
    var board = new BoardEntity
    {
      Id = Guid.NewGuid(),
      WorkspaceId = workspaceId,
      ProjectId = projectId,
      Name = name,
      Template = boardTemplate,
      CreatedAt = createdAt,
      UpdatedAt = createdAt
    };

    var position = 0;
    foreach (var listSeed in listSeeds)
    {
      var list = new BoardListEntity
      {
        Id = Guid.NewGuid(),
        BoardId = board.Id,
        WorkspaceId = workspaceId,
        ProjectId = projectId,
        Title = listSeed.Title,
        Position = position++,
        CreatedAt = createdAt,
        UpdatedAt = createdAt
      };

      var cardPosition = 0;
      foreach (var cardSeed in listSeed.Cards)
      {
        list.Cards.Add(new BoardCardEntity
        {
          Id = Guid.NewGuid(),
          BoardId = board.Id,
          ListId = list.Id,
          WorkspaceId = workspaceId,
          ProjectId = projectId,
          Title = cardSeed.Title,
          Description = cardSeed.Description,
          Priority = cardSeed.Priority,
          Labels = cardSeed.Labels.ToArray(),
          Position = cardPosition++,
          CreatedAt = createdAt,
          UpdatedAt = createdAt
        });
      }

      board.Lists.Add(list);
    }

    dbContext.Boards.Add(board);
    await dbContext.SaveChangesAsync(cancellationToken);

    return await FindInProjectAsync(
        workspaceId,
        projectId,
        board.Id,
        cancellationToken)
      ?? throw new InvalidOperationException("Board was not created.");
  }

  public async Task<ProjectBoard?> CreateListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    string title,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken)
  {
    var boardExists = await BoardExistsAsync(
      workspaceId,
      projectId,
      boardId,
      cancellationToken);

    if (!boardExists)
    {
      return null;
    }

    var nextPosition = await dbContext.BoardLists
      .Where(list => list.WorkspaceId == workspaceId
        && list.ProjectId == projectId
        && list.BoardId == boardId)
      .Select(list => (int?)list.Position)
      .MaxAsync(cancellationToken) + 1 ?? 0;

    dbContext.BoardLists.Add(new BoardListEntity
    {
      Id = Guid.NewGuid(),
      WorkspaceId = workspaceId,
      ProjectId = projectId,
      BoardId = boardId,
      Title = title,
      Position = nextPosition,
      CreatedAt = createdAt,
      UpdatedAt = createdAt
    });

    await TouchBoardAsync(boardId, createdAt, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);

    return await FindInProjectAsync(
      workspaceId,
      projectId,
      boardId,
      cancellationToken);
  }

  public async Task<ProjectBoard?> UpdateListAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    string title,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    var updated = await dbContext.BoardLists
      .Where(list => list.WorkspaceId == workspaceId
        && list.ProjectId == projectId
        && list.BoardId == boardId
        && list.Id == listId)
      .ExecuteUpdateAsync(setters => setters
        .SetProperty(list => list.Title, title)
        .SetProperty(list => list.UpdatedAt, updatedAt),
        cancellationToken);

    if (updated == 0)
    {
      return null;
    }

    await TouchBoardAsync(boardId, updatedAt, cancellationToken);

    return await FindInProjectAsync(
      workspaceId,
      projectId,
      boardId,
      cancellationToken);
  }

  public async Task<ProjectBoardCard?> FindCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    CancellationToken cancellationToken)
  {
    var board = await FindInProjectAsync(
      workspaceId,
      projectId,
      boardId,
      cancellationToken);

    return board?.Lists
      .SelectMany(list => list.Cards)
      .SingleOrDefault(card => card.Id == cardId);
  }

  public async Task<ProjectBoard?> CreateCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    BoardCardMutation mutation,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken)
  {
    var listExists = await ListExistsAsync(
      workspaceId,
      projectId,
      boardId,
      listId,
      cancellationToken);

    if (!listExists)
    {
      return null;
    }

    var nextPosition = await dbContext.BoardCards
      .Where(card => card.WorkspaceId == workspaceId
        && card.ProjectId == projectId
        && card.BoardId == boardId
        && card.ListId == listId)
      .Select(card => (int?)card.Position)
      .MaxAsync(cancellationToken) + 1 ?? 0;

    var card = new BoardCardEntity
    {
      Id = Guid.NewGuid(),
      WorkspaceId = workspaceId,
      ProjectId = projectId,
      BoardId = boardId,
      ListId = listId,
      Title = mutation.Title,
      Description = mutation.Description,
      Priority = mutation.Priority,
      DueDate = mutation.DueDate,
      Labels = mutation.Labels.ToArray(),
      Position = nextPosition,
      CreatedAt = createdAt,
      UpdatedAt = createdAt,
      Assignees = mutation.AssigneeIds
        .Select(assigneeId => new BoardCardAssigneeEntity
        {
          Id = Guid.NewGuid(),
          UserId = assigneeId,
          AssignedAt = createdAt
        })
        .ToArray()
    };

    dbContext.BoardCards.Add(card);
    await TouchBoardAsync(boardId, createdAt, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);

    return await FindInProjectAsync(
      workspaceId,
      projectId,
      boardId,
      cancellationToken);
  }

  public async Task<ProjectBoard?> UpdateCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    BoardCardMutation mutation,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    var card = await dbContext.BoardCards
      .Include(card => card.Assignees)
      .SingleOrDefaultAsync(card => card.WorkspaceId == workspaceId
        && card.ProjectId == projectId
        && card.BoardId == boardId
        && card.Id == cardId,
        cancellationToken);

    if (card is null)
    {
      return null;
    }

    card.Title = mutation.Title;
    card.Description = mutation.Description;
    card.Priority = mutation.Priority;
    card.DueDate = mutation.DueDate;
    card.Labels = mutation.Labels.ToArray();
    card.UpdatedAt = updatedAt;

    dbContext.BoardCardAssignees.RemoveRange(card.Assignees);
    card.Assignees = mutation.AssigneeIds
      .Select(assigneeId => new BoardCardAssigneeEntity
      {
        Id = Guid.NewGuid(),
        CardId = cardId,
        UserId = assigneeId,
        AssignedAt = updatedAt
      })
      .ToArray();

    await TouchBoardAsync(boardId, updatedAt, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);

    return await FindInProjectAsync(
      workspaceId,
      projectId,
      boardId,
      cancellationToken);
  }

  public async Task<ProjectBoard?> MoveCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid listId,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    var listExists = await ListExistsAsync(
      workspaceId,
      projectId,
      boardId,
      listId,
      cancellationToken);

    if (!listExists)
    {
      return null;
    }

    var nextPosition = await dbContext.BoardCards
      .Where(card => card.WorkspaceId == workspaceId
        && card.ProjectId == projectId
        && card.BoardId == boardId
        && card.ListId == listId)
      .Select(card => (int?)card.Position)
      .MaxAsync(cancellationToken) + 1 ?? 0;

    var updated = await dbContext.BoardCards
      .Where(card => card.WorkspaceId == workspaceId
        && card.ProjectId == projectId
        && card.BoardId == boardId
        && card.Id == cardId)
      .ExecuteUpdateAsync(setters => setters
        .SetProperty(card => card.ListId, listId)
        .SetProperty(card => card.Position, nextPosition)
        .SetProperty(card => card.UpdatedAt, updatedAt),
        cancellationToken);

    if (updated == 0)
    {
      return null;
    }

    await TouchBoardAsync(boardId, updatedAt, cancellationToken);

    return await FindInProjectAsync(
      workspaceId,
      projectId,
      boardId,
      cancellationToken);
  }

  public async Task<bool> DeleteCardAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    CancellationToken cancellationToken)
  {
    var deleted = await dbContext.BoardCards
      .Where(card => card.WorkspaceId == workspaceId
        && card.ProjectId == projectId
        && card.BoardId == boardId
        && card.Id == cardId)
      .ExecuteDeleteAsync(cancellationToken);

    return deleted > 0;
  }

  private async Task<bool> BoardExistsAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    CancellationToken cancellationToken)
  {
    return await dbContext.Boards
      .AsNoTracking()
      .AnyAsync(board => board.WorkspaceId == workspaceId
        && board.ProjectId == projectId
        && board.Id == boardId,
        cancellationToken);
  }

  private async Task<bool> ListExistsAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    CancellationToken cancellationToken)
  {
    return await dbContext.BoardLists
      .AsNoTracking()
      .AnyAsync(list => list.WorkspaceId == workspaceId
        && list.ProjectId == projectId
        && list.BoardId == boardId
        && list.Id == listId,
        cancellationToken);
  }

  private async Task TouchBoardAsync(
    Guid boardId,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    await dbContext.Boards
      .Where(board => board.Id == boardId)
      .ExecuteUpdateAsync(setters => setters
        .SetProperty(board => board.UpdatedAt, updatedAt),
        cancellationToken);
  }

  private static ProjectBoardList ToList(
    BoardListEntity entity,
    IReadOnlyCollection<ProjectBoardCard> cards) =>
    new(
      entity.Id,
      entity.BoardId,
      entity.ProjectId,
      entity.WorkspaceId,
      entity.Title,
      entity.Position,
      entity.CreatedAt,
      entity.UpdatedAt,
      cards);

  private static ProjectBoardCard ToCard(
    BoardCardEntity entity,
    IReadOnlyCollection<ProjectBoardCardAssignee> assignees) =>
    new(
      entity.Id,
      entity.BoardId,
      entity.ListId,
      entity.ProjectId,
      entity.WorkspaceId,
      entity.Title,
      entity.Description,
      entity.Priority,
      entity.DueDate,
      entity.Labels,
      entity.Position,
      entity.CreatedAt,
      entity.UpdatedAt,
      assignees);
}
