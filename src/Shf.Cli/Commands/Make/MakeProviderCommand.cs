using System.ComponentModel;
using Shf.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shf.Cli.Commands.Make;

public sealed class MakeProviderCommand(
    IProjectLocator locator,
    ITemplateRenderer renderer,
    IFileWriter writer) : Command<MakeProviderCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Provider name in PascalCase, e.g. Sms, Storage, Payment.")]
        public required string Name { get; init; }

        [CommandOption("--first-driver <DRIVER>")]
        [Description("Seed one driver immediately (e.g. Twilio for Sms). Adds it to the ProviderType enum and the factory switch.")]
        public string? FirstDriver { get; init; }

        [CommandOption("--force")]
        [Description("Overwrite existing files.")]
        public bool Force { get; init; }

        [CommandOption("--dry-run")]
        [Description("Print what would be written without touching disk.")]
        public bool DryRun { get; init; }

        [CommandOption("--project <PATH>")]
        [Description("Path to the Business project. Auto-detected from cwd if omitted; the other targets derive from it.")]
        public string? Project { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (!IsPascalCaseIdentifier(settings.Name))
        {
            AnsiConsole.MarkupLine("[red]Provider name must be a PascalCase identifier (letters and digits only, starts with an uppercase letter).[/]");
            return 1;
        }

        if (settings.FirstDriver is { Length: > 0 } && !IsPascalCaseIdentifier(settings.FirstDriver))
        {
            AnsiConsole.MarkupLine("[red]Driver name must be a PascalCase identifier.[/]");
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
        var driver = settings.FirstDriver;
        var hasDriver = !string.IsNullOrEmpty(driver);

        var model = new
        {
            Name = name,
            NameUpper = name.ToUpperInvariant(),
            Driver = driver ?? "",
            EnumMembers = hasDriver ? $"    {driver} = 0," : "",
            FactoryCases = hasDriver ? $"            {name}ProviderType.{driver} => new {driver}Provider(credential),\r\n" : "",
            DriverUsings = hasDriver ? $"using Providers.{name}.{driver};" : "",
        };

        var templates = Path.Combine(AppContext.BaseDirectory, "Templates", "Provider");
        var businessProvider = Path.Combine(businessRoot, "Providers", name);
        var providerProject = Path.Combine(srcRoot, $"Providers.{name}");

        var plan = new List<(string templateName, string target)>
        {
            ("IProvider.cs.sbn", Path.Combine(businessProvider, $"I{name}Provider.cs")),
            ("ProviderCredential.cs.sbn", Path.Combine(businessProvider, $"{name}ProviderCredential.cs")),
            ("ProviderType.cs.sbn", Path.Combine(businessProvider, $"{name}ProviderType.cs")),
            ("Csproj.sbn", Path.Combine(providerProject, $"Providers.{name}.csproj")),
            ("ProviderFactory.cs.sbn", Path.Combine(providerProject, "ProviderFactory.cs")),
            ("Register.cs.sbn", Path.Combine(providerProject, $"Register{name}Provider.cs")),
            ("ProviderResultCode.cs.sbn", Path.Combine(providerProject, $"{name}ProviderResultCode.cs")),
        };

        if (hasDriver)
        {
            plan.Add(("Driver.cs.sbn", Path.Combine(providerProject, driver!, $"{driver}Provider.cs")));
        }

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

        var slnxPath = Path.Combine(srcRoot, "SHFramework.slnx");
        if (File.Exists(slnxPath))
        {
            try
            {
                writer.ApplyEdit(
                    slnxPath,
                    content => SlnxMutator.AddProjectToSrcFolder(content, $"Providers.{name}/Providers.{name}.csproj"),
                    settings.DryRun);
                AnsiConsole.MarkupLine($"[green]{(settings.DryRun ? "would edit" : "edited")}[/] {Path.GetRelativePath(Directory.GetCurrentDirectory(), slnxPath)} (added Providers.{name})");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]warning:[/] could not update SHFramework.slnx ({ex.Message}); add `<Project Path=\"Providers.{name}/Providers.{name}.csproj\" />` manually.");
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]note:[/] SHFramework.slnx not found; add the new project to your solution manually.");
        }

        AnsiConsole.MarkupLine($"  [dim]hint:[/] register in [cyan]Program.cs[/] with [yellow]builder.Services.Add{name}Provider();[/]");
        return 0;
    }

    private static bool IsPascalCaseIdentifier(string s) =>
        !string.IsNullOrEmpty(s) && char.IsAsciiLetterUpper(s[0]) && s.All(char.IsLetterOrDigit);
}
