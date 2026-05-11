using System.ComponentModel;
using Shf.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shf.Cli.Commands.Make;

public sealed class MakeMigrationCommand(
    IProjectLocator locator,
    IProcessRunner processRunner) : Command<MakeMigrationCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Migration name in PascalCase (e.g. AddForecastTable, RenameUserEmail).")]
        public required string Name { get; init; }

        [CommandOption("--persistence <PROJECT>")]
        [Description("Persistence project name (e.g. Persistence.PostgreSql). Auto-detected when there is exactly one Persistence.* project under src/.")]
        public string? Persistence { get; init; }

        [CommandOption("--output-dir <DIR>")]
        [Description("Migrations output directory relative to the persistence project. Defaults to Migrations.")]
        public string? OutputDir { get; init; }

        [CommandOption("--dry-run")]
        [Description("Print the dotnet ef command without invoking it.")]
        public bool DryRun { get; init; }

        [CommandOption("--project <PATH>")]
        [Description("Path to the Business project. Auto-detected from cwd if omitted.")]
        public string? Project { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (!IsPascalCaseIdentifier(settings.Name))
        {
            AnsiConsole.MarkupLine("[red]Migration name must be a PascalCase identifier (letters and digits, starts uppercase).[/]");
            return 1;
        }

        var businessRoot = settings.Project ?? locator.FindBusinessProject(Directory.GetCurrentDirectory());
        if (businessRoot is null)
        {
            AnsiConsole.MarkupLine("[red]Could not locate the Business project. Run from inside a Strawhats Framework service, or pass --project.[/]");
            return 1;
        }

        var srcRoot = Path.GetDirectoryName(businessRoot)!;

        var persistenceCsproj = ResolvePersistenceProject(srcRoot, settings.Persistence);
        if (persistenceCsproj is null)
        {
            return 1;
        }

        var startupCsproj = Path.Combine(srcRoot, "WebApi", "WebApi.csproj");
        if (!File.Exists(startupCsproj))
        {
            AnsiConsole.MarkupLine($"[red]Could not find {Path.GetRelativePath(Directory.GetCurrentDirectory(), startupCsproj)} (needed as the EF startup project).[/]");
            return 1;
        }

        var outputDir = settings.OutputDir ?? "Migrations";
        var args = new List<string>
        {
            "ef", "migrations", "add", settings.Name,
            "--project", persistenceCsproj,
            "--startup-project", startupCsproj,
            "--output-dir", outputDir,
        };

        AnsiConsole.MarkupLine($"[dim]→[/] [cyan]dotnet {string.Join(' ', args)}[/]");

        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine("[yellow]dry-run:[/] command not executed.");
            return 0;
        }

        var exit = processRunner.Run("dotnet", args, workingDirectory: srcRoot);
        if (exit != 0)
        {
            AnsiConsole.MarkupLine($"[red]dotnet ef migrations add exited with code {exit}.[/]");
            AnsiConsole.MarkupLine("  [dim]hint:[/] EF tooling not installed? Run [cyan]dotnet tool install -g dotnet-ef[/].");
            return exit;
        }

        AnsiConsole.MarkupLine($"[green]migration[/] [yellow]{settings.Name}[/] added to {Path.GetRelativePath(Directory.GetCurrentDirectory(), Path.GetDirectoryName(persistenceCsproj)!)}/{outputDir}/.");
        return 0;
    }

    private static string? ResolvePersistenceProject(string srcRoot, string? explicitName)
    {
        if (!string.IsNullOrEmpty(explicitName))
        {
            var candidate = Path.Combine(srcRoot, explicitName, $"{explicitName}.csproj");
            if (!File.Exists(candidate))
            {
                AnsiConsole.MarkupLine($"[red]Persistence project not found: {Path.GetRelativePath(Directory.GetCurrentDirectory(), candidate)}.[/]");
                return null;
            }
            return candidate;
        }

        var detected = Directory.EnumerateDirectories(srcRoot, "Persistence.*")
            .Select(dir => Path.Combine(dir, $"{Path.GetFileName(dir)}.csproj"))
            .Where(File.Exists)
            .ToList();

        switch (detected.Count)
        {
            case 0:
                AnsiConsole.MarkupLine("[red]No Persistence.* project found under src/. Run [cyan]shf make:persistence <variant>[/] first.[/]");
                return null;
            case 1:
                return detected[0];
            default:
                AnsiConsole.MarkupLine($"[red]Multiple Persistence projects found. Pass --persistence <name> to disambiguate:[/]");
                foreach (var csproj in detected)
                {
                    AnsiConsole.MarkupLine($"  [yellow]{Path.GetFileNameWithoutExtension(csproj)}[/]");
                }
                return null;
        }
    }

    private static bool IsPascalCaseIdentifier(string s) =>
        !string.IsNullOrEmpty(s) && char.IsAsciiLetterUpper(s[0]) && s.All(char.IsLetterOrDigit);
}
