using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Modules.Boards.Application.Planning;

/// <summary>
/// A board as the engine plans it, with what its answers are numbered by.
/// Everything that measures a milestone starts from this, so two answers about
/// the same board always agree.
/// </summary>
internal sealed record BoardPlan(
  ProjectBoard Board,
  GraphBoard<Guid> Graph,
  IReadOnlyDictionary<Guid, ProjectBoardCard> Cards,
  IReadOnlyList<Guid> People,
  IReadOnlyList<string> Labels,
  DateOnly Today)
{
  // A few thousand runs keep the 85% date steady, and a fixed seed keeps a
  // forecast the same from one load to the next while nothing changes.
  private const int RunCount = 2000;
  private const ulong Seed = 129;

  /// <summary>The same draws for every question asked about a board.</summary>
  public static GraphSimulation Simulation { get; } = new(Seed, RunCount);

  public int PersonOf(Guid userId) => _personByUser[userId];

  private readonly Dictionary<Guid, int> _personByUser = People
    .Select((userId, index) => (userId, index))
    .ToDictionary(person => person.userId, person => person.index);

  /// <summary>The open milestone with that id, or null.</summary>
  public ProjectBoardCard? FindMilestone(Guid cardId) =>
    Cards.GetValueOrDefault(cardId) is { MilestoneDate: not null } card ? card : null;

  public DateOnly Finish(double workingDays) => WorkingDays.Finish(Today, workingDays);

  public DateOnly Start(double workingDays) => WorkingDays.Start(Today, workingDays);
}
