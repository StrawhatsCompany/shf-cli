namespace Shf.Cli.Services;

public interface IProcessRunner
{
    /// <summary>
    /// Runs <paramref name="fileName"/> with the supplied arguments, inheriting stdout/stderr to the
    /// current console. Returns the child process exit code.
    /// </summary>
    int Run(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null);
}
