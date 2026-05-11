using Shf.Cli.Commands.Make;
using Shf.Cli.Services;
using Spectre.Console.Cli;

namespace Shf.Cli.Tests.Commands.Make;

public class MakeMigrationCommandTests : IDisposable
{
    private readonly string _fixtureRoot;
    private readonly string _srcRoot;
    private readonly string _businessRoot;
    private readonly string _webApiRoot;

    public MakeMigrationCommandTests()
    {
        _fixtureRoot = Path.Combine(Path.GetTempPath(), "shf-cli-migration-tests", Guid.NewGuid().ToString("N"));
        _srcRoot = Path.Combine(_fixtureRoot, "src");
        _businessRoot = Path.Combine(_srcRoot, "Business");
        _webApiRoot = Path.Combine(_srcRoot, "WebApi");
        Directory.CreateDirectory(_businessRoot);
        Directory.CreateDirectory(_webApiRoot);
        File.WriteAllText(Path.Combine(_webApiRoot, "WebApi.csproj"), "<Project/>");
    }

    public void Dispose()
    {
        try { Directory.Delete(_fixtureRoot, recursive: true); } catch { }
    }

    private void AddPersistence(string folderName)
    {
        var dir = Path.Combine(_srcRoot, folderName);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{folderName}.csproj"), "<Project/>");
    }

    [Fact]
    public void Auto_detects_the_single_persistence_project()
    {
        AddPersistence("Persistence.Sqlite");
        var (cmd, runner) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeMigrationCommand.Settings { Name = "AddForecastTable" });

        Assert.Equal(0, exit);
        runner.Received(1).Run(
            "dotnet",
            Arg.Is<IReadOnlyList<string>>(a =>
                a.Contains("ef") &&
                a.Contains("migrations") &&
                a.Contains("add") &&
                a.Contains("AddForecastTable") &&
                a.Any(x => x.EndsWith("Persistence.Sqlite.csproj")) &&
                a.Any(x => x.EndsWith("WebApi.csproj"))),
            Arg.Any<string>());
    }

    [Fact]
    public void Returns_nonzero_when_no_persistence_project_exists()
    {
        var (cmd, runner) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeMigrationCommand.Settings { Name = "Init" });

        Assert.NotEqual(0, exit);
        runner.DidNotReceiveWithAnyArgs().Run(default!, default!, default);
    }

    [Fact]
    public void Returns_nonzero_when_multiple_persistence_projects_without_explicit_flag()
    {
        AddPersistence("Persistence.Sqlite");
        AddPersistence("Persistence.PostgreSql");
        var (cmd, runner) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeMigrationCommand.Settings { Name = "Init" });

        Assert.NotEqual(0, exit);
        runner.DidNotReceiveWithAnyArgs().Run(default!, default!, default);
    }

    [Fact]
    public void Persistence_flag_disambiguates_between_multiple_projects()
    {
        AddPersistence("Persistence.Sqlite");
        AddPersistence("Persistence.PostgreSql");
        var (cmd, runner) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeMigrationCommand.Settings { Name = "Init", Persistence = "Persistence.PostgreSql" });

        Assert.Equal(0, exit);
        runner.Received(1).Run(
            "dotnet",
            Arg.Is<IReadOnlyList<string>>(a => a.Any(x => x.EndsWith("Persistence.PostgreSql.csproj"))),
            Arg.Any<string>());
    }

    [Fact]
    public void Output_dir_flag_is_passed_through()
    {
        AddPersistence("Persistence.Sqlite");
        var (cmd, runner) = BuildCommand();

        cmd.Execute(Ctx(), new MakeMigrationCommand.Settings { Name = "Init", OutputDir = "Db/Migrations" });

        runner.Received(1).Run(
            "dotnet",
            Arg.Is<IReadOnlyList<string>>(a => a.Contains("--output-dir") && a.Contains("Db/Migrations")),
            Arg.Any<string>());
    }

    [Fact]
    public void Default_output_dir_is_Migrations()
    {
        AddPersistence("Persistence.Sqlite");
        var (cmd, runner) = BuildCommand();

        cmd.Execute(Ctx(), new MakeMigrationCommand.Settings { Name = "Init" });

        runner.Received(1).Run(
            "dotnet",
            Arg.Is<IReadOnlyList<string>>(a => a.Contains("--output-dir") && a.Contains("Migrations")),
            Arg.Any<string>());
    }

    [Fact]
    public void Dry_run_skips_the_process_invocation()
    {
        AddPersistence("Persistence.Sqlite");
        var (cmd, runner) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeMigrationCommand.Settings { Name = "Init", DryRun = true });

        Assert.Equal(0, exit);
        runner.DidNotReceiveWithAnyArgs().Run(default!, default!, default);
    }

    [Fact]
    public void Returns_nonzero_when_name_is_not_pascal_case()
    {
        AddPersistence("Persistence.Sqlite");
        var (cmd, _) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeMigrationCommand.Settings { Name = "add_forecast_table" });

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void Explicit_persistence_that_does_not_exist_fails()
    {
        AddPersistence("Persistence.Sqlite");
        var (cmd, runner) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeMigrationCommand.Settings { Name = "Init", Persistence = "Persistence.Mongo" });

        Assert.NotEqual(0, exit);
        runner.DidNotReceiveWithAnyArgs().Run(default!, default!, default);
    }

    [Fact]
    public void Returns_nonzero_when_WebApi_project_is_missing()
    {
        AddPersistence("Persistence.Sqlite");
        File.Delete(Path.Combine(_webApiRoot, "WebApi.csproj"));
        var (cmd, runner) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeMigrationCommand.Settings { Name = "Init" });

        Assert.NotEqual(0, exit);
        runner.DidNotReceiveWithAnyArgs().Run(default!, default!, default);
    }

    [Fact]
    public void Propagates_non_zero_exit_code_from_dotnet_ef()
    {
        AddPersistence("Persistence.Sqlite");
        var (cmd, runner) = BuildCommand();
        runner.Run(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>()).Returns(42);

        var exit = cmd.Execute(Ctx(), new MakeMigrationCommand.Settings { Name = "Init" });

        Assert.Equal(42, exit);
    }

    private (MakeMigrationCommand cmd, IProcessRunner runner) BuildCommand()
    {
        var locator = Substitute.For<IProjectLocator>();
        locator.FindBusinessProject(Arg.Any<string>()).Returns(_businessRoot);
        var runner = Substitute.For<IProcessRunner>();
        runner.Run(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>()).Returns(0);
        return (new MakeMigrationCommand(locator, runner), runner);
    }

    private static CommandContext Ctx() => new([], Substitute.For<IRemainingArguments>(), "make:migration", null);
}
