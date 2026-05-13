namespace Shf.Cli.Services;

public interface IAuthScaffolder
{
    /// <summary>
    /// True if a code template exists for <paramref name="slug"/>. Slices without code templates
    /// fall back to issue emission in the calling command.
    /// </summary>
    bool HasCodeTemplate(string slug);

    /// <summary>
    /// Emits the code files for <paramref name="slug"/> into <paramref name="srcRoot"/>.
    /// Returns the relative paths of every file written (or that would be written under dry-run).
    /// Existing files are skipped unless <paramref name="force"/> is true.
    /// </summary>
    IReadOnlyList<string> EmitFiles(string slug, string srcRoot, bool force, bool dryRun);

    /// <summary>
    /// Applies the wiring inserts for <paramref name="slug"/> (Program.cs / RegisterBusiness.cs etc.).
    /// Returns a list of human-readable messages describing each successful or skipped insert.
    /// </summary>
    IReadOnlyList<string> ApplyWiring(string slug, string srcRoot, bool dryRun);
}
