using System.Net;
using Coordina.Api.Tests.Support;

namespace Coordina.Api.Tests.Integration.Boards;

public sealed class BoardCommentEndpointsTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  [Fact]
  public async Task EditComment_ByItsAuthor_ChangesTheTextAndRecordsTheEdit()
  {
    var (scope, cardId) = await CreateCardAsync();
    var member = await scope.AddMemberAsync();
    var commentId = await CommentAsync(scope, cardId, "Shipping friday", member.AccessToken);

    var board = await scope.SendForAsync<BoardResponse>(
      HttpMethod.Patch, CommentPath(cardId, commentId), new { body = "  Shipping monday  " }, member.AccessToken);

    var comment = Assert.Single(board.Card(cardId).Comments);
    Assert.Equal("Shipping monday", comment.Body);
    Assert.NotNull(comment.EditedAt);
    Assert.True(comment.EditedAt >= comment.CreatedAt);
  }

  [Fact]
  public async Task NewComment_HasNoEditedAt()
  {
    var (scope, cardId) = await CreateCardAsync();
    await CommentAsync(scope, cardId, "First thought");

    var comment = Assert.Single((await scope.GetBoardAsync()).Card(cardId).Comments);

    Assert.Null(comment.EditedAt);
  }

  [Fact]
  public async Task EditComment_BySomeoneElse_IsForbidden_EvenForAnOwner()
  {
    var (scope, cardId) = await CreateCardAsync();
    var author = await scope.AddMemberAsync();
    var other = await scope.AddMemberAsync();
    var commentId = await CommentAsync(scope, cardId, "Mine", author.AccessToken);

    using var byMember = await scope.SendAsync(
      HttpMethod.Patch, CommentPath(cardId, commentId), new { body = "Theirs" }, other.AccessToken);
    using var byOwner = await scope.SendAsync(
      HttpMethod.Patch, CommentPath(cardId, commentId), new { body = "Theirs" });

    Assert.Equal(HttpStatusCode.Forbidden, byMember.StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, byOwner.StatusCode);
    Assert.Equal("Mine", Assert.Single((await scope.GetBoardAsync()).Card(cardId).Comments).Body);
  }

  [Fact]
  public async Task EditComment_WithAnEmptyBody_ReturnsValidationProblem()
  {
    var (scope, cardId) = await CreateCardAsync();
    var commentId = await CommentAsync(scope, cardId, "Keep me");

    using var response = await scope.SendAsync(HttpMethod.Patch, CommentPath(cardId, commentId), new { body = "   " });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task DeleteComment_ByItsAuthor_RemovesIt()
  {
    var (scope, cardId) = await CreateCardAsync();
    var member = await scope.AddMemberAsync();
    var commentId = await CommentAsync(scope, cardId, "Wrong card", member.AccessToken);

    using var response = await scope.SendAsync(HttpMethod.Delete, CommentPath(cardId, commentId), accessToken: member.AccessToken);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Empty((await scope.GetBoardAsync()).Card(cardId).Comments);
  }

  [Fact]
  public async Task DeleteComment_ByAWorkspaceOwner_RemovesSomeoneElsesComment()
  {
    var (scope, cardId) = await CreateCardAsync();
    var member = await scope.AddMemberAsync();
    var commentId = await CommentAsync(scope, cardId, "Off topic", member.AccessToken);

    using var response = await scope.SendAsync(HttpMethod.Delete, CommentPath(cardId, commentId));

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Empty((await scope.GetBoardAsync()).Card(cardId).Comments);
  }

  [Fact]
  public async Task DeleteComment_ByAnotherMember_IsForbidden()
  {
    var (scope, cardId) = await CreateCardAsync();
    var author = await scope.AddMemberAsync();
    var other = await scope.AddMemberAsync();
    var commentId = await CommentAsync(scope, cardId, "Stays", author.AccessToken);

    using var response = await scope.SendAsync(HttpMethod.Delete, CommentPath(cardId, commentId), accessToken: other.AccessToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Single((await scope.GetBoardAsync()).Card(cardId).Comments);
  }

  [Fact]
  public async Task CommentRoutes_ForANonMember_ReturnNotFound()
  {
    var (scope, cardId) = await CreateCardAsync();
    var commentId = await CommentAsync(scope, cardId, "Private");
    var outsider = await Api.RegisterAsync("Outsider");

    using var edit = await scope.SendAsync(
      HttpMethod.Patch, CommentPath(cardId, commentId), new { body = "Hacked" }, outsider.AccessToken);
    using var delete = await scope.SendAsync(HttpMethod.Delete, CommentPath(cardId, commentId), accessToken: outsider.AccessToken);

    Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
  }

  [Fact]
  public async Task CommentRoutes_ForACommentOfAnotherCard_ReturnNotFound()
  {
    var (scope, cardId) = await CreateCardAsync();
    var otherCard = await scope.AddCardAsync(scope.FirstList.Id, "Other card");
    var commentId = await CommentAsync(scope, cardId, "Belongs here");

    using var edit = await scope.SendAsync(HttpMethod.Patch, CommentPath(otherCard.Id, commentId), new { body = "Moved" });
    using var delete = await scope.SendAsync(HttpMethod.Delete, CommentPath(otherCard.Id, commentId));

    Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
  }

  private static string CommentPath(string cardId, string commentId) => $"/cards/{cardId}/comments/{commentId}";

  private async Task<(BoardScope Scope, string CardId)> CreateCardAsync()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var card = await scope.AddCardAsync(scope.FirstList.Id, "Discussed card");

    return (scope, card.Id);
  }

  private static async Task<string> CommentAsync(BoardScope scope, string cardId, string body, string? accessToken = null)
  {
    var board = await scope.SendForAsync<BoardResponse>(
      HttpMethod.Post, $"/cards/{cardId}/comments", new { body }, accessToken);

    return board.Card(cardId).Comments.OrderBy(comment => comment.CreatedAt).Last().Id;
  }
}
