using Coordina.Quality.Checks;

namespace Coordina.Quality.Rules.CSharp;

internal sealed class ModuleLayoutRule : CSharpRule
{
  private static readonly string[] Layers = ["Application", "Contracts", "Domain", "Infrastructure"];

  public override RuleInfo Info { get; } = new(
    "CQ1201",
    "API modules keep the documented layout",
    Severity.Error,
    "Every module has the same shape (CONTRIBUTING.md, Backend Guidelines), so anyone can find business rules, HTTP shapes, the model and persistence in any module. The module root only holds <Module>Module.cs and <Module>Endpoints.cs.");

  public override bool AppliesTo(SourceFile file) =>
    base.AppliesTo(file) && RepositoryPaths.ModuleOf(file.Path) is not null;

  public override IEnumerable<Violation> Check(SourceFile file)
  {
    var module = RepositoryPaths.ModuleOf(file.Path)!;
    var inModule = RepositoryPaths.PathInModule(file.Path, module);
    var slash = inModule.IndexOf('/', StringComparison.Ordinal);

    if (slash >= 0)
    {
      var layer = inModule[..slash];

      if (!Layers.Contains(layer))
      {
        yield return At(
          file,
          1,
          $"Move this file out of '{layer}': module code goes in {string.Join(", ", Layers)}.");
      }

      yield break;
    }

    var prefix = Naming.ToPascalCase(module);
    string[] allowed = [$"{prefix}Module.cs", $"{prefix}Endpoints.cs"];

    if (!allowed.Contains(inModule))
    {
      yield return At(
        file,
        1,
        $"The root of module '{module}' only holds {string.Join(" and ", allowed)}: rename or move '{inModule}'.");
    }
  }
}
