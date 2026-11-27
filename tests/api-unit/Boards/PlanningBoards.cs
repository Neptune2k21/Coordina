using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Unit.Tests.Boards;

/// <summary>Boards for the planning tests, without a database.</summary>
internal static class PlanningBoards
{
  private static readonly DateTimeOffset Created = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

  public static ProjectBoardCard Card(string title, params Guid[] people) =>
    new(
      Guid.NewGuid(),
      Guid.Empty,
      Guid.Empty,
      Guid.Empty,
      Guid.Empty,
      title,
      null,
      null,
      null,
      CardSize.M,
      null,
      [],
      false,
      null,
      null,
      0,
      Created,
      Created,
      people.Select(person => new ProjectBoardCardAssignee(person, null, null)).ToArray(),
      [],
      [],
      []);

  public static ProjectBoardCard DependingOn(this ProjectBoardCard card, params ProjectBoardCard[] dependencies) =>
    card with
    {
      Dependencies = dependencies
        .Select(dependency => new ProjectBoardCardDependency(dependency.Id, dependency.Title, dependency.IsCompleted))
        .ToArray()
    };

  public static ProjectBoard Board(params ProjectBoardCard[] cards)
  {
    var boardId = Guid.NewGuid();
    var todo = new ProjectBoardList(
      Guid.NewGuid(),
      boardId,
      Guid.Empty,
      Guid.Empty,
      "To Do",
      0,
      BoardTaskStatus.Todo,
      Created,
      Created,
      []);
    var started = todo with { Id = Guid.NewGuid(), Title = "Doing", TaskStatus = BoardTaskStatus.InProgress };

    return new ProjectBoard(
      boardId,
      Guid.Empty,
      Guid.Empty,
      "Board",
      BoardTemplate.Basic,
      Created,
      Created,
      [
        todo with { Cards = cards.Select(card => card with { ListId = todo.Id }).ToArray() },
        started
      ]);
  }
}
