using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Coordina.Analyzers;

/// <summary>Reports a type named <c>*Result</c> or <c>*ResultStatus</c> declared in a module (CQ6005).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ModuleResultTypeAnalyzer : ArchitectureAnalyzer
{
  public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [Rules.ModuleResultType];

  private protected override void Analyze(FileContext file)
  {
    if (ApiNamespaces.ModuleOf(file.Namespace) is null)
    {
      return;
    }

    foreach (var declaration in file.Root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
    {
      var name = declaration.Identifier.Text;

      if (name.EndsWith("Result", StringComparison.Ordinal) || name.EndsWith("ResultStatus", StringComparison.Ordinal))
      {
        file.Report(Rules.ModuleResultType, declaration, name);
      }
    }
  }
}
