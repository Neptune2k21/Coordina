using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Coordina.Quality.Rules.CSharp;

/// <summary>Every local, parameter, loop and pattern variable a reader has to understand.</summary>
internal static class LocalNames
{
  public static IEnumerable<(SyntaxToken Identifier, string Kind)> In(SyntaxNode root)
  {
    foreach (var node in root.DescendantNodes())
    {
      switch (node)
      {
        case ParameterSyntax parameter
          when parameter.Parent?.Parent is not RecordDeclarationSyntax
            && parameter.Identifier.ValueText.Length > 0:
          yield return (parameter.Identifier, "parameter");
          break;
        case VariableDeclaratorSyntax variable
          when variable.Parent?.Parent is not FieldDeclarationSyntax
            and not EventFieldDeclarationSyntax
            && variable.Parent?.Parent is not LocalDeclarationStatementSyntax { IsConst: true }:
          yield return (variable.Identifier, "variable");
          break;
        case ForEachStatementSyntax loop:
          yield return (loop.Identifier, "loop variable");
          break;
        case SingleVariableDesignationSyntax designation:
          yield return (designation.Identifier, "variable");
          break;
        case CatchDeclarationSyntax { Identifier.ValueText.Length: > 0 } declaration:
          yield return (declaration.Identifier, "exception variable");
          break;
      }
    }
  }
}
