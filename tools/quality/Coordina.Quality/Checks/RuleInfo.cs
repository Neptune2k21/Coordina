namespace Coordina.Quality.Checks;

/// <summary>
/// What a rule enforces and why. <see cref="Rationale"/> is shown with every
/// violation, so a contributor learns the convention instead of guessing it.
/// </summary>
internal sealed record RuleInfo(
  string Id,
  string Title,
  Severity Severity,
  string Rationale);
