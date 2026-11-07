using Coordina.Quality.Checks;

namespace Coordina.Quality.Reporting;

/// <summary>
/// Writes GitHub Actions workflow commands, which show up as annotations on
/// the changed lines of a pull request.
/// </summary>
internal sealed class GitHubReporter(TextWriter output) : IReporter
{
  public void Report(CheckResult result)
  {
    foreach (var violation in result.Comparison.Added.Concat(result.Warnings))
    {
      var level = violation.Rule.Severity == Severity.Error ? "error" : "warning";
      var title = Escape($"{violation.Rule.Id} {violation.Rule.Title}", isProperty: true);
      var path = Escape(violation.Path, isProperty: true);
      var message = Escape($"{violation.Message} {violation.Rule.Rationale}", isProperty: false);

      output.WriteLine($"::{level} file={path},line={violation.Line},title={title}::{message}");
    }

    new ConsoleReporter(output).Report(result);
  }

  private static string Escape(string value, bool isProperty)
  {
    var escaped = value
      .Replace("%", "%25", StringComparison.Ordinal)
      .Replace("\r", "%0D", StringComparison.Ordinal)
      .Replace("\n", "%0A", StringComparison.Ordinal);

    return isProperty
      ? escaped
        .Replace(":", "%3A", StringComparison.Ordinal)
        .Replace(",", "%2C", StringComparison.Ordinal)
      : escaped;
  }
}
