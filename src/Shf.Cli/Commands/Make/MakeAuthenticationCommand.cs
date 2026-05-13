using System.ComponentModel;
using Shf.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shf.Cli.Commands.Make;

public sealed class MakeAuthenticationCommand(
    IAuthTemplateLoader templates,
    IGitHubIssueClient github) : Command<MakeAuthenticationCommand.Settings>
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

        [CommandOption("--repo <OWNER/REPO>")]
        [Description("Target GitHub repo for the emitted issues. Defaults to the cwd's origin remote.")]
        public string? Repo { get; init; }

        [CommandOption("--dry-run")]
        [Description("Print the plan and the resolved issue list without calling gh.")]
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

        // 1. Pick the auth types — flags first, then interactive.
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

        // 4. Resolve target repo.
        var repo = settings.Repo ?? github.DetectRepoFromGit(Directory.GetCurrentDirectory());
        if (string.IsNullOrEmpty(repo))
        {
            AnsiConsole.MarkupLine("[red]Could not detect GitHub repo from origin remote. Pass --repo owner/name or run inside a git checkout with a github.com origin.[/]");
            return 1;
        }

        // 5. Build the final ordered slug list (foundations first, then tenant if opted in, then deps order).
        var ordered = new List<string> { "foundations" };
        if (includeTenant) ordered.Add("tenant");
        ordered.AddRange(TopologicalOrder(resolved));

        // 6. Load templates and emit.
        var allTemplates = templates.LoadAll().ToDictionary(t => t.Slug, StringComparer.OrdinalIgnoreCase);
        var missing = ordered.Where(s => !allTemplates.ContainsKey(s)).ToList();
        if (missing.Count > 0)
        {
            AnsiConsole.MarkupLine($"[red]Missing template files for: {string.Join(", ", missing)}.[/]");
            AnsiConsole.MarkupLine("[red]The CLI was packaged without one or more Authentication templates — please file an issue.[/]");
            return 1;
        }

        AnsiConsole.Write(new Rule($"[bold]Authentication scaffolding plan[/] → [cyan]{repo}[/]").LeftJustified());
        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn("[bold]#[/]")
            .AddColumn("[bold]Slug[/]")
            .AddColumn("[bold]Title[/]");
        for (var i = 0; i < ordered.Count; i++)
        {
            var t = allTemplates[ordered[i]];
            table.AddRow($"{i + 1}", t.Slug, Markup.Escape(t.Title));
        }
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[dim]Tenancy: {(includeTenant ? "ENABLED — TenantId on every owned entity" : "DISABLED — single-tenant")}[/]");

        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine("[yellow]--dry-run: no issues created.[/]");
            return 0;
        }

        if (!AnsiConsole.Confirm($"Create {ordered.Count} GitHub issues on [cyan]{repo}[/]?", defaultValue: true))
        {
            AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
            return 0;
        }

        // 7. Ensure labels exist.
        var allLabels = ordered.SelectMany(slug => allTemplates[slug].Labels).Distinct().ToList();
        foreach (var label in allLabels)
        {
            github.EnsureLabel(repo, label, "Auto-created by `shf make:authentication`", "0E8A16");
        }

        // 8. First pass: create issues, capture numbers.
        var slugToIssue = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var slug in ordered)
        {
            var template = allTemplates[slug];
            var body = ApplyTenancyVariant(template.Body, includeTenant);
            var number = github.CreateIssue(repo, template.Title, body, template.Labels);
            if (number is null)
            {
                AnsiConsole.MarkupLine($"[red]Failed to create issue for [white]{slug}[/].[/]");
                return 1;
            }
            slugToIssue[slug] = number.Value;
            AnsiConsole.MarkupLine($"[green]created[/] #{number} — {Markup.Escape(template.Title)}");
        }

        // 9. Second pass: patch bodies to replace {{slug:foo}} placeholders with #NN.
        foreach (var slug in ordered)
        {
            var template = allTemplates[slug];
            var body = ApplyTenancyVariant(template.Body, includeTenant);
            var patched = SubstitutePlaceholders(body, slugToIssue);
            if (patched != body)
            {
                github.EditIssueBody(repo, slugToIssue[slug], patched);
            }
        }

        AnsiConsole.MarkupLine($"\n[green]Done.[/] {ordered.Count} issues opened on [cyan]{repo}[/].");
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
        // Pre-tick the common starting set.
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

    private static string ApplyTenancyVariant(string body, bool includeTenant)
    {
        // Templates can use {{#tenant}}…{{/tenant}} blocks that render only when tenancy is on,
        // and {{#no-tenant}}…{{/no-tenant}} blocks for the opposite. Plus a {{tenant-flag}}
        // marker that renders "ENABLED" or "DISABLED".
        var open = includeTenant ? "{{#tenant}}" : "{{#no-tenant}}";
        var close = includeTenant ? "{{/tenant}}" : "{{/no-tenant}}";
        var keepBlock = ExtractBlock(body, open, close);
        var stripOpen = includeTenant ? "{{#no-tenant}}" : "{{#tenant}}";
        var stripClose = includeTenant ? "{{/no-tenant}}" : "{{/tenant}}";
        var trimmed = StripBlock(keepBlock, stripOpen, stripClose);
        return trimmed.Replace("{{tenant-flag}}", includeTenant ? "ENABLED" : "DISABLED", StringComparison.Ordinal);
    }

    private static string ExtractBlock(string body, string open, string close)
    {
        return body.Replace(open, "", StringComparison.Ordinal).Replace(close, "", StringComparison.Ordinal);
    }

    private static string StripBlock(string body, string open, string close)
    {
        while (true)
        {
            var start = body.IndexOf(open, StringComparison.Ordinal);
            if (start < 0) return body;
            var end = body.IndexOf(close, start, StringComparison.Ordinal);
            if (end < 0) return body;
            body = body.Remove(start, (end + close.Length) - start);
        }
    }

    private static string SubstitutePlaceholders(string body, IReadOnlyDictionary<string, int> slugToIssue)
    {
        foreach (var (slug, number) in slugToIssue)
        {
            body = body.Replace($"{{{{slug:{slug}}}}}", $"#{number}", StringComparison.OrdinalIgnoreCase);
        }
        return body;
    }
}
