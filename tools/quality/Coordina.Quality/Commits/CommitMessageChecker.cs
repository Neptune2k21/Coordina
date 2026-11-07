using System.Text.RegularExpressions;
using Coordina.Quality.Checks;

namespace Coordina.Quality.Commits;

/// <summary>
/// Pull requests are squash-merged, so their title becomes the commit on
/// master, and the PR title check enforces the same header format. Checking
/// commits locally catches the problem before it reaches a pull request.
/// </summary>
internal static partial class CommitMessageChecker
{
  public const int MaxHeaderLength = 72;

  public static RuleInfo Format { get; } = new(
    "CQ5001",
    "Commit headers follow Conventional Commits",
    Severity.Error,
    "'type(scope): summary' keeps history and release notes readable. Allowed types: feat, fix, perf, refactor, test, docs, build, ci, chore, revert. The summary starts in lowercase and has no final period.");

  public static RuleInfo Length { get; } = new(
    "CQ5002",
    $"Commit headers are {MaxHeaderLength} characters or fewer",
    Severity.Error,
    "Longer headers are cut off by GitHub and most git tools.");

  public static RuleInfo BodySeparator { get; } = new(
    "CQ5003",
    "A blank line separates the header from the body",
    Severity.Error,
    "Git and GitHub treat everything before the first blank line as the header.");

  public static IReadOnlyList<RuleInfo> Rules { get; } = [Format, Length, BodySeparator];

  public static IReadOnlyCollection<Violation> Check(string message, string source = "COMMIT_EDITMSG")
  {
    var lines = message
      .Replace("\r\n", "\n", StringComparison.Ordinal)
      .Split('\n')
      .Where(line => !line.StartsWith('#'))
      .ToArray();
    var header = lines.FirstOrDefault(line => line.Trim().Length > 0)?.TrimEnd() ?? string.Empty;
    var violations = new List<Violation>();

    if (IsGeneratedByGit(header))
    {
      return violations;
    }

    if (!HeaderPattern().IsMatch(header))
    {
      violations.Add(new Violation(
        Format,
        source,
        1,
        $"'{header}' is not 'type(scope): lowercase summary', for example 'fix(boards): reject dependency cycles'."));
    }

    if (header.Length > MaxHeaderLength)
    {
      violations.Add(new Violation(
        Length,
        source,
        1,
        $"The header is {header.Length} characters long. Keep it at {MaxHeaderLength} or fewer."));
    }

    var headerIndex = Array.IndexOf(lines, lines.First(line => line.Trim().Length > 0));

    if (lines.Length > headerIndex + 1 && lines[headerIndex + 1].Trim().Length > 0)
    {
      violations.Add(new Violation(
        BodySeparator,
        source,
        2,
        "Add a blank line after the header."));
    }

    return violations;
  }

  // Git writes these itself; rewording them would only get in the way.
  private static bool IsGeneratedByGit(string header) =>
    header.StartsWith("Merge ", StringComparison.Ordinal)
    || header.StartsWith("fixup! ", StringComparison.Ordinal)
    || header.StartsWith("squash! ", StringComparison.Ordinal)
    || header.StartsWith("amend! ", StringComparison.Ordinal);

  [GeneratedRegex(@"^(feat|fix|perf|refactor|test|docs|build|ci|chore|revert)(\([a-z0-9-]+\))?!?: [^A-Z ].*[^.]$")]
  private static partial Regex HeaderPattern();
}
