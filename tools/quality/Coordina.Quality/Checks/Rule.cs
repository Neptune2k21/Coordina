namespace Coordina.Quality.Checks;

internal abstract class Rule
{
  public abstract RuleInfo Info { get; }

  public abstract bool AppliesTo(SourceFile file);

  public abstract IEnumerable<Violation> Check(SourceFile file);

  protected Violation At(SourceFile file, int line, string message) =>
    new(Info, file.Path, line, message);
}
