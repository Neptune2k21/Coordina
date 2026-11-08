using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Coordina.Analyzers;

/// <summary>Reports a type or namespace from a layer the file's layer may not use (CQ6001).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LayerDependencyAnalyzer : ArchitectureAnalyzer
{
  public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [Rules.LayerDependency];

  private protected override void Analyze(FileContext file)
  {
    var (layer, forbidden) = Forbidden(file.Namespace);

    if (forbidden.IsEmpty)
    {
      return;
    }

    foreach (var directive in file.Root.DescendantNodes().OfType<UsingDirectiveSyntax>())
    {
      if (directive.Name is not null
        && file.Model.GetSymbolInfo(directive.Name, file.CancellationToken).Symbol is INamespaceSymbol imported)
      {
        ReportIfForbidden(file, directive.Name, imported.ToDisplayString(), imported.ToDisplayString(), layer, forbidden);
      }
    }

    foreach (var (name, type) in file.TypeReferences())
    {
      ReportIfForbidden(file, name, type.Name, type.ContainingNamespace?.ToDisplayString(), layer, forbidden);
    }
  }

  private static (string Layer, ImmutableArray<string> Forbidden) Forbidden(string declaredNamespace)
  {
    if (ApiNamespaces.IsIn(declaredNamespace, ApiNamespaces.Core))
    {
      return ("core", [ApiNamespaces.Shared, ApiNamespaces.Modules]);
    }

    return ApiNamespaces.IsIn(declaredNamespace, ApiNamespaces.Shared)
      ? ("shared", [ApiNamespaces.Modules])
      : ("", []);
  }

  private static void ReportIfForbidden(
    FileContext file,
    SyntaxNode node,
    string name,
    string? targetNamespace,
    string layer,
    ImmutableArray<string> forbidden)
  {
    foreach (var root in forbidden)
    {
      if (ApiNamespaces.IsIn(targetNamespace, root))
      {
        file.Report(Rules.LayerDependency, node, name, root, layer);
        return;
      }
    }
  }
}
