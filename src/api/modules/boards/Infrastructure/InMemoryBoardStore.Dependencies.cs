using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Modules.Boards.Infrastructure;

// Dependencies between cards.
public sealed partial class InMemoryBoardStore
{
  public Task<ProjectBoard?> AddCardDependencyAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid dependsOnCardId,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!TryGetBoard(workspaceId, projectId, boardId, out var board)
      || !TryGetCard(workspaceId, projectId, boardId, cardId, out var card)
      || !TryGetCard(workspaceId, projectId, boardId, dependsOnCardId, out _))
    {
      return Task.FromResult<ProjectBoard?>(null);
    }

    var exists = _dependencies.Values.Any(dependency =>
      dependency.CardId == cardId
      && dependency.DependsOnCardId == dependsOnCardId);

    if (!exists)
    {
      var dependency = new DependencyRecord(
        Guid.NewGuid(),
        cardId,
        dependsOnCardId,
        createdAt);
      _dependencies[dependency.Id] = dependency;
      _cards[cardId] = card with { UpdatedAt = createdAt };
      _boards[boardId] = board with { UpdatedAt = createdAt };
    }

    return Task.FromResult<ProjectBoard?>(ToBoard(_boards[boardId]));
  }

  public Task<int> DeleteCardDependenciesAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    IReadOnlyCollection<Guid> dependsOnCardIds,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!TryGetBoard(workspaceId, projectId, boardId, out var board)
      || !TryGetCard(workspaceId, projectId, boardId, cardId, out var card))
    {
      return Task.FromResult(0);
    }

    var deleted = _dependencies.Values
      .Where(candidate => candidate.CardId == cardId
        && dependsOnCardIds.Contains(candidate.DependsOnCardId))
      .Count(dependency => _dependencies.TryRemove(dependency.Id, out _));

    if (deleted > 0)
    {
      _cards[cardId] = card with { UpdatedAt = updatedAt };
      _boards[boardId] = board with { UpdatedAt = updatedAt };
    }

    return Task.FromResult(deleted);
  }
}
