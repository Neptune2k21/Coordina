namespace Coordina.Api.Modules.Boards.Application.Graph;

/// <summary>Mirrors <c>coordina_graph_action_kind</c>.</summary>
public enum GraphActionKind
{
  CutCard = 1,
  AssignCard = 2,
  AddPerson = 3
}

public sealed record GraphActionOptions<TNode>(
  TNode Milestone,
  GraphSimulation Simulation,
  int ScreenRunCount,
  int Limit)
  where TNode : notnull;

/// <summary>
/// One thing to do about a milestone, and its forecast once it is done. The
/// node and person are those of the card to cut or assign; the value lost is
/// what a cut gives up, by the priorities of the cards that leave.
/// </summary>
public sealed record GraphAction<TNode>(
  GraphActionKind Kind,
  TNode Node,
  int Person,
  double OnTimeBefore,
  double OnTimeAfter,
  double P85DaysBefore,
  double P85DaysAfter,
  double OthersDelayDays,
  int DroppedCount,
  double ValueLost)
  where TNode : notnull;
