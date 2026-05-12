using System.ComponentModel;
using System.Text.Json;
using Shf.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shf.Cli.Commands.List;

public sealed class ListEndpointsCommand(
    IProjectLocator locator,
    IEndpointScanner scanner) : Command<ListEndpointsCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--project <PATH>")]
        [Description("Path to the WebApi project. Auto-detected from cwd if omitted.")]
        public string? Project { get; init; }

        [CommandOption("--filter <PATTERN>")]
        [Description("Substring filter applied to route and operation name (case-insensitive).")]
        public string? Filter { get; init; }

        [CommandOption("--json")]
        [Description("Emit JSON instead of a table (for scripting).")]
        public bool Json { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var webApiRoot = settings.Project ?? locator.FindWebApiProject(Directory.GetCurrentDirectory());
        if (webApiRoot is null)
        {
            AnsiConsole.MarkupLine("[red]Could not locate the WebApi project. Run from inside a Strawhats Framework service, or pass --project.[/]");
            return 1;
        }

        var endpoints = scanner.Scan(webApiRoot);

        if (!string.IsNullOrEmpty(settings.Filter))
        {
            endpoints = [.. endpoints.Where(e =>
                e.Route.Contains(settings.Filter, StringComparison.OrdinalIgnoreCase) ||
                e.Name.Contains(settings.Filter, StringComparison.OrdinalIgnoreCase))];
        }

        if (settings.Json)
        {
            var payload = endpoints.Select(e => new { method = e.Method, route = e.Route, name = e.Name, file = e.RelativePath });
            AnsiConsole.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (endpoints.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]no endpoints found[/] under {Path.GetRelativePath(Directory.GetCurrentDirectory(), webApiRoot)}");
            return 0;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold]Method[/]"))
            .AddColumn(new TableColumn("[bold]Route[/]"))
            .AddColumn(new TableColumn("[bold]Name[/]"));

        foreach (var e in endpoints)
        {
            table.AddRow(MethodMarkup(e.Method), Markup.Escape(e.Route), Markup.Escape(e.Name));
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[dim]{endpoints.Count} endpoint{(endpoints.Count == 1 ? "" : "s")} in {Path.GetRelativePath(Directory.GetCurrentDirectory(), webApiRoot)}[/]");
        return 0;
    }

    private static string MethodMarkup(string method) => method switch
    {
        "GET" => "[green]GET[/]",
        "POST" => "[yellow]POST[/]",
        "PUT" => "[blue]PUT[/]",
        "PATCH" => "[blue]PATCH[/]",
        "DELETE" => "[red]DELETE[/]",
        _ => Markup.Escape(method),
    };
}
