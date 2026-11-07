using Coordina.Quality.Checks;

namespace Coordina.Quality.Rules.CSharp;

internal sealed class TypePlacementRule : CSharpRule
{
  private static readonly (Func<string, bool> Matches, string Layer, string Kind)[] Placements =
  [
    (name => name.EndsWith("EntityConfiguration", StringComparison.Ordinal), "Infrastructure", "EF configuration"),
    (name => name.EndsWith("Entity", StringComparison.Ordinal), "Infrastructure", "EF entity"),
    (name => name.EndsWith("Store", StringComparison.Ordinal)
      && (name.StartsWith("Postgres", StringComparison.Ordinal) || name.StartsWith("InMemory", StringComparison.Ordinal)),
      "Infrastructure", "store implementation"),
    (name => name.EndsWith("Request", StringComparison.Ordinal) || name.EndsWith("Response", StringComparison.Ordinal),
      "Contracts", "HTTP contract"),
    (name => name.EndsWith("Service", StringComparison.Ordinal), "Application", "service")
  ];

  public override RuleInfo Info { get; } = new(
    "CQ1202",
    "Types live in the layer their name announces",
    Severity.Error,
    "A *Request or *Response is an HTTP shape, an *Entity is persistence, a *Service is application logic. Putting them elsewhere breaks the layering that the architecture tests rely on.");

  public override bool AppliesTo(SourceFile file) =>
    base.AppliesTo(file) && RepositoryPaths.ModuleOf(file.Path) is not null;

  public override IEnumerable<Violation> Check(SourceFile file)
  {
    var module = RepositoryPaths.ModuleOf(file.Path)!;
    var inModule = RepositoryPaths.PathInModule(file.Path, module);
    var layer = inModule.Contains('/', StringComparison.Ordinal)
      ? inModule[..inModule.IndexOf('/', StringComparison.Ordinal)]
      : string.Empty;

    foreach (var type in TopLevelTypes(file.Root))
    {
      var name = NameOf(type);
      var placement = Placements.FirstOrDefault(candidate => candidate.Matches(name));

      if (placement.Layer is not null && placement.Layer != layer)
      {
        yield return At(
          file,
          SourceFile.LineOf(IdentifierOf(type)),
          $"Move {placement.Kind} '{name}' to {placement.Layer}.");
      }
    }
  }
}
