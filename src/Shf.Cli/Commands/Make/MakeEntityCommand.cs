using System.ComponentModel;
using Shf.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shf.Cli.Commands.Make;

public sealed class MakeEntityCommand(
    IProjectLocator locator,
    ITemplateRenderer renderer,
    IFileWriter writer) : Command<MakeEntityCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Entity identifier in the form <Domain>/<Name>, e.g. Weather/Forecast.")]
        public required string Name { get; init; }

        [CommandOption("--properties <LIST>")]
        [Description("Comma-separated Name:Type pairs, e.g. \"Title:string,Quantity:int,Notes:string?\".")]
        public string? Properties { get; init; }

        [CommandOption("--record")]
        [Description("Emit a positional record instead of a class with set-accessors.")]
        public bool AsRecord { get; init; }

        [CommandOption("--no-id")]
        [Description("Skip the default Id (Guid) property.")]
        public bool NoId { get; init; }

        [CommandOption("--no-timestamp")]
        [Description("Skip the default CreatedAt (DateTimeOffset) property.")]
        public bool NoTimestamp { get; init; }

        [CommandOption("--force")]
        [Description("Overwrite existing files.")]
        public bool Force { get; init; }

        [CommandOption("--dry-run")]
        [Description("Print what would be written without touching disk.")]
        public bool DryRun { get; init; }

        [CommandOption("--project <PATH>")]
        [Description("Path to the Domain project. Auto-detected from cwd if omitted.")]
        public string? Project { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var parts = settings.Name.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            AnsiConsole.MarkupLine("[red]Name must be of the form <Domain>/<Name> (e.g. Weather/Forecast).[/]");
            return 1;
        }

        var domain = parts[0];
        var entityName = parts[1];

        var domainRoot = settings.Project ?? locator.FindDomainProject(Directory.GetCurrentDirectory());
        if (domainRoot is null)
        {
            AnsiConsole.MarkupLine("[red]Could not locate the Domain project. Run from inside a Strawhats Framework service, or pass --project.[/]");
            return 1;
        }

        var properties = BuildPropertyList(settings);
        if (properties.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Entity has no properties. Use --properties or remove --no-id/--no-timestamp.[/]");
            return 1;
        }

        var (templateName, propertiesBlock) = settings.AsRecord
            ? ("Record.cs.sbn", string.Join(", ", properties.Select(p => $"{p.Type} {p.Name}")))
            : ("Class.cs.sbn", string.Join(Environment.NewLine, properties.Select(p => $"    public {p.Type} {p.Name} {{ get; set; }}")));

        var model = new
        {
            Domain = domain,
            Name = entityName,
            Properties = propertiesBlock,
        };

        var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "Entity", templateName);
        var targetPath = Path.Combine(domainRoot, "Entities", domain, $"{entityName}.cs");

        var rendered = renderer.Render(templatePath, model);

        try
        {
            writer.Write(targetPath, rendered, overwrite: settings.Force, dryRun: settings.DryRun);
            AnsiConsole.MarkupLine($"[green]{(settings.DryRun ? "would write" : "wrote")}[/] {Path.GetRelativePath(Directory.GetCurrentDirectory(), targetPath)}");
            AnsiConsole.MarkupLine($"  shape=[yellow]{(settings.AsRecord ? "record" : "class")}[/] properties=[yellow]{properties.Count}[/]");
        }
        catch (IOException ex)
        {
            AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
            return 1;
        }

        return 0;
    }

    private static List<Property> BuildPropertyList(Settings settings)
    {
        var list = new List<Property>();
        if (!settings.NoId) list.Add(new Property("Id", "Guid"));
        if (!settings.NoTimestamp) list.Add(new Property("CreatedAt", "DateTimeOffset"));
        list.AddRange(ParseProperties(settings.Properties));
        return list;
    }

    internal static IEnumerable<Property> ParseProperties(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) yield break;
        foreach (var raw in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = raw.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            {
                throw new ArgumentException($"Property '{raw}' must be in the form Name:Type.");
            }
            yield return new Property(parts[0], parts[1]);
        }
    }

    internal sealed record Property(string Name, string Type);
}
