namespace Coordina.Api.Tests.Support;

/// <summary>Finds what a test is about in a board response.</summary>
public static class BoardResponseExtensions
{
  public static BoardCardResponse Card(this BoardResponse board, string cardId) =>
    board.Lists.SelectMany(list => list.Cards).Single(card => card.Id == cardId);

  public static BoardListResponse List(this BoardResponse board, string listId) =>
    board.Lists.Single(list => list.Id == listId);
}
