namespace Shf.Cli.Services;

internal sealed class ProjectLocator : IProjectLocator
{
    public string? FindBusinessProject(string startDirectory)
    {
        foreach (var dir in WalkUp(startDirectory))
        {
            var candidate = Path.Combine(dir, "src", "Business", "Business.csproj");
            if (File.Exists(candidate))
            {
                return Path.GetDirectoryName(candidate);
            }

            candidate = Path.Combine(dir, "Business", "Business.csproj");
            if (File.Exists(candidate))
            {
                return Path.GetDirectoryName(candidate);
            }
        }
        return null;
    }

    public string? FindRepoRoot(string startDirectory)
    {
        foreach (var dir in WalkUp(startDirectory))
        {
            if (Directory.Exists(Path.Combine(dir, ".git"))) return dir;
            if (Directory.EnumerateFiles(dir, "*.slnx").Any()) return dir;
            if (Directory.EnumerateFiles(dir, "*.sln").Any()) return dir;
        }
        return null;
    }

    private static IEnumerable<string> WalkUp(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            yield return dir.FullName;
            dir = dir.Parent;
        }
    }
}
