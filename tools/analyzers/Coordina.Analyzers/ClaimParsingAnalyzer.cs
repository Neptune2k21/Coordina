using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Coordina.Analyzers;

/// <summary>Reports claim lookups in modules other than auth (CQ6003).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ClaimParsingAnalyzer : ArchitectureAnalyzer
{
  private const string ClaimsNamespace = "System.Security.Claims";

  public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [Rules.ClaimParsing];

  private protected override void Analyze(FileContext file)
  {
    var module = ApiNamespaces.ModuleOf(file.Namespace);

    if (module is null or "Auth")
    {
      return;
    }

    foreach (var (name, type) in file.TypeReferences())
    {
      if (type.ContainingNamespace?.ToDisplayString() == ClaimsNamespace && ReadsClaims(type, file.Model.GetSymbolInfo(name, file.CancellationToken).Symbol))
      {
        file.Report(Rules.ClaimParsing, name, name.Identifier.Text);
      }
    }
  }

  // ClaimTypes names claims; Find*, HasClaim and Claims read them, on
  // ClaimsPrincipal and ClaimsIdentity or as extensions like FindFirstValue.
  private static bool ReadsClaims(INamedTypeSymbol type, ISymbol? used) =>
    type.Name == "ClaimTypes"
    || (used is not null
      && (used.Name.StartsWith("Find", StringComparison.Ordinal) || used.Name is "HasClaim" or "Claims"));
}
