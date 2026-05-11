using System.Text.RegularExpressions;

namespace Shf.Cli.Services;

internal static partial class SlnxMutator
{
    [GeneratedRegex(@"Path=""([^""]+)""", RegexOptions.Compiled)]
    private static partial Regex PathAttribute();

    /// <summary>
    /// Inserts <paramref name="projectRelativePath"/> into the <c>/src/</c> folder of an `.slnx`
    /// solution document. Idempotent — if the entry already exists the original content is
    /// returned unchanged. Inserts in alphabetical order to match the existing convention.
    /// </summary>
    public static string AddProjectToSrcFolder(string slnxContent, string projectRelativePath)
    {
        if (slnxContent.Contains($"Path=\"{projectRelativePath}\""))
        {
            return slnxContent;
        }

        var newline = slnxContent.Contains("\r\n") ? "\r\n" : "\n";
        var lines = slnxContent.Split(newline).ToList();

        var inSrc = false;
        var insertAt = -1;

        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains("Name=\"/src/\""))
            {
                inSrc = true;
                continue;
            }
            if (!inSrc) continue;

            if (lines[i].Contains("</Folder>"))
            {
                insertAt = i;
                break;
            }

            var match = PathAttribute().Match(lines[i]);
            if (match.Success && string.CompareOrdinal(match.Groups[1].Value, projectRelativePath) > 0)
            {
                insertAt = i;
                break;
            }
        }

        if (insertAt < 0)
        {
            throw new InvalidOperationException("Could not locate the /src/ folder in the .slnx file.");
        }

        lines.Insert(insertAt, $"    <Project Path=\"{projectRelativePath}\" />");
        return string.Join(newline, lines);
    }
}
