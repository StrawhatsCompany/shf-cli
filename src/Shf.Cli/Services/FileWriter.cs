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
}
