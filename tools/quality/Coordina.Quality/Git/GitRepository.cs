using System.Diagnostics;

namespace Coordina.Quality.Git;

/// <summary>The few git questions the tool needs answered, asked through the git CLI.</summary>
internal sealed class GitRepository
{
  private GitRepository(string root)
  {
    Root = root;
  }

  public string Root { get; }

  public static GitRepository Discover(string workingDirectory)
  {
    var root = Run(workingDirectory, "rev-parse", "--show-toplevel").Trim();

    return new GitRepository(root);
  }

  /// <summary>Tracked and untracked files that git does not ignore.</summary>
  public IReadOnlyCollection<string> AllFiles() =>
    Lines("ls-files", "--cached", "--others", "--exclude-standard")
      .Where(path => File.Exists(Path.Combine(Root, path)))
      .Distinct(StringComparer.Ordinal)
      .ToArray();

  /// <summary>
  /// Everything that differs from where this branch left <paramref name="baseRef"/>:
  /// commits on the branch, uncommitted changes and new files.
  /// </summary>
  public IReadOnlyCollection<string> ChangedFiles(string baseRef)
  {
    var mergeBase = Run(Root, "merge-base", baseRef, "HEAD").Trim();

    return Lines("diff", "--name-only", "--diff-filter=d", mergeBase)
      .Concat(Lines("ls-files", "--others", "--exclude-standard"))
      .Where(path => File.Exists(Path.Combine(Root, path)))
      .Distinct(StringComparer.Ordinal)
      .ToArray();
  }

  public IReadOnlyCollection<string> StagedFiles() =>
    Lines("diff", "--cached", "--name-only", "--diff-filter=d");

  public string ReadStaged(string path) => Run(Root, "show", $":{path}");

  public string ReadWorkingCopy(string path) => File.ReadAllText(Path.Combine(Root, path));

  public bool RefExists(string reference) =>
    TryRun(Root, out _, "rev-parse", "--verify", "--quiet", reference);

  private string[] Lines(params string[] arguments) =>
    Run(Root, arguments)
      .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

  private static string Run(string workingDirectory, params string[] arguments) =>
    TryRun(workingDirectory, out var output, arguments)
      ? output
      : throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {output}");

  private static bool TryRun(string workingDirectory, out string output, params string[] arguments)
  {
    var start = new ProcessStartInfo("git")
    {
      WorkingDirectory = workingDirectory,
      RedirectStandardOutput = true,
      RedirectStandardError = true
    };

    foreach (var argument in arguments)
    {
      start.ArgumentList.Add(argument);
    }

    using var process = Process.Start(start)
      ?? throw new InvalidOperationException("Could not start git.");
    var standardOutput = process.StandardOutput.ReadToEndAsync();
    var standardError = process.StandardError.ReadToEnd();
    process.WaitForExit();

    output = process.ExitCode == 0 ? standardOutput.Result : standardError;

    return process.ExitCode == 0;
  }
}
