using System.Text.RegularExpressions;
using Coordina.Quality.Checks;

namespace Coordina.Quality.Rules.Files;

/// <summary>
/// clang-tidy's own suppression, <c>// NOLINTNEXTLINE(check): reason</c>, follows
/// the same rule as <c>quality-ignore</c>: it names what it silences and why.
/// clang-tidy itself accepts a bare <c>NOLINT</c>, which silences every check.
/// </summary>
internal sealed partial class NolintReasonRule : Rule
{
  public override RuleInfo Info { get; } = new(
    "CQ3002",
    "NOLINT comments name their checks and give a reason",
    Severity.Error,
    "A bare NOLINT silences every clang-tidy check on the line, including ones added later, and one without a reason cannot be reviewed.");

  public override bool AppliesTo(SourceFile file) => RepositoryPaths.IsGraphEngineSource(file.Path);

  public override IEnumerable<Violation> Check(SourceFile file)
  {
    for (var index = 0; index < file.Lines.Count; index++)
    {
      var match = NolintPattern().Match(file.Lines[index]);

      // NOLINTEND closes a NOLINTBEGIN, which already carries the reason.
      if (!match.Success || match.Groups["kind"].Value == "END")
      {
        continue;
      }

      var directive = $"NOLINT{match.Groups["kind"].Value}";

      if (match.Groups["checks"].Value.Trim().Length == 0)
      {
        yield return At(file, index + 1, $"Name the checks this silences: '// {directive}(check-name): reason'.");
      }
      else if (!ReasonPattern().IsMatch(match.Groups["rest"].Value))
      {
        yield return At(file, index + 1, $"Say why after the checks: '// {directive}({match.Groups["checks"].Value}): reason'.");
      }
    }
  }

  // clang-tidy honours NOLINT anywhere on the line, not only at the start of a comment.
  [GeneratedRegex(@"\bNOLINT(?<kind>NEXTLINE|BEGIN|END)?\b(?:\((?<checks>[^)]*)\))?(?<rest>.*)")]
  private static partial Regex NolintPattern();

  [GeneratedRegex(@"^\s*:\s*\S")]
  private static partial Regex ReasonPattern();
}
