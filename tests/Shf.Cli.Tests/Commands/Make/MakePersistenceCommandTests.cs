using System.Text.Json;
using Shf.Cli.Commands.Make;
using Shf.Cli.Services;
using Spectre.Console.Cli;

namespace Shf.Cli.Tests.Commands.Make;

public class MakePersistenceCommandTests : IDisposable
{
    private readonly string _fixtureRoot;
    private readonly string _srcRoot;
    private readonly string _businessRoot;
    private readonly string _webApiRoot;

    public MakePersistenceCommandTests()
    {
        _fixtureRoot = Path.Combine(Path.GetTempPath(), "shf-cli-persistence-tests", Guid.NewGuid().ToString("N"));
        _srcRoot = Path.Combine(_fixtureRoot, "src");
        _businessRoot = Path.Combine(_srcRoot, "Business");
        _webApiRoot = Path.Combine(_srcRoot, "WebApi");
        Directory.CreateDirectory(_businessRoot);
        Directory.CreateDirectory(_webApiRoot);

        File.WriteAllText(Path.Combine(_srcRoot, "SHFramework.slnx"),
            "<Solution>\n  <Folder Name=\"/src/\">\n  </Folder>\n</Solution>\n");
        File.WriteAllText(Path.Combine(_webApiRoot, "appsettings.json"), "{\n  \"Logging\": {}\n}\n");
        File.WriteAllText(Path.Combine(_webApiRoot, "appsettings.Development.json"), "{\n  \"Logging\": {}\n}\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_fixtureRoot, recursive: true); } catch { }
    }

    [Theory]
    [InlineData("postgres", "PostgreSql", "UseNpgsql")]
    [InlineData("sqlserver", "SqlServer", "UseSqlServer")]
    [InlineData("sqlite", "Sqlite", "UseSqlite")]
    public void Generates_five_files_for_each_variant(string arg, string folderName, string useMethod)
    {
        var cmd = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakePersistenceCommand.Settings { Variant = arg });

        Assert.Equal(0, exit);
        var projRoot = Path.Combine(_srcRoot, $"Persistence.{folderName}");
        Assert.True(File.Exists(Path.Combine(projRoot, $"Persistence.{folderName}.csproj")));
        Assert.True(File.Exists(Path.Combine(projRoot, $"{folderName}DbContext.cs")));
        Assert.True(File.Exists(Path.Combine(projRoot, $"{folderName}DbContextFactory.cs")));
        Assert.True(File.Exists(Path.Combine(projRoot, $"{folderName}Options.cs")));
        Assert.True(File.Exists(Path.Combine(projRoot, $"Register{folderName}Persistence.cs")));

        var register = File.ReadAllText(Path.Combine(projRoot, $"Register{folderName}Persistence.cs"));
        Assert.Contains(useMethod, register);
    }

    [Fact]
    public void Couchbase_generates_three_files_and_no_dbcontext()
    {
        var cmd = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakePersistenceCommand.Settings { Variant = "couchbase" });

        Assert.Equal(0, exit);
        var projRoot = Path.Combine(_srcRoot, "Persistence.Couchbase");
        Assert.True(File.Exists(Path.Combine(projRoot, "Persistence.Couchbase.csproj")));
        Assert.True(File.Exists(Path.Combine(projRoot, "CouchbaseOptions.cs")));
        Assert.True(File.Exists(Path.Combine(projRoot, "RegisterCouchbasePersistence.cs")));
        Assert.False(File.Exists(Path.Combine(projRoot, "CouchbaseDbContext.cs")));
        Assert.False(File.Exists(Path.Combine(projRoot, "CouchbaseDbContextFactory.cs")));
    }

    [Fact]
    public void Couchbase_csproj_pulls_in_couchbase_packages()
    {
        var cmd = BuildCommand();

        cmd.Execute(Ctx(), new MakePersistenceCommand.Settings { Variant = "couchbase" });

        var csproj = File.ReadAllText(Path.Combine(_srcRoot, "Persistence.Couchbase", "Persistence.Couchbase.csproj"));
        Assert.Contains("CouchbaseNetClient", csproj);
        Assert.Contains("Couchbase.Extensions.DependencyInjection", csproj);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", csproj);
    }

    [Fact]
    public void Couchbase_register_uses_AddCouchbase_not_AddDbContext()
    {
        var cmd = BuildCommand();

        cmd.Execute(Ctx(), new MakePersistenceCommand.Settings { Variant = "couchbase" });

        var register = File.ReadAllText(Path.Combine(_srcRoot, "Persistence.Couchbase", "RegisterCouchbasePersistence.cs"));
        Assert.Contains("AddCouchbase", register);
        Assert.DoesNotContain("AddDbContext", register);
    }

    [Fact]
    public void Couchbase_writes_default_connection_string_to_appsettings()
    {
        var cmd = BuildCommand();

        cmd.Execute(Ctx(), new MakePersistenceCommand.Settings { Variant = "couchbase" });

        var settings = File.ReadAllText(Path.Combine(_webApiRoot, "appsettings.json"));
        Assert.Contains("couchbase://localhost", settings);
    }

    [Fact]
    public void SqlServer_localdb_flag_overrides_the_default_connection_string()
    {
        var cmd = BuildCommand();

        cmd.Execute(Ctx(), new MakePersistenceCommand.Settings { Variant = "sqlserver", UseLocalDb = true });

        var settings = File.ReadAllText(Path.Combine(_webApiRoot, "appsettings.json"));
        Assert.Contains("(localdb)", settings);
    }

    [Fact]
    public void Explicit_connection_string_wins_over_localdb_and_default()
    {
        var cmd = BuildCommand();

        cmd.Execute(Ctx(), new MakePersistenceCommand.Settings
        {
            Variant = "sqlserver",
            UseLocalDb = true,
            ConnectionString = "Server=explicit;Database=Override;",
        });

        var settings = File.ReadAllText(Path.Combine(_webApiRoot, "appsettings.json"));
        Assert.Contains("explicit", settings);
        Assert.DoesNotContain("(localdb)", settings);
    }

    [Fact]
    public void Edits_both_appsettings_files_and_slnx()
    {
        var cmd = BuildCommand();

        cmd.Execute(Ctx(), new MakePersistenceCommand.Settings { Variant = "sqlite" });

        var prod = File.ReadAllText(Path.Combine(_webApiRoot, "appsettings.json"));
        var dev = File.ReadAllText(Path.Combine(_webApiRoot, "appsettings.Development.json"));
        var slnx = File.ReadAllText(Path.Combine(_srcRoot, "SHFramework.slnx"));

        Assert.Contains("Persistence", prod);
        Assert.Contains("Persistence", dev);
        Assert.Contains("Persistence.Sqlite/Persistence.Sqlite.csproj", slnx);

        using var doc = JsonDocument.Parse(prod);
        Assert.Equal("Data Source=app.db", doc.RootElement.GetProperty("Persistence").GetProperty("ConnectionString").GetString());
    }

    [Fact]
    public void Updates_slnx_when_solution_file_is_named_after_the_service()
    {
        File.Delete(Path.Combine(_srcRoot, "SHFramework.slnx"));
        var renamedSlnx = Path.Combine(_srcRoot, "MyService.slnx");
        File.WriteAllText(renamedSlnx, "<Solution>\n  <Folder Name=\"/src/\">\n  </Folder>\n</Solution>\n");

        var cmd = BuildCommand();
        cmd.Execute(Ctx(), new MakePersistenceCommand.Settings { Variant = "sqlite" });

        var slnx = File.ReadAllText(renamedSlnx);
        Assert.Contains("Persistence.Sqlite/Persistence.Sqlite.csproj", slnx);
    }

    [Fact]
    public void Returns_nonzero_for_unknown_variant()
    {
        var cmd = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakePersistenceCommand.Settings { Variant = "mongo" });

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void Returns_nonzero_when_business_project_cannot_be_located()
    {
        var locator = Substitute.For<IProjectLocator>();
        locator.FindBusinessProject(Arg.Any<string>()).Returns((string?)null);
        var cmd = new MakePersistenceCommand(locator, new TokenTemplateRenderer(), new FileWriter());

        var exit = cmd.Execute(Ctx(), new MakePersistenceCommand.Settings { Variant = "sqlite" });

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void Dry_run_leaves_the_file_system_alone()
    {
        var cmd = BuildCommand();

        cmd.Execute(Ctx(), new MakePersistenceCommand.Settings { Variant = "sqlite", DryRun = true });

        Assert.False(Directory.Exists(Path.Combine(_srcRoot, "Persistence.Sqlite")));
        var slnx = File.ReadAllText(Path.Combine(_srcRoot, "SHFramework.slnx"));
        Assert.DoesNotContain("Persistence.Sqlite", slnx);
    }

    private MakePersistenceCommand BuildCommand()
    {
        var locator = Substitute.For<IProjectLocator>();
        locator.FindBusinessProject(Arg.Any<string>()).Returns(_businessRoot);
        locator.FindWebApiProject(Arg.Any<string>()).Returns(_webApiRoot);
        locator.FindSolutionFile(Arg.Any<string>()).Returns(call =>
        {
            var dir = call.Arg<string>();
            if (!Directory.Exists(dir)) return null;
            return Directory.EnumerateFiles(dir, "*.slnx").FirstOrDefault()
                   ?? Directory.EnumerateFiles(dir, "*.sln").FirstOrDefault();
        });
        return new MakePersistenceCommand(locator, new TokenTemplateRenderer(), new FileWriter());
    }

    private static CommandContext Ctx() => new([], Substitute.For<IRemainingArguments>(), "make:persistence", null);
}
