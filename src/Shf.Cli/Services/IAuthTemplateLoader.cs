namespace Shf.Cli.Services;

public interface IAuthTemplateLoader
{
    /// <summary>
    /// Loads every <c>Templates/Authentication/*.md</c> template that ships with the CLI.
    /// Parses YAML frontmatter for slug/title/labels/depends_on; the rest is the issue body.
    /// </summary>
    IReadOnlyList<AuthTemplate> LoadAll();
}

public sealed record AuthTemplate(
    string Slug,
    string Title,
    IReadOnlyList<string> Labels,
    IReadOnlyList<string> DependsOn,
    string Body);
