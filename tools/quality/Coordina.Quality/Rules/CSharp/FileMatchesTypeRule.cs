using Coordina.Quality.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Coordina.Quality.Rules.CSharp;

internal sealed class FileMatchesTypeRule : CSharpRule
{
  public override RuleInfo Info { get; } = new(
    "CQ1101",
    "A C# file is named after a type it declares",
    Severity.Error,
    "Finding a type by name should mean finding its file. A file may hold closely related types, such as a response and its nested records, as long as one of them gives the file its name. A partial type split by area may name each part Type.Area.cs.");

  public override IEnumerable<Violation> Check(SourceFile file)
  {
    var types = TopLevelTypes(file.Root).ToArray();
    var stem = file.FileName[..^".cs".Length];

    if (types.Length == 0 || types.Any(type => NameOf(type) == stem || IsPartOf(type, stem)))
    {
      yield break;
    }

    var names = string.Join(", ", types.Select(NameOf));

    yield return At(
      file,
      SourceFile.LineOf(IdentifierOf(types[0])),
      $"Name this file after one of its types: {names}.");
  }

  // PostgresBoardStore.Cards.cs holds the card part of a partial PostgresBoardStore.
  private static bool IsPartOf(MemberDeclarationSyntax type, string stem) =>
    type.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword))
    && stem.StartsWith(NameOf(type) + ".", StringComparison.Ordinal)
    && !stem[(NameOf(type).Length + 1)..].Contains('.', StringComparison.Ordinal);
}
