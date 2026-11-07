namespace Coordina.Quality.Checks;

internal sealed record Violation(
  RuleInfo Rule,
  string Path,
  int Line,
  string Message)
{
  /// <summary>
  /// Identifies a violation without its line number, so the baseline survives
  /// edits that only move code up or down.
  /// </summary>
  public string Key => $"{Rule.Id}|{Path}|{Message}";
}
