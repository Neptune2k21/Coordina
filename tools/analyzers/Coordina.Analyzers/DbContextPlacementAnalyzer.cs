using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Coordina.Analyzers;

/// <summary>Reports <c>CoordinaDbContext</c> in module code outside Infrastructure (CQ6004).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DbContextPlacementAnalyzer : ArchitectureAnalyzer
{
  private const string DbContextName = "Coordina.Api.Core.Persistence.CoordinaDbContext";

  public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [Rules.DbContextPlacement];

  private protected override void Analyze(FileContext file)
  {
    if (ApiNamespaces.ModuleOf(file.Namespace) is null || ApiNamespaces.HasSegment(file.Namespace, "Infrastructure"))
    {
      return;
    }

    foreach (var (name, type) in file.TypeReferences())
    {
      if (type.ToDisplayString() == DbContextName)
      {
        file.Report(Rules.DbContextPlacement, name);
      }
    }
  }
}
