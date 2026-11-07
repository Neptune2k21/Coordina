using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Coordina.Quality.Checks;

/// <summary>A file to check, addressed by its path from the repository root.</summary>
internal sealed class SourceFile
{
  private readonly Lazy<SyntaxNode> _root;

  public SourceFile(string path, string text)
  {
    Path = path;
    Text = text;
    Lines = text.Split('\n');
    _root = new Lazy<SyntaxNode>(() => CSharpSyntaxTree
      .ParseText(text, path: path)
      .GetRoot());
  }

  /// <summary>Repository-relative path with forward slashes.</summary>
  public string Path { get; }

  public string Text { get; }

  public IReadOnlyList<string> Lines { get; }

  public string FileName => Path[(Path.LastIndexOf('/') + 1)..];

  /// <summary>The file name before its first dot: <c>board-api</c> for <c>board-api.test.ts</c>.</summary>
  public string Stem => FileName.Split('.')[0];

  public string Directory => Path.Contains('/', StringComparison.Ordinal)
    ? Path[..Path.LastIndexOf('/')]
    : string.Empty;

  /// <summary>The C# syntax tree, parsed on first use.</summary>
  public SyntaxNode Root => _root.Value;

  public static int LineOf(SyntaxToken token) =>
    token.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

  public static int LineOf(SyntaxNode node) =>
    node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
}
