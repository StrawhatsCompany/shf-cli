using System.Text.Json;
using System.Text.Json.Nodes;

namespace Shf.Cli.Services;

internal static class AppSettingsMutator
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        // Match the indentation `dotnet new webapi` produces.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Adds a top-level <paramref name="sectionName"/> object with a <c>ConnectionString</c> child to
    /// the appsettings JSON document if not already present. Idempotent — if the section already
    /// exists, the file is returned unchanged so the user's customizations survive a re-run.
    /// </summary>
    public static string AddConnectionString(string json, string sectionName, string connectionString)
    {
        var root = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidOperationException("appsettings file is not a JSON object.");

        if (root[sectionName] is not null)
        {
            return json;
        }

        root[sectionName] = new JsonObject
        {
            ["ConnectionString"] = connectionString,
        };

        return root.ToJsonString(WriteOptions) + Environment.NewLine;
    }
}
