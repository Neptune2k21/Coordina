namespace Coordina.Api.Modules.Boards.Application.Graph;

/// <summary>Mirrors <c>coordina_graph_question_kind</c>.</summary>
public enum GraphQuestionKind
{
  Size = 1,
  Split = 2,
  Progress = 3,
  Dependency = 4
}

public sealed record GraphQuestionOptions<TNode>(TNode Milestone, GraphSimulation Simulation, int Limit)
  where TNode : notnull;

/// <summary>What to ask, most worth asking first, and the days between the 50% and 85% dates as things are.</summary>
public sealed record GraphQuestions<TNode>(double MarginDays, IReadOnlyList<GraphQuestion<TNode>> Questions)
  where TNode : notnull;

/// <summary>
/// A question about a card, and for a dependency the card it may wait for, how
/// likely that is and how many days it would move the date. The margin is what
/// the answer is expected to take off it.
/// </summary>
public sealed record GraphQuestion<TNode>(
  GraphQuestionKind Kind,
  TNode Node,
  TNode Other,
  double Probability,
  double MarginDays,
  double ShiftDays)
  where TNode : notnull;

/// <summary>A word of a card's text, under any number, and how much it counts.</summary>
public sealed record GraphCardToken<TNode>(TNode Node, uint Token, double Weight)
  where TNode : notnull;

/// <summary>A dependency the board probably misses, from 0 to 1.</summary>
public sealed record GraphDependencyHint<TNode>(TNode Card, TNode Dependency, double Probability)
  where TNode : notnull;
