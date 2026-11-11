using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Coordina.Quality.Checks;

namespace Coordina.Quality.Lint;

/// <summary>
/// Analyses the graph engine with clang-tidy, configured in <c>.clang-tidy</c>,
/// and reports its findings like any other rule (<c>clang-tidy/&lt;check&gt;</c>).
/// Only the .cpp files that cover the changed files are analysed, in parallel,
/// and only findings in the changed files are reported.
/// </summary>
internal sealed partial class ClangTidyRunner(ClangTools tools, string repositoryRoot)
{
  /// <summary>Gives clang-tidy the standard, warnings and include folders the IDE uses too.</summary>
  public const string CompileFlagsPath = "compile_flags.txt";

  private readonly ConcurrentDictionary<string, RuleInfo> _rules = new(StringComparer.Ordinal);

  /// <summary>
  /// Analyses <paramref name="paths"/>, or every engine file in
  /// <paramref name="allPaths"/> when an analysis setting changed.
  /// </summary>
  public IReadOnlyList<Violation> Run(IReadOnlyCollection<string> paths, IReadOnlyCollection<string> allPaths)
  {
    var sources = allPaths
      .Where(RepositoryPaths.IsGraphEngineSource)
      .Where(path => File.Exists(Path.Combine(repositoryRoot, path)))
      .ToArray();
    var checkedPaths = (paths.Any(ClangTools.IsConfiguration) ? sources : paths.Where(RepositoryPaths.IsGraphEngineSource))
      .ToHashSet(StringComparer.Ordinal);

    if (checkedPaths.Count == 0)
    {
      return [];
    }

    var units = IncludeGraph
      .Build(sources, IncludeDirectories(), path => File.ReadAllText(Path.Combine(repositoryRoot, path)))
      .UnitsCovering(checkedPaths);
    string[] arguments = ["--quiet", .. GccInstallArguments()];
    var outputs = new ToolOutput[units.Count];

    Parallel.For(
      0,
      units.Count,
      new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
      index => outputs[index] = ToolProcess.Run(tools.Tidy, repositoryRoot, [.. arguments, units[index]]));

    var violations = new List<Violation>();

    for (var index = 0; index < units.Count; index++)
    {
      var found = Parse(outputs[index].StandardOutput, repositoryRoot);

      // A finding makes clang-tidy exit with 1; failing without one means it could not run.
      if (outputs[index].ExitCode != 0 && found.Count == 0)
      {
        throw new InvalidOperationException(
          $"clang-tidy failed on {units[index]}: {outputs[index].StandardError}{outputs[index].StandardOutput}");
      }

      violations.AddRange(found);
    }

    // A header is analysed once per .cpp file that includes it.
    return violations
      .Where(violation => checkedPaths.Contains(violation.Path))
      .Distinct()
      .ToArray();
  }

  /// <summary>Reads clang-tidy's diagnostics into violations with repository-relative paths.</summary>
  public IReadOnlyList<Violation> Parse(string output, string root) =>
    DiagnosticPattern().Matches(output)
      .Select(match => new Violation(
        RuleFor(
          match.Groups["checks"].Value.Split(',').First(check => !check.StartsWith('-')),
          match.Groups["level"].Value == "error" ? Severity.Error : Severity.Warning),
        RepositoryPaths.Relative(root, match.Groups["path"].Value),
        int.Parse(match.Groups["line"].Value, CultureInfo.InvariantCulture),
        match.Groups["message"].Value))
      .ToArray();

  private RuleInfo RuleFor(string check, Severity severity) =>
    _rules.GetOrAdd(
      $"{check}|{severity}",
      _ => new RuleInfo($"clang-tidy/{check}", $"clang-tidy check {check}", severity, RationaleFor(check)));

  private static string RationaleFor(string check)
  {
    if (check.StartsWith("clang-diagnostic-", StringComparison.Ordinal))
    {
      return $"The compiler reports this with the flags in {CompileFlagsPath}; fix it as a build error.";
    }

    var group = check.StartsWith("clang-analyzer-", StringComparison.Ordinal)
      ? "clang-analyzer"
      : check.Split('-')[0];

    return ".clang-tidy enables this check and says why the others are off. "
      + $"What it looks for: https://clang.llvm.org/extra/clang-tidy/checks/{group}/{check[(group.Length + 1)..]}.html";
  }

  /// <summary>The <c>-I</c> folders of compile_flags.txt, which the include graph resolves against.</summary>
  private string[] IncludeDirectories()
  {
    var path = Path.Combine(repositoryRoot, CompileFlagsPath);

    return File.Exists(path)
      ? File.ReadAllLines(path)
        .Where(flag => flag.StartsWith("-I", StringComparison.Ordinal) && flag.Length > 2)
        .Select(flag => flag[2..].TrimEnd('/'))
        .ToArray()
      : [];
  }

  /// <summary>
  /// clang uses the newest GCC it finds for the C++ standard library, which can
  /// be one installed without its headers (Ubuntu ships a GCC 12 runtime next to
  /// g++ 11). Pointing it at the GCC that builds the engine analyses the same
  /// headers the build compiles against.
  /// </summary>
  private string[] GccInstallArguments()
  {
    try
    {
      var result = ToolProcess.Run("g++", repositoryRoot, ["-print-libgcc-file-name"]);
      var directory = Path.GetDirectoryName(result.StandardOutput.Trim());

      return result.ExitCode == 0
        && directory is not null
        && directory.Contains("/gcc/", StringComparison.Ordinal)
        && Directory.Exists(directory)
          ? [$"--extra-arg=--gcc-install-dir={directory}"]
          : [];
    }
    catch (Win32Exception)
    {
      // No g++: clang finds a standard library on its own.
      return [];
    }
  }

  [GeneratedRegex(@"^(?<path>[^\n]+?):(?<line>\d+):\d+: (?<level>error|warning): (?<message>.*) \[(?<checks>[\w.,-]+)\]\r?$", RegexOptions.Multiline)]
  private static partial Regex DiagnosticPattern();
}
