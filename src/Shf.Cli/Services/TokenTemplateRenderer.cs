using System.Reflection;
using System.Text.RegularExpressions;

namespace Shf.Cli.Services;

/// <summary>
/// Minimal `{{ Identifier }}` substitution. Identifiers map to public properties on the model
/// (case-insensitive). Not a full template engine — we keep it tiny on purpose to avoid pulling
/// in a dependency with active security advisories.
/// </summary>
internal sealed partial class TokenTemplateRenderer : ITemplateRenderer
{
    [GeneratedRegex(@"\{\{\s*(?<id>[A-Za-z_][A-Za-z0-9_]*)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex TokenPattern();

    public string Render(string templatePath, object model)
    {
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template not found: {templatePath}", templatePath);
        }

        var template = File.ReadAllText(templatePath);
        var props = model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        return TokenPattern().Replace(template, match =>
        {
            var id = match.Groups["id"].Value;
            var prop = Array.Find(props, p => string.Equals(p.Name, id, StringComparison.OrdinalIgnoreCase));
            if (prop is null)
            {
                throw new InvalidOperationException(
                    $"Template '{templatePath}' references '{{{{ {id} }}}}' but the model has no matching property.");
            }
            return prop.GetValue(model)?.ToString() ?? "";
        });
    }
}
