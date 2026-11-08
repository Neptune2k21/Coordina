using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Coordina.Analyzers.Tests.Support;

/// <summary>Compiles source files with one analyzer and returns what it reports.</summary>
internal static class AnalyzerHarness
{
  // Stands in for the real type, which lives in the API.
  private const string DbContextSource =
    """
    namespace Coordina.Api.Core.Persistence;

    public sealed class CoordinaDbContext;
    """;

  private static readonly Lazy<MetadataReference[]> PlatformReferences = new(() =>
    ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
      .Split(Path.PathSeparator)
      .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
      .ToArray());

  /// <summary>
  /// Runs <typeparamref name="TAnalyzer"/> on <paramref name="files"/>, given as
  /// path and source, and returns <c>id path:line</c> for each diagnostic.
  /// </summary>
  public static async Task<string[]> RunAsync<TAnalyzer>(params (string Path, string Source)[] files)
    where TAnalyzer : DiagnosticAnalyzer, new()
  {
    var trees = files
      .Append(("src/api/core/Persistence/CoordinaDbContext.cs", DbContextSource))
      .Select(file => CSharpSyntaxTree.ParseText(file.Item2, path: file.Item1));
    var compilation = CSharpCompilation.Create(
      "Probe",
      trees,
      PlatformReferences.Value,
      new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    var errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
    Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(error => error.ToString())));

    var diagnostics = await compilation
      .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new TAnalyzer()))
      .GetAnalyzerDiagnosticsAsync();

    return diagnostics
      .Select(diagnostic =>
      {
        var span = diagnostic.Location.GetLineSpan();
        return $"{diagnostic.Id} {span.Path}:{span.StartLinePosition.Line + 1}";
      })
      .Order(StringComparer.Ordinal)
      .ToArray();
  }
}
