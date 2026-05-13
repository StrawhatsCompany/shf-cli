namespace Shf.Cli.Services;

public interface IProcessRunner
{
    /// <summary>
    /// Runs <paramref name="fileName"/> with the supplied arguments, inheriting stdout/stderr to the
    /// current console. Returns the child process exit code.
    /// </summary>
    int Run(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null);

    /// <summary>
    /// Runs the process and captures stdout/stderr instead of inheriting them. Used when the
    /// caller needs to parse the output (e.g. parsing the issue URL out of <c>gh issue create</c>).
    /// </summary>
    ProcessRunResult RunCapturing(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null);
}

public sealed record ProcessRunResult(int ExitCode, string StdOut, string StdErr);
