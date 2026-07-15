namespace Coordina.Api.Modules.Tasks.Application.Graph;

public sealed record GraphWorkRecommendation<TNode>(
  TNode Node,
  int Score,
  int BlockerCount,
  int UnlockScore,
  int UrgencyScore,
  int PriorityScore,
  int ProgressScore,
  bool IsCriticalPath)
  where TNode : notnull;
