using Coordina.Quality.Checks;

namespace Coordina.Quality.Rules.CSharp;

/// <summary>A C# rule about where code sits inside an API module.</summary>
internal abstract class ModuleRule : CSharpRule
{
  public override bool AppliesTo(SourceFile file) =>
    base.AppliesTo(file) && RepositoryPaths.ModuleOf(file.Path) is not null;

  /// <summary>
  /// Splits <c>src/api/modules/boards/Application/BoardRules.cs</c> into the
  /// module (<c>boards</c>), the path inside it and its top-level folder, which
  /// is empty for files at the module root.
  /// </summary>
  protected static (string Module, string PathInModule, string Layer) Locate(SourceFile file)
  {
    var module = RepositoryPaths.ModuleOf(file.Path)!;
    var pathInModule = RepositoryPaths.PathInModule(file.Path, module);
    var slash = pathInModule.IndexOf('/', StringComparison.Ordinal);

    return (module, pathInModule, slash >= 0 ? pathInModule[..slash] : string.Empty);
  }
}
