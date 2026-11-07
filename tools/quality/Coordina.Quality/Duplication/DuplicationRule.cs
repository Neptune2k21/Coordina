using Coordina.Quality.Checks;

namespace Coordina.Quality.Duplication;

internal static class DuplicationRule
{
  public static RuleInfo Info { get; } = new(
    "CQ4001",
    "Code is not duplicated",
    Severity.Error,
    "A fix made in one copy is easily forgotten in the other. Extract the shared part into one place. If two blocks only look alike and should evolve separately, suppress the rule on that line with the reason.");

  /// <summary>
  /// Reports each side of a clone that lies in a checked file. Messages leave
  /// out line numbers, so the baseline survives code moving up or down.
  /// </summary>
  public static IReadOnlyList<Violation> ToViolations(
    IEnumerable<CodeClone> clones,
    IReadOnlySet<string> checkedPaths)
  {
    var violations = new List<Violation>();

    foreach (var clone in clones)
    {
      if (checkedPaths.Contains(clone.FirstPath))
      {
        violations.Add(At(clone.FirstPath, clone.FirstLine, clone.SecondPath));
      }

      if (checkedPaths.Contains(clone.SecondPath))
      {
        violations.Add(At(clone.SecondPath, clone.SecondLine, clone.FirstPath));
      }
    }

    return violations;
  }

  private static Violation At(string path, int line, string otherPath) =>
    new(
      Info,
      path,
      line,
      path == otherPath
        ? "This block is repeated elsewhere in this file."
        : $"This block is repeated in {otherPath}.");
}
