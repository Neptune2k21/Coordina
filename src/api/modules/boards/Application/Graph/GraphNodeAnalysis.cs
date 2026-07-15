namespace Coordina.Api.Modules.Tasks.Application.Graph;

public sealed record GraphNodeAnalysis<TNode>(
  TNode Node,
  int DependencyCount,
  int DependentCount,
  int DependencyDepth,
  int DependentDepth,
  int TransitiveDependentCount)
  where TNode : notnull;
