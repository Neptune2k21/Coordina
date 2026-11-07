using Coordina.Quality.Baselines;
using Coordina.Quality.Checks;

namespace Coordina.Quality.Reporting;

internal sealed class ConsoleReporter(TextWriter output) : IReporter
{
  public void Report(CheckResult result)
  {
    output.WriteLine($"Checked {result.FileCount} files ({result.Scope}).");

    var shown = result.Comparison.Added
      .Concat(result.Warnings)
      .OrderBy(violation => violation.Path, StringComparer.Ordinal)
      .ThenBy(violation => violation.Line)
      .ToArray();

    foreach (var file in shown.GroupBy(violation => violation.Path))
    {
      output.WriteLine();
      output.WriteLine(file.Key);

      foreach (var violation in file)
      {
        var level = violation.Rule.Severity == Severity.Error ? "error  " : "warning";
        output.WriteLine($"  {violation.Line,5}  {level}  {violation.Rule.Id}  {violation.Message}");
      }
    }

    var rules = shown
      .Select(violation => violation.Rule)
      .DistinctBy(rule => rule.Id)
      .OrderBy(rule => rule.Id, StringComparer.Ordinal)
      .ToArray();

    if (rules.Length > 0)
    {
      output.WriteLine();
      output.WriteLine("Why these rules:");

      foreach (var rule in rules)
      {
        output.WriteLine($"  {rule.Id}  {rule.Title}. {rule.Rationale}");
      }
    }

    output.WriteLine();
    output.WriteLine(Summary(result));

    if (result.Resolved.Count > 0)
    {
      output.WriteLine($"{result.Resolved.Count} baseline entries are fixed. Run 'make quality-baseline' to shrink the baseline.");
    }
  }

  private static string Summary(CheckResult result)
  {
    var errors = result.Comparison.Added.Count;
    var known = result.Comparison.Known.Count;
    var parts = new List<string>
    {
      errors == 0 ? "No new errors" : $"{errors} new error{(errors == 1 ? string.Empty : "s")}",
      $"{result.Warnings.Count} warning{(result.Warnings.Count == 1 ? string.Empty : "s")}"
    };

    if (known > 0)
    {
      parts.Add($"{known} known in {Baseline.RelativePath}");
    }

    return string.Join(", ", parts) + ".";
  }
}
