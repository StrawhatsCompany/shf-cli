using System.ComponentModel;
using Shf.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shf.Cli.Commands.Make;

public sealed class MakeProviderDriverCommand(
    IProjectLocator locator,
    ITemplateRenderer renderer,
    IFileWriter writer) : Command<MakeProviderDriverCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<provider>")]
        [Description("Existing provider name (e.g. Mail, Sms). Must match a Providers.<Provider> project.")]
        public required string Provider { get; init; }

        [CommandArgument(1, "<driver>")]
        [Description("New driver name in PascalCase (e.g. Smtp, Twilio, SendGrid).")]
        public required string Driver { get; init; }

        [CommandOption("--force")]
        [Description("Overwrite the driver file if it already exists.")]
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
        if (!IsPascalCaseIdentifier(settings.Provider) || !IsPascalCaseIdentifier(settings.Driver))
        {
            AnsiConsole.MarkupLine("[red]Provider and driver names must both be PascalCase identifiers.[/]");
            return 1;
        }

        var businessRoot = settings.Project ?? locator.FindBusinessProject(Directory.GetCurrentDirectory());
        if (businessRoot is null)
        {
            AnsiConsole.MarkupLine("[red]Could not locate the Business project. Run from inside a Strawhats Framework service, or pass --project.[/]");
            return 1;
        }

        var srcRoot = Path.GetDirectoryName(businessRoot)!;
        var provider = settings.Provider;
        var driver = settings.Driver;

        var providerProjectRoot = Path.Combine(srcRoot, $"Providers.{provider}");
        var enumPath = Path.Combine(businessRoot, "Providers", provider, $"{provider}ProviderType.cs");
        var factoryPath = Path.Combine(providerProjectRoot, "ProviderFactory.cs");

        if (!File.Exists(Path.Combine(providerProjectRoot, $"Providers.{provider}.csproj")))
        {
            AnsiConsole.MarkupLine($"[red]Providers.{provider} project not found. Run [cyan]shf make:provider {provider}[/] first.[/]");
            return 1;
        }
        if (!File.Exists(enumPath))
        {
            AnsiConsole.MarkupLine($"[red]Could not find {Path.GetRelativePath(Directory.GetCurrentDirectory(), enumPath)}.[/]");
            return 1;
        }
        if (!File.Exists(factoryPath))
        {
            AnsiConsole.MarkupLine($"[red]Could not find {Path.GetRelativePath(Directory.GetCurrentDirectory(), factoryPath)}.[/]");
            return 1;
        }

        var driverFile = Path.Combine(providerProjectRoot, driver, $"{driver}Provider.cs");
        var driverModel = new { Name = provider, Driver = driver };
        var rendered = renderer.Render(Path.Combine(AppContext.BaseDirectory, "Templates", "Provider", "Driver.cs.sbn"), driverModel);

        try
        {
            writer.Write(driverFile, rendered, overwrite: settings.Force, dryRun: settings.DryRun);
            AnsiConsole.MarkupLine($"[green]{(settings.DryRun ? "would write" : "wrote")}[/] {Path.GetRelativePath(Directory.GetCurrentDirectory(), driverFile)}");
        }
        catch (IOException ex)
        {
            AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
            return 1;
        }

        try
        {
            writer.ApplyEdit(enumPath, source => EnumMutator.AddMember(source, driver), settings.DryRun);
            AnsiConsole.MarkupLine($"[green]{(settings.DryRun ? "would edit" : "edited")}[/] {Path.GetRelativePath(Directory.GetCurrentDirectory(), enumPath)} (added {driver})");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to update {Path.GetRelativePath(Directory.GetCurrentDirectory(), enumPath)}: {ex.Message}[/]");
            return 1;
        }

        try
        {
            writer.ApplyEdit(factoryPath, source => FactoryMutator.AddDriverCase(source, provider, driver), settings.DryRun);
            AnsiConsole.MarkupLine($"[green]{(settings.DryRun ? "would edit" : "edited")}[/] {Path.GetRelativePath(Directory.GetCurrentDirectory(), factoryPath)} (added switch case + using)");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to update {Path.GetRelativePath(Directory.GetCurrentDirectory(), factoryPath)}: {ex.Message}[/]");
            return 1;
        }

        return 0;
    }

    private static bool IsPascalCaseIdentifier(string s) =>
        !string.IsNullOrEmpty(s) && char.IsAsciiLetterUpper(s[0]) && s.All(char.IsLetterOrDigit);
}
