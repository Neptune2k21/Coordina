using Coordina.Quality.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Coordina.Quality.Rules.CSharp;

internal abstract class CSharpRule : Rule
{
  public override bool AppliesTo(SourceFile file) => RepositoryPaths.IsCSharp(file.Path);

  protected static bool IsPrivate(SyntaxTokenList modifiers) =>
    modifiers.Any(SyntaxKind.PrivateKeyword)
    || !(modifiers.Any(SyntaxKind.PublicKeyword)
      || modifiers.Any(SyntaxKind.InternalKeyword)
      || modifiers.Any(SyntaxKind.ProtectedKeyword));

  protected static bool HasAttribute(MemberDeclarationSyntax member, params string[] names) =>
    member.AttributeLists
      .SelectMany(list => list.Attributes)
      .Select(attribute => attribute.Name.ToString())
      .Any(name => names.Any(expected =>
        name == expected || name == expected + "Attribute"));

  /// <summary>Top-level types, including those inside a namespace.</summary>
  protected static IEnumerable<MemberDeclarationSyntax> TopLevelTypes(SyntaxNode root) =>
    root.ChildNodes()
      .SelectMany(node => node is BaseNamespaceDeclarationSyntax ns
        ? ns.Members.Cast<SyntaxNode>()
        : [node])
      .OfType<MemberDeclarationSyntax>()
      .Where(member => member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax);

  protected static string NameOf(MemberDeclarationSyntax type) => type switch
  {
    BaseTypeDeclarationSyntax declaration => declaration.Identifier.ValueText,
    DelegateDeclarationSyntax declaration => declaration.Identifier.ValueText,
    _ => string.Empty
  };

  protected static SyntaxToken IdentifierOf(MemberDeclarationSyntax type) => type switch
  {
    BaseTypeDeclarationSyntax declaration => declaration.Identifier,
    DelegateDeclarationSyntax declaration => declaration.Identifier,
    _ => default
  };
}
