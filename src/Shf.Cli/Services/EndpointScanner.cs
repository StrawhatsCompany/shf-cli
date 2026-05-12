using System.Text.RegularExpressions;

namespace Shf.Cli.Services;

internal sealed partial class EndpointScanner : IEndpointScanner
{
    [GeneratedRegex(@":\s*IEndpoint\b", RegexOptions.Compiled)]
    private static partial Regex IsEndpointMarker();

    [GeneratedRegex(@"Route\s*(?:=>|\{\s*get;\s*\}\s*=)\s*""([^""]+)""", RegexOptions.Compiled)]
    private static partial Regex RoutePattern();

    [GeneratedRegex(@"\.WithName\(\s*""([^""]+)""\s*\)", RegexOptions.Compiled)]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"\bapp\.Map(Get|Post|Put|Delete|Patch)\b", RegexOptions.Compiled)]
    private static partial Regex VerbPattern();

    [GeneratedRegex(@"\bapp\.MapMethods\(\s*[^,]+,\s*new\[\]\s*\{\s*([^}]+)\s*\}", RegexOptions.Compiled)]
    private static partial Regex MapMethodsPattern();

    public IReadOnlyList<EndpointInfo> Scan(string webApiRoot)
    {
        if (string.IsNullOrEmpty(webApiRoot) || !Directory.Exists(webApiRoot))
        {
            return [];
        }

        var results = new List<EndpointInfo>();

        foreach (var file in Directory.EnumerateFiles(webApiRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            string content;
            try { content = File.ReadAllText(file); }
            catch { continue; }

            if (!IsEndpointMarker().IsMatch(content)) continue;

            var route = RoutePattern().Match(content);
            var name = NamePattern().Match(content);
            if (!route.Success) continue;

            var method = ExtractMethod(content);
            var relPath = Path.GetRelativePath(webApiRoot, file);
            results.Add(new EndpointInfo(
                Method: method,
                Route: route.Groups[1].Value,
                Name: name.Success ? name.Groups[1].Value : "(unnamed)",
                RelativePath: relPath));
        }

        return results
            .OrderBy(e => e.Route, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ExtractMethod(string content)
    {
        var verb = VerbPattern().Match(content);
        if (verb.Success) return verb.Groups[1].Value.ToUpperInvariant();

        var methods = MapMethodsPattern().Match(content);
        if (methods.Success)
        {
            var verbs = methods.Groups[1].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(v => v.Trim('"', ' ').ToUpperInvariant());
            return string.Join("|", verbs);
        }

        return "?";
    }
}
