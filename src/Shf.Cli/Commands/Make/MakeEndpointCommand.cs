using System.ComponentModel;
using Shf.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shf.Cli.Commands.Make;

public sealed class MakeEndpointCommand(
    IProjectLocator locator,
    ITemplateRenderer renderer,
    IFileWriter writer) : Command<MakeEndpointCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Endpoint identifier in the form <Domain>/<Operation>, e.g. Weather/GetForecastsByCity.")]
        public required string Name { get; init; }

        [CommandOption("--query")]
        [Description("Force GET endpoint wired to a Query slice (Result<Response> envelope).")]
        public bool ForceQuery { get; init; }

        [CommandOption("--command")]
        [Description("Force POST endpoint wired to a Command slice (Result envelope, no response).")]
        public bool ForceCommand { get; init; }

        [CommandOption("--route <PATTERN>")]
        [Description("Route override. Defaults to api/v1/<Domain>/<Operation>.")]
        public string? Route { get; init; }

        [CommandOption("--summary <TEXT>")]
        [Description("OpenAPI summary. Defaults to the operation name humanized.")]
        public string? Summary { get; init; }

        [CommandOption("--force")]
        [Description("Overwrite existing files.")]
        public bool Force { get; init; }

        [CommandOption("--dry-run")]
        [Description("Print what would be written without touching disk.")]
        public bool DryRun { get; init; }

        [CommandOption("--project <PATH>")]
        [Description("Path to the WebApi project. Auto-detected from cwd if omitted.")]
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

        var webApiRoot = settings.Project ?? locator.FindWebApiProject(Directory.GetCurrentDirectory());
        if (webApiRoot is null)
        {
            AnsiConsole.MarkupLine("[red]Could not locate the WebApi project. Run from inside a Strawhats Framework service, or pass --project.[/]");
            return 1;
        }

        if (settings.ForceQuery && settings.ForceCommand)
        {
            AnsiConsole.MarkupLine("[red]--query and --command are mutually exclusive.[/]");
            return 1;
        }

        var isQuery = settings.ForceQuery || (!settings.ForceCommand && LooksLikeQuery(operation));
        var templateName = isQuery ? "Get.cs.sbn" : "Post.cs.sbn";

        var route = settings.Route ?? $"api/v1/{domain}/{operation}";
        var summary = settings.Summary ?? Humanize(operation);

        var model = new
        {
            Domain = domain,
            Operation = operation,
            Route = route,
            Summary = summary,
        };

        var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "Endpoint", templateName);
        var targetPath = Path.Combine(webApiRoot, "Endpoints", domain, $"{operation}Endpoint.cs");

        var rendered = renderer.Render(templatePath, model);

        try
        {
            writer.Write(targetPath, rendered, overwrite: settings.Force, dryRun: settings.DryRun);
            AnsiConsole.MarkupLine($"[green]{(settings.DryRun ? "would write" : "wrote")}[/] {Path.GetRelativePath(Directory.GetCurrentDirectory(), targetPath)}");
            AnsiConsole.MarkupLine($"  method=[yellow]{(isQuery ? "GET" : "POST")}[/] wire=[yellow]{(isQuery ? "Query+Response" : "Command")}[/] route=[yellow]{route}[/]");
            if (isQuery)
            {
                AnsiConsole.MarkupLine($"  [dim]requires:[/] [yellow]Business.Features.{domain}.{operation}.{operation}Query[/] and [yellow]{operation}Response[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"  [dim]requires:[/] [yellow]Business.Features.{domain}.{operation}.{operation}Command[/]");
            }
            AnsiConsole.MarkupLine($"  [dim]hint:[/] run [cyan]shf make:feature {domain}/{operation}[/] first if the slice does not exist yet.");
        }
        catch (IOException ex)
        {
            AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
            return 1;
        }

        return 0;
    }

    private static readonly string[] QueryPrefixes = ["Get", "List", "Find", "Search", "Read", "Browse"];

    private static bool LooksLikeQuery(string operation) =>
        QueryPrefixes.Any(p => operation.StartsWith(p, StringComparison.Ordinal));

    private static string Humanize(string operation)
    {
        // Insert spaces before each uppercase letter except the first.
        var chars = new List<char>(operation.Length + 4);
        for (var i = 0; i < operation.Length; i++)
        {
            if (i > 0 && char.IsUpper(operation[i]) && !char.IsUpper(operation[i - 1]))
            {
                chars.Add(' ');
            }
            chars.Add(i == 0 ? operation[i] : char.ToLowerInvariant(operation[i]));
        }
        return new string(chars.ToArray());
    }
}
