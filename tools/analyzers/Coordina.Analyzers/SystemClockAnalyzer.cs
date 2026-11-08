using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Coordina.Analyzers;

/// <summary>Reports <c>DateTime.UtcNow</c> and the like in modules (CQ6002).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SystemClockAnalyzer : ArchitectureAnalyzer
{
  private static readonly ImmutableHashSet<string> ClockProperties =
    ["System.DateTime.Now", "System.DateTime.UtcNow", "System.DateTime.Today", "System.DateTimeOffset.Now", "System.DateTimeOffset.UtcNow"];

  public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [Rules.SystemClock];

  private protected override void Analyze(FileContext file)
  {
    if (ApiNamespaces.ModuleOf(file.Namespace) is null)
    {
      return;
    }

    foreach (var (name, symbol) in file.References())
    {
      if (symbol is IPropertySymbol { IsStatic: true } property)
      {
        var fullName = $"{property.ContainingType.ToDisplayString()}.{property.Name}";

        if (ClockProperties.Contains(fullName))
        {
          file.Report(Rules.SystemClock, name.Parent ?? name, $"{property.ContainingType.Name}.{property.Name}");
        }
      }
    }
  }
}
