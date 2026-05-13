using System.ComponentModel;
using Shf.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shf.Cli.Commands.Make;

public sealed class MakeAuthenticationCommand(
    IAuthTemplateLoader templates,
    IAuthScaffolder scaffolder,
    IProjectLocator locator) : Command<MakeAuthenticationCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--types <CSV>")]
        [Description("Comma-separated auth types to scaffold (identity,jwt,refresh,apikey,mfa-totp,mfa-email,mfa-sms,sso). Use 'all' for everything. Omit to pick interactively.")]
        public string? Types { get; init; }

        [CommandOption("--tenant")]
        [Description("Include multi-tenancy (TenantId on every owned entity).")]
        public bool Tenant { get; init; }

        [CommandOption("--no-tenant")]
        [Description("Skip multi-tenancy. Cannot be combined with --tenant.")]
        public bool NoTenant { get; init; }

        [CommandOption("--force")]
        [Description("Overwrite existing files.")]
        public bool Force { get; init; }

        [CommandOption("--dry-run")]
        [Description("Print the plan without writing files or modifying Program.cs / RegisterBusiness.cs.")]
        public bool DryRun { get; init; }
    }

    private static readonly (string Key, string Label, string[] DependsOn)[] AuthTypes =
    [
        ("identity",  "User + Role + Permission (identity core)",      []),
        ("jwt",       "JWT login (password auth)",                     ["identity"]),
        ("refresh",   "Sessions + Refresh tokens",                     ["jwt"]),
        ("apikey",    "API keys",                                      ["identity"]),
        ("mfa-totp",  "MFA: TOTP (authenticator app)",                 ["jwt"]),
        ("mfa-email", "MFA: Email (one-time code)",                    ["jwt"]),
        ("mfa-sms",   "MFA: SMS (one-time code, via Twilio)",          ["jwt"]),
        ("sso",       "SSO (OIDC) — admin-registered providers",       ["refresh"]),
    ];

    public override int Execute(CommandContext context, Settings settings)
    {
        if (settings.Tenant && settings.NoTenant)
        {
            AnsiConsole.MarkupLine("[red]--tenant and --no-tenant are mutually exclusive.[/]");
            return 1;
        }

        var businessRoot = locator.FindBusinessProject(Directory.GetCurrentDirectory());
        if (businessRoot is null)
        {
            AnsiConsole.MarkupLine("[red]Could not locate the Business project. Run from inside a Strawhats Framework service.[/]");
            return 1;
        }
        var srcRoot = Path.GetDirectoryName(businessRoot)!;

        // 1. Select types — flags first, then interactive.
        HashSet<string> selected;
        if (!string.IsNullOrEmpty(settings.Types))
        {
            selected = ParseTypes(settings.Types);
            if (selected.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]--types produced no valid keys. Known: {string.Join(", ", AuthTypes.Select(t => t.Key))}, or 'all'.[/]");
                return 1;
            }
        }
        else
        {
            selected = PromptForTypes();
            if (selected.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No auth types selected. Nothing to do.[/]");
                return 0;
            }
        }

        // 2. Resolve dependencies.
        var resolved = ResolveDependencies(selected);
        var added = resolved.Except(selected).ToList();
        if (added.Count > 0)
        {
            AnsiConsole.MarkupLine($"[dim]Auto-including dependencies: {string.Join(", ", added)}[/]");
        }

        // 3. Tenant decision.
        bool includeTenant;
        if (settings.Tenant) includeTenant = true;
        else if (settings.NoTenant) includeTenant = false;
        else includeTenant = AnsiConsole.Confirm("Include multi-tenancy (TenantId on every entity)?", defaultValue: true);

        // 4. Build the final ordered slug list (foundations first, then tenant if opted in, then deps order).
        var ordered = new List<string> { "foundations" };
        if (includeTenant) ordered.Add("tenant");
        ordered.AddRange(TopologicalOrder(resolved));

        // 5. Show plan.
        AnsiConsole.Write(new Rule("[bold]Authentication scaffolding plan[/]").LeftJustified());
        var byTitle = templates.LoadAll().ToDictionary(t => t.Slug, StringComparer.OrdinalIgnoreCase);
        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn("[bold]#[/]")
            .AddColumn("[bold]Slug[/]")
            .AddColumn("[bold]Title[/]")
            .AddColumn("[bold]Code template[/]");
        for (var i = 0; i < ordered.Count; i++)
        {
            var slug = ordered[i];
            var title = byTitle.TryGetValue(slug, out var tmpl) ? tmpl.Title : slug;
            var hasCode = scaffolder.HasCodeTemplate(slug) ? "[green]yes[/]" : "[yellow]not yet[/]";
            table.AddRow($"{i + 1}", slug, Markup.Escape(title), hasCode);
        }
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[dim]Tenancy: {(includeTenant ? "ENABLED — TenantId on every owned entity" : "DISABLED — single-tenant")}[/]");
        AnsiConsole.MarkupLine($"[dim]Target:  {Path.GetRelativePath(Directory.GetCurrentDirectory(), srcRoot)}[/]");

        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine("[yellow]--dry-run: no files written.[/]");
            return 0;
        }

        if (!AnsiConsole.Confirm($"Scaffold {ordered.Count} slice(s) into [cyan]{Path.GetRelativePath(Directory.GetCurrentDirectory(), srcRoot)}[/]?", defaultValue: true))
        {
            AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
            return 0;
        }

        // 6. Emit each slice.
        var emitted = 0;
        var skipped = new List<string>();
        foreach (var slug in ordered)
        {
            if (!scaffolder.HasCodeTemplate(slug))
            {
                skipped.Add(slug);
                continue;
            }

            AnsiConsole.MarkupLine($"\n[bold cyan]{slug}[/]");
            var files = scaffolder.EmitFiles(slug, srcRoot, settings.Force, settings.DryRun);
            foreach (var file in files)
            {
                if (file.StartsWith("(skipped"))
                {
                    AnsiConsole.MarkupLine($"  [yellow]{Markup.Escape(file)}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"  [green]wrote[/] {Markup.Escape(file)}");
                }
            }

            var wiring = scaffolder.ApplyWiring(slug, srcRoot, settings.DryRun);
            foreach (var msg in wiring)
            {
                AnsiConsole.MarkupLine($"  [dim]wiring:[/] {Markup.Escape(msg)}");
            }
            emitted++;
        }

        if (skipped.Count > 0)
        {
            AnsiConsole.MarkupLine($"\n[yellow]No code template yet for:[/] {string.Join(", ", skipped)}");
            AnsiConsole.MarkupLine("[dim]These slices are coming in a future shf-cli release — pin the framework template version manually for now (StrawhatsCompany/sh-framework-template @ v3.7.0+).[/]");
        }

        if (emitted == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Nothing scaffolded — no slices in your selection have code templates yet.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"\n[green]Done.[/] Scaffolded {emitted} slice(s) into [cyan]{Path.GetRelativePath(Directory.GetCurrentDirectory(), srcRoot)}[/].");
        AnsiConsole.MarkupLine("[dim]Next: run [yellow]dotnet build[/] to verify, then commit the changes.[/]");
        return 0;
    }

    private static HashSet<string> ParseTypes(string csv)
    {
        if (string.Equals(csv.Trim(), "all", StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<string>(AuthTypes.Select(t => t.Key), StringComparer.OrdinalIgnoreCase);
        }
        var known = AuthTypes.ToDictionary(t => t.Key, _ => true, StringComparer.OrdinalIgnoreCase);
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(k => known.ContainsKey(k))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> PromptForTypes()
    {
        var prompt = new MultiSelectionPrompt<string>()
            .Title("Which authentication features do you want?")
            .NotRequired()
            .PageSize(12)
            .InstructionsText("[grey](Press [blue]space[/] to toggle, [green]enter[/] to confirm)[/]")
            .AddChoices(AuthTypes.Select(t => $"{t.Key}  —  {t.Label}"));
        prompt.Select($"identity  —  {AuthTypes.First(t => t.Key == "identity").Label}");
        prompt.Select($"jwt  —  {AuthTypes.First(t => t.Key == "jwt").Label}");
        prompt.Select($"refresh  —  {AuthTypes.First(t => t.Key == "refresh").Label}");

        var picked = AnsiConsole.Prompt(prompt);
        return picked.Select(line => line.Split("  —  ", 2)[0]).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> ResolveDependencies(HashSet<string> selected)
    {
        var result = new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase);
        var byKey = AuthTypes.ToDictionary(t => t.Key, StringComparer.OrdinalIgnoreCase);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var key in result.ToList())
            {
                if (!byKey.TryGetValue(key, out var def)) continue;
                foreach (var dep in def.DependsOn)
                {
                    if (result.Add(dep)) changed = true;
                }
            }
        }
        return result;
    }

    private static IEnumerable<string> TopologicalOrder(HashSet<string> selected)
    {
        var byKey = AuthTypes.ToDictionary(t => t.Key, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();
        void Visit(string key)
        {
            if (!seen.Add(key)) return;
            if (byKey.TryGetValue(key, out var def))
            {
                foreach (var dep in def.DependsOn)
                {
                    if (selected.Contains(dep)) Visit(dep);
                }
            }
            ordered.Add(key);
        }
        foreach (var key in AuthTypes.Select(t => t.Key))
        {
            if (selected.Contains(key)) Visit(key);
        }
        return ordered;
    }
}
