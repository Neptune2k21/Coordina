using System.Diagnostics;
using System.Globalization;

namespace Coordina.Quality.Impact;

/// <summary>Runs the make targets of each planned suite and reports how each one went.</summary>
internal sealed class VerifyRunner(string repositoryRoot, TextWriter output)
{
  public int Run(ImpactPlan plan, bool dryRun)
  {
    if (plan.Suites.Count == 0)
    {
      output.WriteLine("Nothing to verify: no suite covers these changes. 'make check' still applies the quality rules.");
      return 0;
    }

    var results = new List<(Suite Suite, bool Passed, TimeSpan Duration)>();

    foreach (var suite in plan.Suites)
    {
      var command = $"make {string.Join(' ', suite.MakeTargets)}";
      output.WriteLine();
      output.WriteLine($"==> {suite.Name}: {command}");

      if (dryRun)
      {
        continue;
      }

      var stopwatch = Stopwatch.StartNew();
      var passed = RunMake(suite.MakeTargets);
      results.Add((suite, passed, stopwatch.Elapsed));

      if (!passed)
      {
        break;
      }
    }

    if (dryRun)
    {
      return 0;
    }

    output.WriteLine();
    output.WriteLine("Summary");

    foreach (var (suite, passed, duration) in results)
    {
      var seconds = duration.TotalSeconds.ToString("0", CultureInfo.InvariantCulture);
      output.WriteLine($"  {(passed ? "ok  " : "FAIL")}  {suite.Name,-13} {seconds}s");
    }

    foreach (var skipped in plan.Suites.Skip(results.Count))
    {
      output.WriteLine($"  skip  {skipped.Name,-13} (an earlier suite failed)");
    }

    return results.All(result => result.Passed) && results.Count == plan.Suites.Count ? 0 : 1;
  }

  private bool RunMake(IEnumerable<string> targets)
  {
    var start = new ProcessStartInfo("make") { WorkingDirectory = repositoryRoot };

    foreach (var target in targets)
    {
      start.ArgumentList.Add(target);
    }

    using var process = Process.Start(start)
      ?? throw new InvalidOperationException("Could not start make.");
    process.WaitForExit();

    return process.ExitCode == 0;
  }
}
