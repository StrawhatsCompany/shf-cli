using System.Text.Json;
using Shf.Cli.Services;

namespace Shf.Cli.Tests.Services;

public class AppSettingsMutatorTests
{
    [Fact]
    public void Adds_Persistence_section_to_existing_appsettings()
    {
        const string before = """
            {
              "Logging": {
                "LogLevel": { "Default": "Information" }
              },
              "AllowedHosts": "*"
            }
            """;

        var after = AppSettingsMutator.AddConnectionString(before, "Persistence", "Data Source=app.db");

        using var doc = JsonDocument.Parse(after);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("Persistence", out var section));
        Assert.Equal("Data Source=app.db", section.GetProperty("ConnectionString").GetString());
        // Existing keys preserved.
        Assert.True(root.TryGetProperty("Logging", out _));
        Assert.Equal("*", root.GetProperty("AllowedHosts").GetString());
    }

    [Fact]
    public void Is_idempotent_when_section_already_exists()
    {
        const string before = """
            {
              "Persistence": {
                "ConnectionString": "user-customized"
              }
            }
            """;

        var after = AppSettingsMutator.AddConnectionString(before, "Persistence", "Data Source=other.db");

        using var doc = JsonDocument.Parse(after);
        Assert.Equal("user-customized", doc.RootElement.GetProperty("Persistence").GetProperty("ConnectionString").GetString());
    }

    [Fact]
    public void Throws_when_root_is_not_an_object()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AppSettingsMutator.AddConnectionString("[]", "Persistence", "x"));
    }
}
