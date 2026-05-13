using System.Diagnostics;

namespace Shf.Cli.Services;

internal sealed class ProcessRunner : IProcessRunner
{
    public int Run(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
        };
        foreach (var arg in arguments)
        {
            info.ArgumentList.Add(arg);
        }

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName}.");
        process.WaitForExit();
        return process.ExitCode;
    }

    public ProcessRunResult RunCapturing(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
        };
        foreach (var arg in arguments)
        {
            info.ArgumentList.Add(arg);
        }

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName}.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ProcessRunResult(process.ExitCode, stdout, stderr);
    }
}
