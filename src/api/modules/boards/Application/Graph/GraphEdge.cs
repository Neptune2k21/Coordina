namespace Coordina.Api.Modules.Boards.Application.Graph;

public readonly record struct GraphEdge<TNode>(
  TNode Source,
  TNode Target)
  where TNode : notnull;
