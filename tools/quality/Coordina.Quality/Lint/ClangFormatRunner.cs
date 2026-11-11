using System.Globalization;
using System.Text.RegularExpressions;
using Coordina.Quality.Checks;

namespace Coordina.Quality.Lint;

/// <summary>
/// Checks the graph engine's layout with clang-format, configured in
/// <c>.clang-format</c>, and reports each line it would change.
/// </summary>
internal sealed partial class ClangFormatRunner(ClangTools tools, string repositoryRoot)
{
  public static RuleInfo Info { get; } = new(
    "clang-format",
    "C++ code is formatted with clang-format",
    Severity.Error,
    "One layout keeps reviews about the change, not about spacing. Run 'make graph-engine-format' to fix every file; .clang-format holds the style.");

  /// <summary>
  /// Checks <paramref name="paths"/>, or every engine file in
  /// <paramref name="allPaths"/> when a formatting setting changed.
  /// </summary>
  public IReadOnlyList<Violation> Run(IReadOnlyCollection<string> paths, IReadOnlyCollection<string> allPaths)
  {
    var targets = (paths.Any(ClangTools.IsConfiguration) ? allPaths : paths)
      .Where(RepositoryPaths.IsGraphEngineSource)
      .Where(path => File.Exists(Path.Combine(repositoryRoot, path)))
      .ToArray();

    if (targets.Length == 0)
    {
      return [];
    }

    var result = ToolProcess.Run(tools.Format, repositoryRoot, ["--dry-run", "--Werror", .. targets]);
    var violations = Parse(result.StandardError, repositoryRoot);

    // 1 with findings is the expected failure; anything else means it did not run.
    if (result.ExitCode != 0 && violations.Count == 0)
    {
      throw new InvalidOperationException($"clang-format failed: {result.StandardError}");
    }

    return violations;
  }

  /// <summary>Reads clang-format's <c>--dry-run</c> output: one violation per line to reformat.</summary>
  public static IReadOnlyList<Violation> Parse(string output, string repositoryRoot) =>
    DiagnosticPattern().Matches(output)
      .Select(match => (
        Path: RepositoryPaths.Relative(repositoryRoot, match.Groups["path"].Value),
        Line: int.Parse(match.Groups["line"].Value, CultureInfo.InvariantCulture)))
      .Distinct()
      .Select(location => new Violation(
        Info,
        location.Path,
        location.Line,
        "Format this line: run 'make graph-engine-format'."))
      .ToArray();

  [GeneratedRegex(@"^(?<path>[^\n]+?):(?<line>\d+):\d+: (?:error|warning): code should be clang-formatted", RegexOptions.Multiline)]
  private static partial Regex DiagnosticPattern();
}
