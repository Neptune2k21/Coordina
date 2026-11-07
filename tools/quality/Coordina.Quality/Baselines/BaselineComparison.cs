using Coordina.Quality.Checks;

namespace Coordina.Quality.Baselines;

internal sealed record BaselineComparison(
  IReadOnlyList<Violation> Known,
  IReadOnlyList<Violation> Added);
