using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Coordina.Analyzers;

/// <summary>
/// Base for the architecture rules. Each source file is analysed once, with
/// the namespace it declares, which is where its code sits in the layering.
/// Generated code and EF migrations, generated from the composed model, are
/// skipped.
/// </summary>
public abstract class ArchitectureAnalyzer : DiagnosticAnalyzer
{
  public sealed override void Initialize(AnalysisContext context)
  {
    context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
    context.EnableConcurrentExecution();
    context.RegisterSemanticModelAction(AnalyzeFile);
  }

  private protected abstract void Analyze(FileContext file);

  private void AnalyzeFile(SemanticModelAnalysisContext context)
  {
    var tree = context.SemanticModel.SyntaxTree;

    if (IsMigration(tree.FilePath))
    {
      return;
    }

    var root = tree.GetRoot(context.CancellationToken);
    var declared = root.DescendantNodes(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
      .OfType<BaseNamespaceDeclarationSyntax>()
      .FirstOrDefault();

    if (declared is not null)
    {
      Analyze(new FileContext(context, root, declared.Name.ToString()));
    }
  }

  private static bool IsMigration(string path) =>
    path.IndexOf("/Migrations/", StringComparison.Ordinal) >= 0
    || path.IndexOf("\\Migrations\\", StringComparison.Ordinal) >= 0;

  private protected sealed class FileContext(
    SemanticModelAnalysisContext context,
    SyntaxNode root,
    string declaredNamespace)
  {
    public string Namespace { get; } = declaredNamespace;

    public SyntaxNode Root { get; } = root;

    public SemanticModel Model => context.SemanticModel;

    public CancellationToken CancellationToken => context.CancellationToken;

    public void Report(DiagnosticDescriptor rule, SyntaxNode node, params object[] arguments) =>
      context.ReportDiagnostic(Diagnostic.Create(rule, node.GetLocation(), arguments));

    /// <summary>
    /// Every name the file uses, with the symbol it refers to. <c>var</c> is
    /// left out: the expression it stands for is reported instead.
    /// </summary>
    public IEnumerable<(SimpleNameSyntax Name, ISymbol Symbol)> References()
    {
      foreach (var name in Root.DescendantNodes().OfType<SimpleNameSyntax>())
      {
        if (name is IdentifierNameSyntax { IsVar: true })
        {
          continue;
        }

        var info = Model.GetSymbolInfo(name, CancellationToken);
        var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();

        if (symbol is not null)
        {
          yield return (name, symbol);
        }
      }
    }

    /// <summary>
    /// The types the file uses, once per use: <c>Clock.Now</c> is a use of
    /// <c>Clock</c>, and an extension method call a use of its static class.
    /// </summary>
    public IEnumerable<(SimpleNameSyntax Name, INamedTypeSymbol Type)> TypeReferences()
    {
      foreach (var (name, symbol) in References())
      {
        var type = symbol as INamedTypeSymbol ?? symbol.ContainingType;

        if (type is not null && !IsQualifiedBy(name, type))
        {
          yield return (name, type);
        }
      }
    }

    private bool IsQualifiedBy(SimpleNameSyntax name, INamedTypeSymbol type)
    {
      var qualifier = name.Parent switch
      {
        MemberAccessExpressionSyntax access when access.Name == name => access.Expression,
        QualifiedNameSyntax qualified when qualified.Right == name => qualified.Left,
        _ => null
      };

      return qualifier is not null
        && SymbolEqualityComparer.Default.Equals(
          Model.GetSymbolInfo(qualifier, CancellationToken).Symbol?.OriginalDefinition,
          type.OriginalDefinition);
    }
  }
}
