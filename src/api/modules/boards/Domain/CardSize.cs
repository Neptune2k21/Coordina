namespace Coordina.Api.Modules.Boards.Domain;

/// <summary>
/// How big a card is: an order of magnitude, not a duration. The graph engine
/// owns what each size means in time (src/graph-engine/src/planning/card_size.cpp),
/// and the values match its <c>coordina_graph_size</c>.
/// </summary>
public enum CardSize
{
  Xs = 1,
  S = 2,
  M = 3,
  L = 4,
  Xl = 5,
  Xxl = 6
}
