namespace Coordina.Quality.Checks;

/// <summary>Runs every applicable rule on each file and applies suppressions.</summary>
internal sealed class QualityChecker(IReadOnlyList<Rule> rules)
{
  private const int MaxFileSize = 1_000_000;

  public IReadOnlyList<Violation> Check(IEnumerable<SourceFile> files) =>
    Order(files
      .Where(file => file.Text.Length <= MaxFileSize)
      .SelectMany(file => Suppressions.Apply(
        file,
        rules
          .Where(rule => rule.AppliesTo(file))
          .SelectMany(rule => rule.Check(file)))));

  public static IReadOnlyList<Violation> Order(IEnumerable<Violation> violations) =>
    violations
      .OrderBy(violation => violation.Path, StringComparer.Ordinal)
      .ThenBy(violation => violation.Line)
      .ThenBy(violation => violation.Rule.Id, StringComparer.Ordinal)
      .ToArray();

  public static bool Covers(string path) => RepositoryPaths.IsCode(path);
}
