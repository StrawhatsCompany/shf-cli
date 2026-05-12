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

    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> until it finds a directory containing
    /// a `src` folder with a `Domain` project. Returns the absolute path of that `src/Domain`.
    /// </summary>
    string? FindDomainProject(string startDirectory);

    /// <summary>
    /// Returns the absolute path of the solution file (`.slnx` preferred, `.sln` as fallback)
    /// in <paramref name="searchDirectory"/>. The filename is service-specific in scaffolded
    /// projects (e.g. `MyService.slnx`), so callers must not assume a fixed name.
    /// Returns null if no solution file is present.
    /// </summary>
    string? FindSolutionFile(string searchDirectory);
}
