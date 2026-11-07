using Coordina.Quality.Baselines;
using Coordina.Quality.Checks;

namespace Coordina.Quality.Reporting;

internal sealed record CheckResult(
  string Scope,
  int FileCount,
  BaselineComparison Comparison,
  IReadOnlyList<Violation> Warnings,
  IReadOnlyList<BaselineEntry> Resolved);
