using Coordina.Api.Core.Persistence;
using Coordina.Api.Modules.Auth.Infrastructure;
using Coordina.Api.Modules.Boards.Application;
using Coordina.Api.Modules.Boards.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coordina.Api.Modules.Boards.Infrastructure;

/// <summary>
/// The board store on PostgreSQL. It is split by area into partial files
/// (PostgresBoardStore.Cards.cs and so on); this one holds the board itself
/// and the helpers the areas share.
/// </summary>
public sealed partial class PostgresBoardStore : IBoardStore
{
  private readonly CoordinaDbContext _dbContext;

  public PostgresBoardStore(CoordinaDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  private DbSet<AuthUserEntity> AuthUsers => _dbContext.Set<AuthUserEntity>();

  private DbSet<BoardEntity> Boards => _dbContext.Set<BoardEntity>();

  private DbSet<BoardListEntity> BoardLists => _dbContext.Set<BoardListEntity>();

  private DbSet<BoardCardEntity> BoardCards => _dbContext.Set<BoardCardEntity>();

  private DbSet<BoardCardAssigneeEntity> BoardCardAssignees => _dbContext.Set<BoardCardAssigneeEntity>();

  private DbSet<BoardCardCommentEntity> BoardCardComments => _dbContext.Set<BoardCardCommentEntity>();

  private DbSet<BoardCardSubtaskEntity> BoardCardSubtasks => _dbContext.Set<BoardCardSubtaskEntity>();

  private DbSet<BoardCardDependencyEntity> BoardCardDependencies => _dbContext.Set<BoardCardDependencyEntity>();

  public async Task<T> RunExclusiveAsync<T>(
    Guid boardId,
    Func<CancellationToken, Task<T>> action,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(action);

    // Re-entrant: a caller already holding the lock reuses its transaction, and
    // PostgreSQL lets a session take the same advisory lock again.
    await using var transaction = _dbContext.Database.CurrentTransaction is null
      ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
      : null;

    // An advisory lock only makes other RunExclusiveAsync callers wait for this
    // board. Locking the board row would also block unrelated writes, and could
    // deadlock with writes that update a card and then its board. The lock is
    // released when the transaction ends.
    await _dbContext.Database.ExecuteSqlInterpolatedAsync(
      $"SELECT pg_advisory_xact_lock(hashtextextended({boardId.ToString()}, 0))",
      cancellationToken);

    var result = await action(cancellationToken);

    if (transaction is not null)
    {
      await transaction.CommitAsync(cancellationToken);
    }

    return result;
  }

  public async Task<ProjectBoard?> CreateAsync(
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
      board.Lists.Add(NewList(board, listSeed, position++));
    }

    Boards.Add(board);

    try
    {
      await _dbContext.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException exception)
      when (exception.InnerException is PostgresException
      {
        SqlState: PostgresErrorCodes.UniqueViolation
      })
    {
      // Another request created this project's board first.
      _dbContext.ChangeTracker.Clear();
      return null;
    }

    return await FindInProjectAsync(
        workspaceId,
        projectId,
        board.Id,
        cancellationToken)
      ?? throw new InvalidOperationException("Board was not created.");
  }

  // A list seeded from a template, with its starter cards. Cards in a DONE
  // list start completed, like their subtasks.
  private static BoardListEntity NewList(BoardEntity board, BoardListSeed seed, int position)
  {
    var list = new BoardListEntity
    {
      Id = Guid.NewGuid(),
      BoardId = board.Id,
      WorkspaceId = board.WorkspaceId,
      ProjectId = board.ProjectId,
      Title = seed.Title,
      Position = position,
      TaskStatus = seed.TaskStatus,
      CreatedAt = board.CreatedAt,
      UpdatedAt = board.CreatedAt
    };
    var isDone = seed.TaskStatus == BoardTaskStatus.Done;
    var cardPosition = 0;

    foreach (var cardSeed in seed.Cards)
    {
      list.Cards.Add(NewCard(list, cardSeed, cardPosition++, isDone));
    }

    return list;
  }

  private static BoardCardEntity NewCard(BoardListEntity list, BoardCardSeed seed, int position, bool isDone)
  {
    var createdAt = list.CreatedAt;
    var subtaskPosition = 0;

    return new BoardCardEntity
    {
      Id = seed.Id,
      BoardId = list.BoardId,
      ListId = list.Id,
      WorkspaceId = list.WorkspaceId,
      ProjectId = list.ProjectId,
      Title = seed.Title,
      Description = seed.Description,
      Priority = seed.Priority,
      DueDate = seed.DueDate,
      EstimatedMinutes = seed.EstimatedMinutes,
      Labels = seed.Labels.ToArray(),
      IsCompleted = isDone,
      CompletedAt = isDone ? createdAt : null,
      Position = position,
      CreatedAt = createdAt,
      UpdatedAt = createdAt,
      Subtasks = seed.Subtasks
        .Select(title => new BoardCardSubtaskEntity
        {
          Id = Guid.NewGuid(),
          CardId = seed.Id,
          Title = title,
          IsCompleted = isDone,
          CompletedAt = isDone ? createdAt : null,
          Position = subtaskPosition++,
          CreatedAt = createdAt,
          UpdatedAt = createdAt
        })
        .ToList(),
      Dependencies = seed.DependsOnCardIds
        .Select(dependsOnCardId => new BoardCardDependencyEntity
        {
          Id = Guid.NewGuid(),
          CardId = seed.Id,
          DependsOnCardId = dependsOnCardId,
          CreatedAt = createdAt
        })
        .ToList()
    };
  }

  private async Task<bool> BoardExistsAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    CancellationToken cancellationToken)
  {
    return await Boards
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
    return await BoardLists
      .AsNoTracking()
      .AnyAsync(list => list.WorkspaceId == workspaceId
        && list.ProjectId == projectId
        && list.BoardId == boardId
        && list.Id == listId,
        cancellationToken);
  }

  private async Task<int> NextCardPositionAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid listId,
    CancellationToken cancellationToken)
  {
    return await BoardCards
      .Where(card => card.WorkspaceId == workspaceId
        && card.ProjectId == projectId
        && card.BoardId == boardId
        && card.ListId == listId)
      .Select(card => (int?)card.Position)
      .MaxAsync(cancellationToken) + 1 ?? 0;
  }

  private async Task<bool> CardExistsAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    CancellationToken cancellationToken)
  {
    return await BoardCards
      .AsNoTracking()
      .AnyAsync(card => card.WorkspaceId == workspaceId
        && card.ProjectId == projectId
        && card.BoardId == boardId
        && card.Id == cardId,
        cancellationToken);
  }

  private async Task TouchCardAsync(
    Guid cardId,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    await BoardCards
      .Where(card => card.Id == cardId)
      .ExecuteUpdateAsync(setters => setters
        .SetProperty(card => card.UpdatedAt, updatedAt),
        cancellationToken);
  }

  private async Task TouchBoardAsync(
    Guid boardId,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    await Boards
      .Where(board => board.Id == boardId)
      .ExecuteUpdateAsync(setters => setters
        .SetProperty(board => board.UpdatedAt, updatedAt),
        cancellationToken);
  }
}
