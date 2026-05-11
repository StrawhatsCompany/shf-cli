using System.Text.RegularExpressions;

namespace Shf.Cli.Services;

internal static partial class EnumMutator
{
    [GeneratedRegex(@"^\s+(?<name>\w+)\s*=\s*(?<value>\d+)\s*,?\s*$", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex MemberLine();

    /// <summary>
    /// Adds <paramref name="memberName"/> to the enum body in <paramref name="source"/>, picking
    /// the next integer value above the current max. Idempotent — returns the source unchanged
    /// if the member is already declared.
    /// </summary>
    public static string AddMember(string source, string memberName)
    {
        if (Regex.IsMatch(source, $@"\b{Regex.Escape(memberName)}\s*=\s*\d+"))
        {
            return source;
        }

        var nextValue = MemberLine()
            .Matches(source)
            .Select(m => int.Parse(m.Groups["value"].Value))
            .DefaultIfEmpty(-1)
            .Max() + 1;

        var newline = source.Contains("\r\n") ? "\r\n" : "\n";
        var lines = source.Split(newline).ToList();

        var closingIndex = -1;
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (lines[i].TrimStart() == "}")
            {
                closingIndex = i;
                break;
            }
        }
        if (closingIndex < 0)
        {
            throw new InvalidOperationException("Could not locate the enum's closing brace.");
        }

        // Ensure the previous member ends with a comma (so the inserted line is syntactically valid).
        for (var i = closingIndex - 1; i >= 0; i--)
        {
            var trimmed = lines[i].TrimEnd();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            if (trimmed.EndsWith('{')) break;
            if (!trimmed.EndsWith(','))
            {
                lines[i] = trimmed + ",";
            }
            break;
        }

        lines.Insert(closingIndex, $"    {memberName} = {nextValue},");
        return string.Join(newline, lines);
    }
}
