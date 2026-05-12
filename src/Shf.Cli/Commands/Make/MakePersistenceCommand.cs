using System.ComponentModel;
using Shf.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shf.Cli.Commands.Make;

public sealed class MakePersistenceCommand(
    IProjectLocator locator,
    ITemplateRenderer renderer,
    IFileWriter writer) : Command<MakePersistenceCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<variant>")]
        [Description("One of: postgres, sqlserver, sqlite, couchbase.")]
        public required string Variant { get; init; }

        [CommandOption("--connection-string <CONN>")]
        [Description("Override the default connection string written to appsettings.json. If unset, a placeholder default is used.")]
        public string? ConnectionString { get; init; }

        [CommandOption("--localdb")]
        [Description("sqlserver only — use LocalDB as the default connection string. Ignored for other variants.")]
        public bool UseLocalDb { get; init; }

        [CommandOption("--force")]
        [Description("Overwrite existing files.")]
        public bool Force { get; init; }

        [CommandOption("--dry-run")]
        [Description("Print what would be written without touching disk.")]
        public bool DryRun { get; init; }

        [CommandOption("--project <PATH>")]
        [Description("Path to the Business project. Auto-detected from cwd if omitted.")]
        public string? Project { get; init; }
    }

    internal sealed record PersistenceVariant(
        string Name,
        string EfPackage,
        string EfPackageVersion,
        string? UseMethod,
        string DefaultConnectionString,
        string? LocalDbConnectionString,
        bool IsCouchbase = false);

    internal static readonly Dictionary<string, PersistenceVariant> Variants = new(StringComparer.OrdinalIgnoreCase)
    {
        ["postgres"] = new(
            Name: "PostgreSql",
            EfPackage: "Npgsql.EntityFrameworkCore.PostgreSQL",
            EfPackageVersion: "10.0.1",
            UseMethod: "UseNpgsql",
            DefaultConnectionString: "Host=localhost;Port=5432;Database=AppDb;Username=postgres;",
            LocalDbConnectionString: null),
        ["sqlserver"] = new(
            Name: "SqlServer",
            EfPackage: "Microsoft.EntityFrameworkCore.SqlServer",
            EfPackageVersion: "10.0.7",
            UseMethod: "UseSqlServer",
            DefaultConnectionString: "Server=localhost,1433;Database=AppDb;Trusted_Connection=true;TrustServerCertificate=true;",
            LocalDbConnectionString: "Server=(localdb)\\mssqllocaldb;Database=AppDb;Trusted_Connection=true;"),
        ["sqlite"] = new(
            Name: "Sqlite",
            EfPackage: "Microsoft.EntityFrameworkCore.Sqlite",
            EfPackageVersion: "10.0.7",
            UseMethod: "UseSqlite",
            DefaultConnectionString: "Data Source=app.db",
            LocalDbConnectionString: null),
        ["couchbase"] = new(
            Name: "Couchbase",
            EfPackage: "CouchbaseNetClient",
            EfPackageVersion: "3.6.5",
            UseMethod: null,
            DefaultConnectionString: "couchbase://localhost",
            LocalDbConnectionString: null,
            IsCouchbase: true),
    };

    private const string CouchbaseClientVersion = "3.6.5";
    private const string CouchbaseDiVersion = "3.6.5";

    public override int Execute(CommandContext context, Settings settings)
    {
        if (!Variants.TryGetValue(settings.Variant, out var variant))
        {
            AnsiConsole.MarkupLine($"[red]Unknown variant '{settings.Variant}'. Pick one of: postgres, sqlserver, sqlite, couchbase.[/]");
            return 1;
        }

        if (settings.UseLocalDb && variant.LocalDbConnectionString is null)
        {
            AnsiConsole.MarkupLine($"[yellow]--localdb is only meaningful for sqlserver; ignored for {settings.Variant}.[/]");
        }

        var connectionString = settings.ConnectionString
            ?? (settings.UseLocalDb ? variant.LocalDbConnectionString : null)
            ?? variant.DefaultConnectionString;

        var businessRoot = settings.Project ?? locator.FindBusinessProject(Directory.GetCurrentDirectory());
        if (businessRoot is null)
        {
            AnsiConsole.MarkupLine("[red]Could not locate the Business project. Run from inside a Strawhats Framework service, or pass --project.[/]");
            return 1;
        }

        var srcRoot = Path.GetDirectoryName(businessRoot)!;
        var projectRoot = Path.Combine(srcRoot, $"Persistence.{variant.Name}");

        var model = new
        {
            Variant = variant.Name,
            EfPackage = variant.EfPackage,
            EfPackageVersion = variant.EfPackageVersion,
            UseMethod = variant.UseMethod,
            DefaultConnectionString = connectionString,
            CouchbaseClientVersion = CouchbaseClientVersion,
            CouchbaseDiVersion = CouchbaseDiVersion,
        };

        var templatesRoot = Path.Combine(AppContext.BaseDirectory, "Templates", "Persistence");
        var templates = variant.IsCouchbase ? Path.Combine(templatesRoot, "Couchbase") : templatesRoot;
        var plan = variant.IsCouchbase
            ? new (string template, string target)[]
            {
                ("Csproj.sbn", Path.Combine(projectRoot, $"Persistence.{variant.Name}.csproj")),
                ("Options.cs.sbn", Path.Combine(projectRoot, $"{variant.Name}Options.cs")),
                ("Register.cs.sbn", Path.Combine(projectRoot, $"Register{variant.Name}Persistence.cs")),
            }
            : new (string template, string target)[]
            {
                ("Csproj.sbn", Path.Combine(projectRoot, $"Persistence.{variant.Name}.csproj")),
                ("DbContext.cs.sbn", Path.Combine(projectRoot, $"{variant.Name}DbContext.cs")),
                ("DbContextFactory.cs.sbn", Path.Combine(projectRoot, $"{variant.Name}DbContextFactory.cs")),
                ("Options.cs.sbn", Path.Combine(projectRoot, $"{variant.Name}Options.cs")),
                ("Register.cs.sbn", Path.Combine(projectRoot, $"Register{variant.Name}Persistence.cs")),
            };

        foreach (var (templateName, target) in plan)
        {
            var rendered = renderer.Render(Path.Combine(templates, templateName), model);
            try
            {
                writer.Write(target, rendered, overwrite: settings.Force, dryRun: settings.DryRun);
                AnsiConsole.MarkupLine($"[green]{(settings.DryRun ? "would write" : "wrote")}[/] {Path.GetRelativePath(Directory.GetCurrentDirectory(), target)}");
            }
            catch (IOException ex)
            {
                AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
                return 1;
            }
        }

        // Solution edit
        var slnxPath = locator.FindSolutionFile(srcRoot);
        if (slnxPath is not null)
        {
            try
            {
                writer.ApplyEdit(
                    slnxPath,
                    content => SlnxMutator.AddProjectToSrcFolder(content, $"Persistence.{variant.Name}/Persistence.{variant.Name}.csproj"),
                    settings.DryRun);
                AnsiConsole.MarkupLine($"[green]{(settings.DryRun ? "would edit" : "edited")}[/] {Path.GetRelativePath(Directory.GetCurrentDirectory(), slnxPath)} (added Persistence.{variant.Name})");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]warning:[/] could not update {Path.GetFileName(slnxPath)} ({ex.Message}); add the project manually.");
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]note:[/] no .slnx/.sln found in {Path.GetRelativePath(Directory.GetCurrentDirectory(), srcRoot)}; add the new project to your solution manually.");
        }

        // appsettings edits
        var webApiRoot = locator.FindWebApiProject(Directory.GetCurrentDirectory()) ?? Path.Combine(srcRoot, "WebApi");
        foreach (var name in new[] { "appsettings.json", "appsettings.Development.json" })
        {
            var path = Path.Combine(webApiRoot, name);
            if (!File.Exists(path)) continue;
            try
            {
                writer.ApplyEdit(
                    path,
                    content => AppSettingsMutator.AddConnectionString(content, "Persistence", connectionString),
                    settings.DryRun);
                AnsiConsole.MarkupLine($"[green]{(settings.DryRun ? "would edit" : "edited")}[/] {Path.GetRelativePath(Directory.GetCurrentDirectory(), path)} (added Persistence section)");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]warning:[/] could not update {name} ({ex.Message}).");
            }
        }

        AnsiConsole.MarkupLine($"  [dim]hint:[/] register in [cyan]Program.cs[/] with [yellow]builder.Services.Add{variant.Name}Persistence(builder.Configuration);[/]");
        if (variant.IsCouchbase)
        {
            AnsiConsole.MarkupLine($"  [dim]secrets:[/] set [yellow]Persistence:Username[/] and [yellow]Persistence:Password[/] via [yellow]dotnet user-secrets[/]; set [yellow]Persistence:BucketName[/] in appsettings.");
            AnsiConsole.MarkupLine($"  [dim]note:[/] Couchbase is not EF Core — no [yellow]make:migration[/] flow. Manage indexes / schema through the Couchbase console or SDK.");
        }
        else
        {
            AnsiConsole.MarkupLine($"  [dim]design-time:[/] set [yellow]PERSISTENCE_CONNECTION_STRING[/] or rely on the compiled-in default when running [yellow]dotnet ef migrations add[/].");
        }
        return 0;
    }
}
