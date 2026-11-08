using System.Diagnostics;
using System.Text.Json;
using Coordina.Quality.Cli;
using Coordina.Quality.Tests.Support;

namespace Coordina.Quality.Tests.Cli;

/// <summary>Runs the tool end to end against a throwaway git repository.</summary>
public sealed class CommandLineTests : IDisposable
{
  private readonly string _root = Directory.CreateTempSubdirectory("quality-cli-").FullName;

  public CommandLineTests()
  {
    Git("init", "--quiet", "--initial-branch=master");
    Git("config", "user.email", "tests@coordina.test");
    Git("config", "user.name", "Quality Tests");
    Write(".gitignore", "node_modules/\n");
    RepositoryFixture.LinkWebDependencies(_root);
    Write("src/web/src/features/board/board-api.ts", "export const ok = 1\n");
    Git("add", ".");
    Git("commit", "--quiet", "-m", "chore: start");
  }

  public void Dispose()
  {
    Directory.Delete(_root, recursive: true);
  }

  [Fact]
  public void Check_OnlyLooksAtChangesSinceTheBase()
  {
    Git("switch", "--quiet", "-c", "feature");
    Write("src/web/src/features/board/BadName.ts", "export const ok = 1\n");

    var (exitCode, output) = Run("check", "--base", "master");

    Assert.Equal(CommandLine.Failure, exitCode);
    Assert.Contains("Checked 1 files (changed since master)", output, StringComparison.Ordinal);
    Assert.Contains("CQ2001  Rename 'BadName.ts' to kebab-case.", output, StringComparison.Ordinal);
    Assert.Contains("Affected suites", output, StringComparison.Ordinal);
  }

  [Fact]
  public void Baseline_LetsKnownErrorsPass_UntilANewOneAppears()
  {
    Write("src/web/src/features/board/BadName.ts", "export const ok = 1\n");
    Assert.Equal(CommandLine.Success, Run("baseline").ExitCode);

    Assert.Equal(CommandLine.Success, Run("check", "--all").ExitCode);

    Write("src/web/src/features/board/AnotherBadName.ts", "export const ok = 1\n");
    var (exitCode, output) = Run("check", "--all");

    Assert.Equal(CommandLine.Failure, exitCode);
    Assert.Contains("AnotherBadName.ts", output, StringComparison.Ordinal);
    Assert.DoesNotContain("'BadName.ts'", output, StringComparison.Ordinal);
  }

  [Fact]
  public void Staged_ChecksWhatIsAboutToBeCommitted()
  {
    Write("src/web/src/features/board/BadName.ts", "export const ok = 1\n");
    Assert.Equal(CommandLine.Success, Run("check", "--staged").ExitCode);

    Git("add", ".");

    Assert.Equal(CommandLine.Failure, Run("check", "--staged").ExitCode);
  }

  [Fact]
  public void GitHubFormat_WritesAnnotations()
  {
    Write("src/web/src/features/board/BadName.ts", "export const ok = 1\n");

    var (_, output) = Run("check", "--all", "--format", "github");

    Assert.Contains(
      "::error file=src/web/src/features/board/BadName.ts,line=1,title=CQ2001 Web files and folders use kebab-case::",
      output,
      StringComparison.Ordinal);
  }

  [Fact]
  public void Sarif_ListsNewAndKnownErrorsForCodeScanning()
  {
    Write("src/web/src/features/board/BadName.ts", "export const ok = 1\n");
    Assert.Equal(CommandLine.Success, Run("baseline").ExitCode);
    Write("src/web/src/features/board/AnotherBadName.ts", "export const ok = 1\n");

    var (exitCode, _) = Run("check", "--all", "--sarif", "quality.sarif");

    Assert.Equal(CommandLine.Failure, exitCode);
    using var sarif = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "quality.sarif")));
    var run = sarif.RootElement.GetProperty("runs")[0];
    var results = run.GetProperty("results").EnumerateArray()
      .Select(result => (
        Path: result.GetProperty("locations")[0].GetProperty("physicalLocation")
          .GetProperty("artifactLocation").GetProperty("uri").GetString(),
        Rule: result.GetProperty("ruleId").GetString(),
        Level: result.GetProperty("level").GetString(),
        State: result.GetProperty("baselineState").GetString()))
      .ToArray();

    Assert.Equal("2.1.0", sarif.RootElement.GetProperty("version").GetString());
    Assert.Equal(
      [
        ("src/web/src/features/board/AnotherBadName.ts", "CQ2001", "error", "new"),
        ("src/web/src/features/board/BadName.ts", "CQ2001", "error", "unchanged")
      ],
      results);
    Assert.Equal(
      ["CQ2001"],
      run.GetProperty("tool").GetProperty("driver").GetProperty("rules").EnumerateArray()
        .Select(rule => rule.GetProperty("id").GetString()));
  }

  [Fact]
  public void Check_ReportsACopyOfUnchangedCode_OnTheChangedSide()
  {
    var block = string.Join('\n', Enumerable.Range(0, 12).Select(index =>
      $"export const value{index} = compute(\"{index}\", {index} * 2, [{index}, {index + 1}])"));
    Write("src/web/src/features/board/original.ts", block + "\n");
    Git("add", ".");
    Git("commit", "--quiet", "-m", "chore: add original");
    Git("switch", "--quiet", "-c", "feature");
    Write("src/web/src/features/board/copy.ts", block + "\n");

    var (exitCode, output) = Run("check", "--base", "master");

    Assert.Equal(CommandLine.Failure, exitCode);
    Assert.Contains(
      "CQ4001  This block is repeated in src/web/src/features/board/original.ts.",
      output,
      StringComparison.Ordinal);
    Assert.DoesNotContain("original.ts\n", output, StringComparison.Ordinal);
    Assert.Equal(CommandLine.Success, Run("check", "--staged").ExitCode);
  }

  [Fact]
  public void CommitMessage_ChecksTheFileGitPasses()
  {
    Write(".git/COMMIT_EDITMSG", "Fixed stuff\n");

    var (exitCode, output) = Run("commit-msg", Path.Combine(_root, ".git/COMMIT_EDITMSG"));

    Assert.Equal(CommandLine.Failure, exitCode);
    Assert.Contains("CQ5001", output, StringComparison.Ordinal);
  }

  [Theory]
  [InlineData("unknown")]
  [InlineData("check", "--format", "xml")]
  [InlineData("check", "--all", "--staged")]
  [InlineData("verify", "--skip", "everything")]
  [InlineData("verify", "--only", "everything")]
  [InlineData("verify", "--only", "api", "--skip", "e2e")]
  public void BadUsage_ExitsWithTheUsageCode(params string[] args)
  {
    Assert.Equal(CommandLine.UsageError, Run(args).ExitCode);
  }

  [Fact]
  public void Verify_DryRun_ListsTheMakeTargetsForTheChange()
  {
    Git("switch", "--quiet", "-c", "feature");
    Write("src/web/src/features/board/board-api.ts", "export const changed = 1\n");

    var (exitCode, output) = Run("verify", "--base", "master", "--dry-run", "--skip", "e2e");

    Assert.Equal(CommandLine.Success, exitCode);
    Assert.Contains("==> web: make web-format-check web-contract-check web-lint web-typecheck web-test web-build", output, StringComparison.Ordinal);
    Assert.DoesNotContain("==> e2e", output, StringComparison.Ordinal);
    Assert.DoesNotContain("==> api", output, StringComparison.Ordinal);
  }

  [Fact]
  public void Verify_Only_RunsThatSuiteWhateverChanged()
  {
    var (exitCode, output) = Run("verify", "--only", "graph-engine", "--dry-run");

    Assert.Equal(CommandLine.Success, exitCode);
    Assert.Contains("==> graph-engine: make graph-engine-test graph-engine-sanitize graph-engine-benchmark-smoke", output, StringComparison.Ordinal);
    Assert.DoesNotContain("==> web", output, StringComparison.Ordinal);
  }

  private (int ExitCode, string Output) Run(params string[] args)
  {
    using var output = new StringWriter();
    var exitCode = CommandLine.Run(args, output, _root);

    return (exitCode, output.ToString());
  }

  private void Write(string relativePath, string content)
  {
    var path = Path.Combine(_root, relativePath);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, content);
  }

  private void Git(params string[] arguments)
  {
    var start = new ProcessStartInfo("git")
    {
      WorkingDirectory = _root,
      RedirectStandardOutput = true,
      RedirectStandardError = true
    };

    foreach (var argument in arguments)
    {
      start.ArgumentList.Add(argument);
    }

    using var process = Process.Start(start)!;
    process.WaitForExit();

    Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
  }
}
