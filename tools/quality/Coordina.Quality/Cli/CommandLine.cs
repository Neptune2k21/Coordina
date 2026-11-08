using Coordina.Quality.Baselines;
using Coordina.Quality.Checks;
using Coordina.Quality.Commits;
using Coordina.Quality.Duplication;
using Coordina.Quality.Git;
using Coordina.Quality.Impact;
using Coordina.Quality.Lint;
using Coordina.Quality.Reporting;
using Coordina.Quality.Rules;

namespace Coordina.Quality.Cli;

internal static class CommandLine
{
  public const int Success = 0;
  public const int Failure = 1;
  public const int UsageError = 2;

  private const string DefaultBase = "origin/master";

  private const string Usage =
    """
    Coordina quality checks. The same rules run locally and in CI.

    Usage:
      quality check [--all | --staged | --base <ref>] [--format console|github] [--sarif <file>]
                                   Check changed files (default: since --base, origin/master).
                                   --sarif also writes the result for GitHub code scanning.
      quality commit-msg <file>    Check a commit message (used by the commit-msg hook).
      quality affected [--base <ref>] [--format console|github]
                                   Show which suites a change needs, and why.
      quality verify [--all] [--base <ref>] [--skip <suite>...] [--dry-run]
                                   Run only the suites a change needs.
      quality baseline             Record today's errors so only new ones fail.
      quality rules                List every rule and why it exists.

    Exit codes: 0 success, 1 problems found, 2 usage error.
    """;

  public static int Run(string[] args, TextWriter output, string workingDirectory)
  {
    ArgumentNullException.ThrowIfNull(args);
    ArgumentNullException.ThrowIfNull(output);

    try
    {
      var options = CommandOptions.Parse(args.Skip(1));

      return (args.Length > 0 ? args[0] : null) switch
      {
        "check" => Check(options, output, workingDirectory),
        "commit-msg" => CommitMessage(options, output),
        "affected" => Affected(options, output, workingDirectory),
        "verify" => Verify(options, output, workingDirectory),
        "baseline" => WriteBaseline(output, workingDirectory),
        "rules" => ListRules(output),
        null or "help" or "--help" or "-h" => Help(output, Success),
        _ => Help(output, UsageError)
      };
    }
    catch (UsageException exception)
    {
      output.WriteLine(exception.Message);
      return UsageError;
    }
  }

  private static int Check(CommandOptions options, TextWriter output, string workingDirectory)
  {
    var repository = GitRepository.Discover(workingDirectory);
    var (scope, paths) = SelectFiles(repository, options);
    var checker = CreateChecker(repository);
    var files = paths
      .Where(QualityChecker.Covers)
      .Select(path => new SourceFile(
        path,
        options.Staged ? repository.ReadStaged(path) : repository.ReadWorkingCopy(path)))
      .ToArray();
    var violations = checker.Check(files);

    // jscpd and ESLint read the working tree, so the pre-commit hook, which
    // checks the staged version, leaves them to make check and CI.
    if (!options.Staged)
    {
      var duplicates = FindDuplicates(repository, files, output);
      var lint = RunEslint(repository, options.All ? null : paths, output);

      if (duplicates is null || lint is null)
      {
        return Failure;
      }

      violations = QualityChecker.Order(violations.Concat(duplicates).Concat(lint));
    }

    var baseline = Baseline.Load(repository.Root);
    var result = new CheckResult(
      scope,
      files.Length,
      baseline.Compare(violations),
      violations.Where(violation => violation.Rule.Severity == Severity.Warning).ToArray(),
      baseline.Resolved(violations, files.Select(file => file.Path).ToHashSet(StringComparer.Ordinal)));

    IReporter reporter = options.Format == "github"
      ? new GitHubReporter(output)
      : new ConsoleReporter(output);
    reporter.Report(result);

    if (options.Sarif is not null)
    {
      File.WriteAllText(Path.GetFullPath(options.Sarif, workingDirectory), SarifReport.Write(result));
    }

    if (!options.All && !options.Staged)
    {
      output.WriteLine();
      WritePlan(ImpactAnalysis.Analyze(paths), output);
    }

    return result.Comparison.Added.Count == 0 ? Success : Failure;
  }

  private static int CommitMessage(CommandOptions options, TextWriter output)
  {
    var path = options.Positional.Count > 0
      ? options.Positional[0]
      : throw new UsageException("Usage: quality commit-msg <file>");
    var violations = CommitMessageChecker.Check(File.ReadAllText(path));

    foreach (var violation in violations)
    {
      output.WriteLine($"{violation.Rule.Id}  {violation.Message}");
      output.WriteLine($"        {violation.Rule.Rationale}");
    }

    return violations.Count == 0 ? Success : Failure;
  }

  private static int Affected(CommandOptions options, TextWriter output, string workingDirectory)
  {
    var repository = GitRepository.Discover(workingDirectory);
    var plan = ImpactAnalysis.Analyze(repository.ChangedFiles(ResolveBase(repository, options)));

    WritePlan(plan, output);

    if (options.Format == "github")
    {
      WriteGitHubOutputs(plan);
    }

    return Success;
  }

  private static int Verify(CommandOptions options, TextWriter output, string workingDirectory)
  {
    var repository = GitRepository.Discover(workingDirectory);
    var plan = options.All
      ? ImpactAnalysis.Everything("--all")
      : ImpactAnalysis.Analyze(repository.ChangedFiles(ResolveBase(repository, options)));
    var unknown = options.Skip
      .Where(name => Suite.All.All(suite => suite.Name != name))
      .ToArray();

    if (unknown.Length > 0)
    {
      throw new UsageException(
        $"Unknown suite '{unknown[0]}'. Suites: {string.Join(", ", Suite.All.Select(suite => suite.Name))}.");
    }

    var filtered = plan with
    {
      Suites = plan.Suites.Where(suite => !options.Skip.Contains(suite.Name)).ToArray()
    };

    WritePlan(filtered, output);

    return new VerifyRunner(repository.Root, output).Run(filtered, options.DryRun);
  }

  private static int WriteBaseline(TextWriter output, string workingDirectory)
  {
    var repository = GitRepository.Discover(workingDirectory);
    var checker = CreateChecker(repository);
    var files = repository.AllFiles()
      .Where(QualityChecker.Covers)
      .Select(path => new SourceFile(path, repository.ReadWorkingCopy(path)))
      .ToArray();
    var duplicates = FindDuplicates(repository, files, output);

    if (duplicates is null)
    {
      return Failure;
    }

    var baseline = Baseline.From(checker.Check(files).Concat(duplicates));

    baseline.Save(repository.Root);

    output.WriteLine($"Wrote {Baseline.RelativePath}: {baseline.Entries.Sum(entry => entry.Count)} known errors.");

    foreach (var rule in baseline.Entries.GroupBy(entry => entry.Rule).OrderBy(group => group.Key, StringComparer.Ordinal))
    {
      output.WriteLine($"  {rule.Key}  {rule.Sum(entry => entry.Count)}");
    }

    return Success;
  }

  private static int ListRules(TextWriter output)
  {
    var rules = RuleCatalog.Create([]).Select(rule => rule.Info).Concat(RuleCatalog.Others);

    foreach (var rule in rules.OrderBy(rule => rule.Id, StringComparer.Ordinal))
    {
      output.WriteLine($"{rule.Id}  {(rule.Severity == Severity.Error ? "error  " : "warning")}  {rule.Title}");
      output.WriteLine($"        {rule.Rationale}");
    }

    return Success;
  }

  private static int Help(TextWriter output, int exitCode)
  {
    output.WriteLine(Usage);
    return exitCode;
  }

  private static (string Scope, IReadOnlyCollection<string> Paths) SelectFiles(
    GitRepository repository,
    CommandOptions options)
  {
    if (options.All)
    {
      return ("the whole repository", repository.AllFiles());
    }

    if (options.Staged)
    {
      return ("staged changes", repository.StagedFiles());
    }

    var baseRef = ResolveBase(repository, options);

    return ($"changed since {baseRef}", repository.ChangedFiles(baseRef));
  }

  private static string ResolveBase(GitRepository repository, CommandOptions options)
  {
    if (options.Base is not null)
    {
      return repository.RefExists(options.Base)
        ? options.Base
        : throw new UsageException($"Unknown git reference '{options.Base}'.");
    }

    return new[] { DefaultBase, "master", "HEAD" }.First(repository.RefExists);
  }

  /// <summary>
  /// Duplicates touching the checked files, with suppressions applied. Returns
  /// <c>null</c> when jscpd is missing in CI, where skipping would hide problems.
  /// </summary>
  private static Violation[]? FindDuplicates(
    GitRepository repository,
    IReadOnlyCollection<SourceFile> files,
    TextWriter output)
  {
    var detector = new DuplicationDetector(repository.Root);

    if (!detector.IsAvailable)
    {
      return ToolMissing("Duplicated code", DuplicationDetector.RelativeBinaryPath, output);
    }

    var filesByPath = files.ToDictionary(file => file.Path, StringComparer.Ordinal);

    return DuplicationRule
      .ToViolations(detector.FindClones(), filesByPath.Keys.ToHashSet(StringComparer.Ordinal))
      .GroupBy(violation => violation.Path)
      .SelectMany(group => Suppressions.Filter(filesByPath[group.Key], group))
      .ToArray();
  }

  /// <summary>
  /// ESLint findings for <paramref name="paths"/>, or for the whole web app when
  /// it is <c>null</c>. Returns <c>null</c> when ESLint is missing in CI.
  /// </summary>
  private static Violation[]? RunEslint(
    GitRepository repository,
    IReadOnlyCollection<string>? paths,
    TextWriter output)
  {
    var eslint = new EslintRunner(repository.Root);

    if (!eslint.AppliesHere)
    {
      return [];
    }

    return eslint.IsAvailable
      ? eslint.Run(paths).ToArray()
      : ToolMissing("TypeScript lint", EslintRunner.RelativeBinaryPath, output);
  }

  /// <summary>
  /// Says a tool installed with the web app is missing. Locally the check goes
  /// on without it; in CI it fails, because skipping would hide problems.
  /// </summary>
  private static Violation[]? ToolMissing(string what, string relativeBinaryPath, TextWriter output)
  {
    output.WriteLine($"{what} was not checked: {relativeBinaryPath} is missing. Run 'pnpm --dir src/web install'.");

    return Environment.GetEnvironmentVariable("CI") == "true" ? null : [];
  }

  private static QualityChecker CreateChecker(GitRepository repository) =>
    new(RuleCatalog.Create(repository.AllFiles()
      .Where(path => path.EndsWith(".csproj", StringComparison.Ordinal))
      .ToArray()));

  private static void WritePlan(ImpactPlan plan, TextWriter output)
  {
    if (plan.Suites.Count == 0)
    {
      output.WriteLine("Affected suites: none (documentation only).");
      return;
    }

    output.WriteLine("Affected suites (run them with 'make verify'):");

    foreach (var suite in plan.Suites)
    {
      output.WriteLine($"  {suite.Name,-13} {suite.Description}");
      output.WriteLine($"  {string.Empty,-13} because: {string.Join("; ", plan.Reasons[suite])}");
    }
  }

  private static void WriteGitHubOutputs(ImpactPlan plan)
  {
    var path = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");

    if (string.IsNullOrEmpty(path))
    {
      return;
    }

    var lines = Suite.All
      .Select(suite => $"{suite.Name}={(plan.Includes(suite) ? "true" : "false")}")
      .Append($"suites={string.Join(',', plan.Suites.Select(suite => suite.Name))}");

    File.AppendAllLines(path, lines);
  }
}
