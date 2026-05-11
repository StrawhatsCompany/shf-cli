using System.Text.RegularExpressions;

namespace Shf.Cli.Services;

internal static partial class FactoryMutator
{
    // Do NOT include trailing `\s*$` — that would gobble the blank line between the using block
    // and the namespace declaration, leaving the inserted import abutted against `namespace`.
    [GeneratedRegex(@"^using [^;]+;", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex UsingLine();

    [GeneratedRegex(@"^[ \t]+_\s*=>\s*throw", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex DefaultCase();

    /// <summary>
    /// Adds a switch case mapping <c>{providerName}ProviderType.{driverName}</c> to
    /// <c>new {driverName}Provider(credential)</c> before the default <c>_ =&gt;</c> case, and
    /// inserts the matching <c>using Providers.{providerName}.{driverName};</c> import. Both
    /// changes are idempotent.
    /// </summary>
    public static string AddDriverCase(string source, string providerName, string driverName)
    {
        var newline = source.Contains("\r\n") ? "\r\n" : "\n";

        var usingLine = $"using Providers.{providerName}.{driverName};";
        if (!source.Contains(usingLine))
        {
            var usings = UsingLine().Matches(source);
            if (usings.Count == 0)
            {
                throw new InvalidOperationException("Could not locate any using directives in the factory.");
            }
            var lastUsing = usings[^1];
            source = source.Insert(lastUsing.Index + lastUsing.Length, newline + usingLine);
        }

        var caseLine = $"            {providerName}ProviderType.{driverName} => new {driverName}Provider(credential),";
        if (source.Contains(caseLine))
        {
            return source;
        }

        var defaultMatch = DefaultCase().Match(source);
        if (!defaultMatch.Success)
        {
            throw new InvalidOperationException("Could not locate the default case (_ => throw ...) in the factory switch.");
        }

        // Insert the new case on its own line just above the default case, preserving the
        // default's indentation.
        var lineStart = source.LastIndexOf(newline, defaultMatch.Index, StringComparison.Ordinal) + newline.Length;
        return source.Insert(lineStart, caseLine + newline);
    }
}
