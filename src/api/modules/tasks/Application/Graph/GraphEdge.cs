namespace Coordina.Api.Modules.Tasks.Application.Graph;

public readonly record struct GraphEdge<TNode>(
  TNode Source,
  TNode Target)
  where TNode : notnull;
