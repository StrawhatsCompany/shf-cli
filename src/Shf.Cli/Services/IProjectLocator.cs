namespace Shf.Cli.Services;

public interface IProjectLocator
{
    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> until it finds a directory containing
    /// a `src` folder with a `Business` project. Returns the absolute path of that `src/Business`.
    /// Returns null if not found within the walk.
    /// </summary>
    string? FindBusinessProject(string startDirectory);

    /// <summary>
    /// Walks up looking for the repo root — the first ancestor that contains a `.git` folder
    /// or a `.slnx`/`.sln` file.
    /// </summary>
    string? FindRepoRoot(string startDirectory);

    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> until it finds a directory containing
    /// a `src` folder with a `WebApi` project. Returns the absolute path of that `src/WebApi`.
    /// </summary>
    string? FindWebApiProject(string startDirectory);
}
