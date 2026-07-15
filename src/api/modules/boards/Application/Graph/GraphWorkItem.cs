namespace Coordina.Api.Modules.Boards.Application.Graph;

public sealed record GraphWorkItem<TNode>(
  TNode Node,
  bool IsCompleted,
  int Priority,
  int DueDays,
  int SubtaskTotal,
  int SubtaskCompleted,
  int Position)
  where TNode : notnull
{
  public const int NoDueDate = int.MaxValue;
}
