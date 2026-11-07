using Coordina.Quality.Rules.CSharp;
using Coordina.Quality.Tests.Support;

namespace Coordina.Quality.Tests.Checks;

public sealed class SuppressionsTests
{
  private const string Path = "src/api/core/Sample.cs";

  [Fact]
  public void Suppression_WithAReason_HidesTheRuleOnItsLineAndTheNext()
  {
    var violations = RuleHarness.Check(new DescriptiveNamesRule(), Path, """
      public sealed class Sample
      {
        // quality-ignore CQ1006: x and y are the axes of the chart.
        public void Plot(int x, int y) { }
      }
      """);

    Assert.Empty(violations);
  }

  [Fact]
  public void Suppression_OnlyHidesTheNamedRule()
  {
    var violations = RuleHarness.Check(new DescriptiveNamesRule(), Path, """
      public sealed class Sample
      {
        // quality-ignore CQ1005: not the rule that fires here.
        public void Plot(int x) { }
      }
      """);

    Assert.Single(violations);
  }

  [Theory]
  [InlineData("// quality-ignore CQ1006")]
  [InlineData("// quality-ignore CQ1006:   ")]
  [InlineData("// quality-ignore: no rule id")]
  public void Suppression_WithoutRuleOrReason_IsItselfAnError(string comment)
  {
    var violations = RuleHarness.Check(
      new DescriptiveNamesRule(),
      Path,
      $"public sealed class Sample\n{{\n  {comment}\n  public void Plot(int x) {{ }}\n}}\n");

    Assert.Contains(violations, violation => violation.Rule.Id == "CQ0001");
    Assert.Contains(violations, violation => violation.Rule.Id == "CQ1006");
  }

  [Fact]
  public void SuppressionInsideACSharpString_IsIgnored()
  {
    var violations = RuleHarness.Check(new DescriptiveNamesRule(), Path, """
      public sealed class Sample
      {
        private const string Example = "// quality-ignore CQ1006";
        public void Plot(int x) { }
      }
      """);

    Assert.Equal(["CQ1006"], violations.Select(violation => violation.Rule.Id));
  }

  [Fact]
  public void TextThatIsNotAComment_IsNotASuppression()
  {
    var violations = RuleHarness.Check(new DescriptiveNamesRule(), Path, """
      public sealed class Sample
      {
        private const string Pattern = "quality-ignore";
        public void Plot(int x) { }
      }
      """);

    Assert.Equal(["CQ1006"], violations.Select(violation => violation.Rule.Id));
  }
}
