using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Coordina.Quality.Checks;

/// <summary>
/// Reads <c>// quality-ignore CQ1006: why</c> comments. A suppression covers its
/// own line and the next one, and must give a reason so a reviewer can judge it.
/// </summary>
internal static partial class Suppressions
{
  public static RuleInfo Info { get; } = new(
    "CQ0001",
    "Suppressions name a rule and give a reason",
    Severity.Error,
    "An exception without a reason cannot be reviewed, and one without a rule id hides every rule.");

  public static IReadOnlyCollection<Violation> Apply(
    SourceFile file,
    IEnumerable<Violation> violations)
  {
    var suppressed = new HashSet<(string RuleId, int Line)>();
    var problems = new List<Violation>();

    foreach (var (line, comment) in Comments(file))
    {
      var match = SuppressionPattern().Match(comment);

      if (!match.Success)
      {
        continue;
      }

      var ruleId = match.Groups["rule"].Value;

      if (ruleId.Length == 0 || match.Groups["reason"].Value.Trim().Length == 0)
      {
        problems.Add(new Violation(
          Info,
          file.Path,
          line,
          "Write the suppression as 'quality-ignore CQ1234: reason'."));
        continue;
      }

      suppressed.Add((ruleId, line));
      suppressed.Add((ruleId, line + 1));
    }

    return violations
      .Where(violation => !suppressed.Contains((violation.Rule.Id, violation.Line)))
      .Concat(problems)
      .ToArray();
  }

  /// <summary>
  /// Comments with their line. C# comments come from the syntax tree, so a
  /// string that happens to contain "// quality-ignore" is not mistaken for one.
  /// </summary>
  private static IEnumerable<(int Line, string Text)> Comments(SourceFile file)
  {
    if (RepositoryPaths.IsCSharp(file.Path))
    {
      return file.Root.DescendantTrivia()
        .Where(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
        .Select(trivia => (
          trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
          trivia.ToString()));
    }

    return file.Lines.Select((text, index) => (index + 1, text));
  }

  [GeneratedRegex(@"//\s*quality-ignore(?:\s+(?<rule>CQ\d{4}))?(?:\s*:\s*(?<reason>.*))?")]
  private static partial Regex SuppressionPattern();
}
