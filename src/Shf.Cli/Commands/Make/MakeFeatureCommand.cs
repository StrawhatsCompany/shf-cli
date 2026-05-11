using System.ComponentModel;
using Shf.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shf.Cli.Commands.Make;

public sealed class MakeFeatureCommand(
    IProjectLocator locator,
    ITemplateRenderer renderer,
    IFileWriter writer) : Command<MakeFeatureCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Feature path in the form <Domain>/<Operation>, e.g. Weather/GetForecastsByCity.")]
        public required string Name { get; init; }

        [CommandOption("--command")]
        [Description("Force command (no response) shape even if the name starts with Get/List/Find.")]
        public bool ForceCommand { get; init; }

        [CommandOption("--query")]
        [Description("Force query (with response) shape regardless of name.")]
        public bool ForceQuery { get; init; }

        [CommandOption("--no-response")]
        [Description("Skip generating the Response class (only meaningful for queries).")]
        public bool NoResponse { get; init; }

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
        var parts = settings.Name.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            AnsiConsole.MarkupLine("[red]Name must be of the form <Domain>/<Operation> (e.g. Weather/GetForecastsByCity).[/]");
            return 1;
        }

        var domain = parts[0];
        var operation = parts[1];

        var businessRoot = settings.Project ?? locator.FindBusinessProject(Directory.GetCurrentDirectory());
        if (businessRoot is null)
        {
            AnsiConsole.MarkupLine("[red]Could not locate the Business project. Run from inside a Strawhats Framework service, or pass --project.[/]");
            return 1;
        }

        var isQuery = settings.ForceQuery || (!settings.ForceCommand && LooksLikeQuery(operation));
        var withResponse = isQuery && !settings.NoResponse;

        var sliceDir = Path.Combine(businessRoot, "Features", domain, operation);
        var model = new { Domain = domain, Operation = operation };

        var plan = new List<(string path, string template)>();
        if (isQuery)
        {
            plan.Add((Path.Combine(sliceDir, $"{operation}Query.cs"), "Query.cs.sbn"));
            plan.Add((Path.Combine(sliceDir, $"{operation}Handler.cs"), "QueryHandler.cs.sbn"));
            if (withResponse)
            {
                plan.Add((Path.Combine(sliceDir, $"{operation}Response.cs"), "Response.cs.sbn"));
            }
        }
        else
        {
            plan.Add((Path.Combine(sliceDir, $"{operation}Command.cs"), "Command.cs.sbn"));
            plan.Add((Path.Combine(sliceDir, $"{operation}Handler.cs"), "CommandHandler.cs.sbn"));
        }

        var templateRoot = Path.Combine(AppContext.BaseDirectory, "Templates", "Feature");

        foreach (var (path, templateName) in plan)
        {
            var rendered = renderer.Render(Path.Combine(templateRoot, templateName), model);
            try
            {
                writer.Write(path, rendered, overwrite: settings.Force, dryRun: settings.DryRun);
                AnsiConsole.MarkupLine($"[green]{(settings.DryRun ? "would write" : "wrote")}[/] {Path.GetRelativePath(Directory.GetCurrentDirectory(), path)}");
            }
            catch (IOException ex)
            {
                AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
                return 1;
            }
        }

        return 0;
    }

    private static readonly string[] QueryPrefixes = ["Get", "List", "Find", "Search", "Read", "Browse"];

    private static bool LooksLikeQuery(string operation) =>
        QueryPrefixes.Any(p => operation.StartsWith(p, StringComparison.Ordinal));
}
