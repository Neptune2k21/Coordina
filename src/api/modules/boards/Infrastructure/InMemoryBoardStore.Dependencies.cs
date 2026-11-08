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

  public Task<bool> DeleteCardDependencyAsync(
    Guid workspaceId,
    Guid projectId,
    Guid boardId,
    Guid cardId,
    Guid dependsOnCardId,
    DateTimeOffset updatedAt,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!TryGetBoard(workspaceId, projectId, boardId, out var board)
      || !TryGetCard(workspaceId, projectId, boardId, cardId, out var card))
    {
      return Task.FromResult(false);
    }

    var dependency = _dependencies.Values.FirstOrDefault(candidate =>
      candidate.CardId == cardId
      && candidate.DependsOnCardId == dependsOnCardId);

    if (dependency is null)
    {
      return Task.FromResult(false);
    }

    var deleted = _dependencies.TryRemove(dependency.Id, out _);
    if (deleted)
    {
      _cards[cardId] = card with { UpdatedAt = updatedAt };
      _boards[boardId] = board with { UpdatedAt = updatedAt };
    }

    return Task.FromResult(deleted);
  }
}
