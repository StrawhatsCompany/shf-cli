using System.Text.RegularExpressions;

namespace Shf.Cli.Services;

internal sealed partial class AuthTemplateLoader : IAuthTemplateLoader
{
    [GeneratedRegex(@"^---\s*\n(?<front>.*?)\n---\s*\n(?<body>.*)$", RegexOptions.Singleline | RegexOptions.Compiled)]
    private static partial Regex FrontmatterPattern();

    private static readonly string TemplatesRoot = Path.Combine(
        AppContext.BaseDirectory, "Templates", "Authentication");

    public IReadOnlyList<AuthTemplate> LoadAll()
    {
        if (!Directory.Exists(TemplatesRoot)) return [];

        var results = new List<AuthTemplate>();
        foreach (var path in Directory.EnumerateFiles(TemplatesRoot, "*.md").OrderBy(p => p, StringComparer.Ordinal))
        {
            var content = File.ReadAllText(path);
            var match = FrontmatterPattern().Match(content);
            if (!match.Success) continue;

            var front = ParseFrontmatter(match.Groups["front"].Value);
            var slug = front.GetValueOrDefault("slug", "");
            var title = front.GetValueOrDefault("title", "");
            var labels = SplitCsv(front.GetValueOrDefault("labels", ""));
            var deps = SplitCsv(front.GetValueOrDefault("depends_on", ""));

            results.Add(new AuthTemplate(slug, title, labels, deps, match.Groups["body"].Value.TrimStart()));
        }
        return results;
    }

    private static Dictionary<string, string> ParseFrontmatter(string yaml)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in yaml.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            var colon = trimmed.IndexOf(':');
            if (colon <= 0) continue;
            var key = trimmed[..colon].Trim();
            var value = trimmed[(colon + 1)..].Trim().Trim('[', ']');
            dict[key] = value;
        }
        return dict;
    }

    private static IReadOnlyList<string> SplitCsv(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
