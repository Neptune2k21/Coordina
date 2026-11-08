using Coordina.Api.Modules.Boards.Domain;
using Microsoft.EntityFrameworkCore;

namespace Coordina.Api.Modules.Boards.Infrastructure;

// Dependencies between cards.
public sealed partial class PostgresBoardStore
{
  public async Task<ProjectBoard?> AddCardDependencyAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid dependsOnCardId,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken)
  {
    var cardExists = await CardExistsAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      cancellationToken);
    var dependencyExists = await CardExistsAsync(
      workspaceId,
      projectId,
      boardId,
      dependsOnCardId,
      cancellationToken);

    if (!cardExists || !dependencyExists)
    {
      return null;
    }

    var alreadyLinked = await BoardCardDependencies
      .AnyAsync(dependency => dependency.CardId == cardId
        && dependency.DependsOnCardId == dependsOnCardId,
        cancellationToken);

    if (!alreadyLinked)
    {
      BoardCardDependencies.Add(new BoardCardDependencyEntity
      {
        Id = Guid.NewGuid(),
        CardId = cardId,
        DependsOnCardId = dependsOnCardId,
        CreatedAt = createdAt
      });

      await TouchCardAsync(cardId, createdAt, cancellationToken);
      await TouchBoardAsync(boardId, createdAt, cancellationToken);
      await _dbContext.SaveChangesAsync(cancellationToken);
    }

    return await FindInProjectAsync(
      workspaceId,
      projectId,
      boardId,
      cancellationToken);
  }

  public async Task<bool> DeleteCardDependencyAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid dependsOnCardId,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    var cardExists = await CardExistsAsync(
      workspaceId,
      projectId,
      boardId,
      cardId,
      cancellationToken);

    if (!cardExists)
    {
      return false;
    }

    var deleted = await BoardCardDependencies
      .Where(dependency => dependency.CardId == cardId
        && dependency.DependsOnCardId == dependsOnCardId)
      .ExecuteDeleteAsync(cancellationToken);

    if (deleted == 0)
    {
      return false;
    }

    await TouchCardAsync(cardId, updatedAt, cancellationToken);
    await TouchBoardAsync(boardId, updatedAt, cancellationToken);

    return true;
  }
}
