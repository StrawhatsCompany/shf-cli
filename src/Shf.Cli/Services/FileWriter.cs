namespace Shf.Cli.Services;

internal sealed class FileWriter : IFileWriter
{
    public bool Write(string path, string content, bool overwrite, bool dryRun)
    {
        if (File.Exists(path) && !overwrite)
        {
            throw new IOException($"File already exists: {path}. Pass --force to overwrite.");
        }

        if (dryRun)
        {
            return true;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return true;
    }

    public string ApplyEdit(string path, Func<string, string> transform, bool dryRun)
    {
        if (!File.Exists(path))
        {
            throw new IOException($"File not found: {path}.");
        }

        var original = File.ReadAllText(path);
        var updated = transform(original);

        if (!dryRun && !ReferenceEquals(original, updated) && original != updated)
        {
            File.WriteAllText(path, updated);
        }

        return updated;
    }
}
