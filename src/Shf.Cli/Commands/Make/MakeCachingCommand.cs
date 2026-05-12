using System.ComponentModel;
using Shf.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shf.Cli.Commands.Make;

public sealed class MakeCachingCommand(
    IProjectLocator locator,
    ITemplateRenderer renderer,
    IFileWriter writer) : Command<MakeCachingCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Caching provider name in PascalCase, e.g. Redis, Memcached, DistributedSqlServer.")]
        public required string Name { get; init; }

        [CommandOption("--with-package <PACKAGE>")]
        [Description("Add a NuGet PackageReference to the generated csproj (e.g. StackExchange.Redis).")]
        public string? WithPackage { get; init; }

        [CommandOption("--package-version <VERSION>")]
        [Description("Version for --with-package. Defaults to a permissive floor if omitted.")]
        public string? PackageVersion { get; init; }

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

    public override int Execute(CommandContext context, Settings settings)
    {
        if (!IsPascalCaseIdentifier(settings.Name))
        {
            AnsiConsole.MarkupLine("[red]Caching provider name must be a PascalCase identifier (letters and digits only, starts with an uppercase letter).[/]");
            return 1;
        }

        var businessRoot = settings.Project ?? locator.FindBusinessProject(Directory.GetCurrentDirectory());
        if (businessRoot is null)
        {
            AnsiConsole.MarkupLine("[red]Could not locate the Business project. Run from inside a Strawhats Framework service, or pass --project.[/]");
            return 1;
        }

        var srcRoot = Path.GetDirectoryName(businessRoot)!;
        var name = settings.Name;
        var projectRoot = Path.Combine(srcRoot, $"Caching.{name}");

        var packageReferences = BuildPackageReferences(settings.WithPackage, settings.PackageVersion);

        var model = new
        {
            Name = name,
            PackageReferences = packageReferences,
        };

        var templates = Path.Combine(AppContext.BaseDirectory, "Templates", "Caching");
        var plan = new (string template, string target)[]
        {
            ("Csproj.sbn", Path.Combine(projectRoot, $"Caching.{name}.csproj")),
            ("CacheProvider.cs.sbn", Path.Combine(projectRoot, $"{name}CacheProvider.cs")),
            ("ProviderFactory.cs.sbn", Path.Combine(projectRoot, "ProviderFactory.cs")),
            ("Register.cs.sbn", Path.Combine(projectRoot, $"Register{name}Caching.cs")),
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

        var enumPath = Path.Combine(businessRoot, "Caching", "CacheProviderType.cs");
        if (File.Exists(enumPath))
        {
            try
            {
                writer.ApplyEdit(
                    enumPath,
                    content => EnumMutator.AddMember(content, name),
                    settings.DryRun);
                AnsiConsole.MarkupLine($"[green]{(settings.DryRun ? "would edit" : "edited")}[/] {Path.GetRelativePath(Directory.GetCurrentDirectory(), enumPath)} (added CacheProviderType.{name})");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]warning:[/] could not update CacheProviderType.cs ({ex.Message}); add the enum value manually.");
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]warning:[/] {Path.GetRelativePath(Directory.GetCurrentDirectory(), enumPath)} not found; add [yellow]{name}[/] to your CacheProviderType enum manually.");
        }

        var slnxPath = locator.FindSolutionFile(srcRoot);
        if (slnxPath is not null)
        {
            try
            {
                writer.ApplyEdit(
                    slnxPath,
                    content => SlnxMutator.AddProjectToSrcFolder(content, $"Caching.{name}/Caching.{name}.csproj"),
                    settings.DryRun);
                AnsiConsole.MarkupLine($"[green]{(settings.DryRun ? "would edit" : "edited")}[/] {Path.GetRelativePath(Directory.GetCurrentDirectory(), slnxPath)} (added Caching.{name})");
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

        AnsiConsole.MarkupLine($"  [dim]hint:[/] register in [cyan]Program.cs[/] with [yellow]builder.Services.Add{name}Caching(builder.Configuration);[/]");
        AnsiConsole.MarkupLine($"  [dim]next:[/] implement the four [yellow]{name}CacheProvider[/] methods — `Get/Set/Exists/Remove` — against your {name} client.");
        return 0;
    }

    private static string BuildPackageReferences(string? package, string? version)
    {
        if (string.IsNullOrWhiteSpace(package))
        {
            return string.Empty;
        }

        var v = string.IsNullOrWhiteSpace(version) ? "*" : version;
        return $"\r\n    <ItemGroup>\r\n      <PackageReference Include=\"{package}\" Version=\"{v}\" />\r\n    </ItemGroup>\r\n";
    }

    private static bool IsPascalCaseIdentifier(string s) =>
        !string.IsNullOrEmpty(s) && char.IsAsciiLetterUpper(s[0]) && s.All(char.IsLetterOrDigit);
}
