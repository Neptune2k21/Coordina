using Microsoft.CodeAnalysis;

namespace Coordina.Analyzers;

/// <summary>
/// The architecture rules. Like the quality tool's rules, each message says
/// what to use instead, and the description says why the rule exists.
/// </summary>
internal static class Rules
{
  private const string Category = "Architecture";
  private const string HelpLink = "https://github.com/Neptune2k21/Coordina/blob/master/CONTRIBUTING.md#architecture-rules";

  public static readonly DiagnosticDescriptor LayerDependency = Create(
    "CQ6001",
    "Layers depend only on the layers below them",
    "'{0}' is in {1}, which {2} may not depend on",
    "The API is layered modules → shared → core. Core is the kernel every module plugs into, so it never reaches into shared helpers or modules, and shared never reaches into modules.");

  public static readonly DiagnosticDescriptor SystemClock = Create(
    "CQ6002",
    "Modules read the time from TimeProvider",
    "Inject TimeProvider instead of reading {0}",
    "Reading the system clock directly makes time-dependent behavior impossible to test. TimeProvider is registered by core and faked in tests.");

  public static readonly DiagnosticDescriptor ClaimParsing = Create(
    "CQ6003",
    "Only the auth module reads claims",
    "Bind CurrentUser instead of reading claims with '{0}'",
    "Claims are an authentication detail. Core's CurrentUser gives every other module the signed-in user, so claim names and parsing live in one place.");

  public static readonly DiagnosticDescriptor DbContextPlacement = Create(
    "CQ6004",
    "Only Infrastructure uses CoordinaDbContext",
    "Use the module's store instead of CoordinaDbContext outside Infrastructure",
    "Endpoints and application code go through a store interface, so persistence stays behind one seam and can be tested and changed on its own.");

  public static readonly DiagnosticDescriptor ModuleResultType = Create(
    "CQ6005",
    "Modules use core's Result types",
    "Return Result, Result<T> and Error from core instead of declaring '{0}'",
    "One result type lets every endpoint map success and errors to HTTP the same way.");

  private static DiagnosticDescriptor Create(string id, string title, string message, string description) =>
    new(id, title, message, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description, HelpLink);
}
