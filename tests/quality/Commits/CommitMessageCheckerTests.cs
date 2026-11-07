using Coordina.Quality.Commits;

namespace Coordina.Quality.Tests.Commits;

public sealed class CommitMessageCheckerTests
{
  [Theory]
  [InlineData("fix(boards): reject dependency cycles")]
  [InlineData("feat: add a blank board option")]
  [InlineData("refactor(api)!: rename the board contract")]
  [InlineData("fix(boards): reject cycles\n\nTwo requests could pass the check together.")]
  [InlineData("# Git comment\nci: run CodeQL weekly\n")]
  [InlineData("Merge branch 'master' into fix/cycles")]
  [InlineData("fixup! fix(boards): reject cycles")]
  public void ValidMessages_Pass(string message)
  {
    Assert.Empty(CommitMessageChecker.Check(message));
  }

  [Theory]
  [InlineData("Fix the bug", "CQ5001")]
  [InlineData("fix(boards): Reject cycles", "CQ5001")]
  [InlineData("fix(boards): reject cycles.", "CQ5001")]
  [InlineData("feature(boards): reject cycles", "CQ5001")]
  [InlineData("fix(Boards): reject cycles", "CQ5001")]
  [InlineData("fix(boards): reject cycles\nTwo requests could pass.", "CQ5003")]
  public void InvalidMessages_NameTheBrokenRule(string message, string rule)
  {
    Assert.Contains(CommitMessageChecker.Check(message), violation => violation.Rule.Id == rule);
  }

  [Fact]
  public void LongHeaders_AreRejected()
  {
    var header = "fix(boards): " + new string('a', CommitMessageChecker.MaxHeaderLength);

    Assert.Contains(CommitMessageChecker.Check(header), violation => violation.Rule.Id == "CQ5002");
  }
}
