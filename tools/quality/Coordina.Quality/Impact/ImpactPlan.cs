namespace Coordina.Quality.Impact;

internal sealed record ImpactPlan(
  IReadOnlyList<Suite> Suites,
  IReadOnlyDictionary<Suite, IReadOnlyCollection<string>> Reasons)
{
  public bool Includes(Suite suite) => Suites.Contains(suite);
}
