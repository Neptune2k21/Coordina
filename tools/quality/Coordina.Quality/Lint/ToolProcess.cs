using System.Diagnostics;

namespace Coordina.Quality.Lint;

/// <summary>What an external tool printed, and how it exited.</summary>
internal sealed record ToolOutput(int ExitCode, string StandardOutput, string StandardError);

/// <summary>Runs the external tools the checks wrap: ESLint, jscpd, clang-format and clang-tidy.</summary>
internal static class ToolProcess
{
  public static ToolOutput Run(string executable, string workingDirectory, IEnumerable<string> arguments)
  {
    var start = new ProcessStartInfo(executable)
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
      ?? throw new InvalidOperationException($"Could not start {Path.GetFileName(executable)}.");
    var errors = process.StandardError.ReadToEndAsync();
    var output = process.StandardOutput.ReadToEnd();
    process.WaitForExit();

    return new ToolOutput(process.ExitCode, output, errors.Result);
  }
}
