namespace Coordina.Api.Modules.Tasks.Application.Graph;

public sealed record GraphEdgeEvaluation(GraphEdgeStatus Status)
{
  public bool CanAdd => Status == GraphEdgeStatus.Allowed;
}

public enum GraphEdgeStatus
{
  Allowed,
  MissingSource,
  MissingTarget,
  SelfReference,
  Duplicate,
  Cycle,
  InvalidArgument = 100
}
