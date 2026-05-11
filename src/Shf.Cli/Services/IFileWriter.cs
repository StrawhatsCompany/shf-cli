namespace Shf.Cli.Services;

public interface IFileWriter
{
    /// <summary>
    /// Writes <paramref name="content"/> to <paramref name="path"/>. If <paramref name="dryRun"/>
    /// is true, no file is written. Throws if the file already exists and <paramref name="overwrite"/>
    /// is false. Returns true if a write happened (or would have, in dry-run).
    /// </summary>
    bool Write(string path, string content, bool overwrite, bool dryRun);
}
