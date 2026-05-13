namespace Shf.Cli.Services;

internal sealed class AuthScaffolder(IFileWriter writer) : IAuthScaffolder
{
    private static readonly string CodeRoot = Path.Combine(
        AppContext.BaseDirectory, "Templates", "Authentication", "code");

    public bool HasCodeTemplate(string slug)
    {
        var dir = Path.Combine(CodeRoot, slug);
        return Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories).Any();
    }

    public IReadOnlyList<string> EmitFiles(string slug, string srcRoot, bool force, bool dryRun)
    {
        var dir = Path.Combine(CodeRoot, slug);
        if (!Directory.Exists(dir)) return [];

        var written = new List<string>();
        foreach (var template in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(dir, template);
            var target = Path.Combine(srcRoot, relative);
            var content = File.ReadAllText(template);

            try
            {
                writer.Write(target, content, overwrite: force, dryRun: dryRun);
                written.Add(relative.Replace('\\', '/'));
            }
            catch (IOException)
            {
                // FileWriter throws on exists-without-force. Surface that as a "skip" so the
                // caller can render it differently; don't crash the whole emit.
                written.Add($"(skipped — exists) {relative.Replace('\\', '/')}");
            }
        }
        return written;
    }

    public IReadOnlyList<string> ApplyWiring(string slug, string srcRoot, bool dryRun)
    {
        return slug switch
        {
            "foundations" => ApplyFoundationsWiring(srcRoot, dryRun),
            "tenant" => ApplyTenantWiring(srcRoot, dryRun),
            _ => [],
        };
    }

    private IReadOnlyList<string> ApplyTenantWiring(string srcRoot, bool dryRun)
    {
        var messages = new List<string>();
        var registerBusiness = Path.Combine(srcRoot, "Business", "RegisterBusiness.cs");
        if (!File.Exists(registerBusiness))
        {
            messages.Add($"(skipped) Business/RegisterBusiness.cs not found at {registerBusiness}");
            return messages;
        }

        messages.Add(EditOrSkip(registerBusiness, dryRun, content =>
        {
            if (content.Contains("ITenantStore", StringComparison.Ordinal))
            {
                return (content, "already wired");
            }
            content = EnsureUsing(content, "using Business.Identity;", "using Business.Identity;\n");
            content = EnsureUsing(content, "using Microsoft.Extensions.DependencyInjection.Extensions;", "using Microsoft.Extensions.DependencyInjection.Extensions;\n");

            const string marker = "return services;";
            var idx = content.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return (content, "marker `return services;` not found — manual wire needed");

            var insert = "        services.TryAddSingleton<ITenantStore, InMemoryTenantStore>();\n\n        ";
            content = content.Insert(idx, insert);
            return (content, "added TryAddSingleton<ITenantStore, InMemoryTenantStore> inside AddBusiness");
        }));
        return messages;
    }

    // ---- foundations: TryAddScoped null contexts in AddBusiness, AddHttpContextAccessor +
    // scoped overrides in Program.cs.

    private IReadOnlyList<string> ApplyFoundationsWiring(string srcRoot, bool dryRun)
    {
        var messages = new List<string>();

        var registerBusiness = Path.Combine(srcRoot, "Business", "RegisterBusiness.cs");
        if (File.Exists(registerBusiness))
        {
            messages.Add(EditOrSkip(registerBusiness, dryRun, content =>
            {
                if (content.Contains("TryAddScoped<IUserContext", StringComparison.Ordinal))
                {
                    return (content, "already wired");
                }
                var ns = "using Business.Common;\n";
                var nsExt = "using Microsoft.Extensions.DependencyInjection.Extensions;\n";
                content = EnsureUsing(content, "using Business.Common;", ns);
                content = EnsureUsing(content, "using Microsoft.Extensions.DependencyInjection.Extensions;", nsExt);

                // Insert after the last existing services.* line inside AddBusiness, just before
                // the closing brace of the method. Marker: `return services;`.
                const string marker = "return services;";
                var idx = content.IndexOf(marker, StringComparison.Ordinal);
                if (idx < 0) return (content, "marker `return services;` not found — manual wire needed");

                var insert =
                    "        services.TryAddScoped<IUserContext, NullUserContext>();\n" +
                    "        services.TryAddScoped<ITenantContext, NullTenantContext>();\n\n        ";
                content = content.Insert(idx, insert);
                return (content, "added TryAddScoped null defaults inside AddBusiness");
            }));
        }
        else
        {
            messages.Add($"(skipped) Business/RegisterBusiness.cs not found at {registerBusiness}");
        }

        var program = Path.Combine(srcRoot, "WebApi", "Program.cs");
        if (File.Exists(program))
        {
            messages.Add(EditOrSkip(program, dryRun, content =>
            {
                if (content.Contains("HttpUserContext", StringComparison.Ordinal))
                {
                    return (content, "already wired");
                }

                content = EnsureUsing(content, "using Business.Common;", "using Business.Common;\n");
                content = EnsureUsing(content, "using WebApi.Common;", "using WebApi.Common;\n");

                // Find the .AddBusiness() chain and insert HTTP context overrides after the
                // builder.Services chain closes (look for the first semicolon after AddBusiness()).
                const string marker = ".AddBusiness()";
                var idx = content.IndexOf(marker, StringComparison.Ordinal);
                if (idx < 0) return (content, "marker `.AddBusiness()` not found — manual wire needed");
                var semi = content.IndexOf(';', idx);
                if (semi < 0) return (content, "couldn't find statement end after AddBusiness — manual wire needed");

                var insert =
                    "\n\nbuilder.Services.AddHttpContextAccessor();" +
                    "\nbuilder.Services.AddScoped<IUserContext, HttpUserContext>();" +
                    "\nbuilder.Services.AddScoped<ITenantContext, HttpTenantContext>();";
                content = content.Insert(semi + 1, insert);
                return (content, "added AddHttpContextAccessor + scoped Http*Context registrations");
            }));
        }
        else
        {
            messages.Add($"(skipped) WebApi/Program.cs not found at {program}");
        }

        return messages;
    }

    private string EditOrSkip(string path, bool dryRun, Func<string, (string content, string message)> transform)
    {
        string note = "";
        writer.ApplyEdit(path, content =>
        {
            var (next, msg) = transform(content);
            note = msg;
            return next;
        }, dryRun);
        return $"{Path.GetFileName(path)}: {note}";
    }

    private static string EnsureUsing(string source, string token, string lineToInsert)
    {
        if (source.Contains(token, StringComparison.Ordinal)) return source;
        // Insert at the top, right after any leading using block.
        var firstLine = source.IndexOf('\n');
        if (firstLine < 0) return lineToInsert + source;
        return lineToInsert + source;
    }
}
