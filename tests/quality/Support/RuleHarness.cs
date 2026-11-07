using Coordina.Quality.Checks;

namespace Coordina.Quality.Tests.Support;

internal static class RuleHarness
{
  /// <summary>Runs one rule the way the checker does, including suppressions.</summary>
  public static Violation[] Check(Rule rule, string path, string text)
  {
    var file = new SourceFile(path, text);

    return rule.AppliesTo(file)
      ? Suppressions.Apply(file, rule.Check(file)).ToArray()
      : [];
  }

  public static string[] Messages(Rule rule, string path, string text) =>
    Check(rule, path, text).Select(violation => violation.Message).ToArray();
}
