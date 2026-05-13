using Shf.Cli.Services;

namespace Shf.Cli.Tests.Services;

public class AuthScaffolderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "shf-scaffolder-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void HasCodeTemplate_true_for_foundations_false_for_unknown()
    {
        var scaffolder = new AuthScaffolder(new FileWriter());

        Assert.True(scaffolder.HasCodeTemplate("foundations"));
        Assert.True(scaffolder.HasCodeTemplate("tenant"));
        Assert.False(scaffolder.HasCodeTemplate("sso"));   // not shipped in v0.6.0
        Assert.False(scaffolder.HasCodeTemplate("nonsense"));
    }

    [Fact]
    public void EmitFiles_foundations_writes_twelve_files_into_src_root()
    {
        Directory.CreateDirectory(_root);
        var scaffolder = new AuthScaffolder(new FileWriter());

        var emitted = scaffolder.EmitFiles("foundations", _root, force: false, dryRun: false);

        // 6 Domain/Abstractions + 4 Business/Common + 2 WebApi/Common = 12 files
        Assert.Equal(12, emitted.Count);
        Assert.True(File.Exists(Path.Combine(_root, "Domain/Abstractions/IPrimaryKey.cs")));
        Assert.True(File.Exists(Path.Combine(_root, "Business/Common/IUserContext.cs")));
        Assert.True(File.Exists(Path.Combine(_root, "WebApi/Common/HttpUserContext.cs")));
    }

    [Fact]
    public void EmitFiles_dry_run_writes_no_files()
    {
        Directory.CreateDirectory(_root);
        var scaffolder = new AuthScaffolder(new FileWriter());

        scaffolder.EmitFiles("foundations", _root, force: false, dryRun: true);

        Assert.False(File.Exists(Path.Combine(_root, "Domain/Abstractions/IPrimaryKey.cs")));
    }

    [Fact]
    public void EmitFiles_skips_existing_without_force()
    {
        Directory.CreateDirectory(_root);
        var scaffolder = new AuthScaffolder(new FileWriter());

        // First pass writes everything.
        var first = scaffolder.EmitFiles("foundations", _root, force: false, dryRun: false);
        Assert.DoesNotContain(first, p => p.StartsWith("(skipped"));

        // Second pass without --force should mark them all skipped.
        var second = scaffolder.EmitFiles("foundations", _root, force: false, dryRun: false);
        Assert.All(second, p => Assert.StartsWith("(skipped", p));
    }

    [Fact]
    public void ApplyWiring_foundations_injects_TryAddScoped_into_RegisterBusiness()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Business"));
        File.WriteAllText(Path.Combine(_root, "Business", "RegisterBusiness.cs"),
            "namespace Business;\n\npublic static class RegisterBusiness\n{\n    public static IServiceCollection AddBusiness(this IServiceCollection services)\n    {\n        return services;\n    }\n}\n");

        var scaffolder = new AuthScaffolder(new FileWriter());
        scaffolder.ApplyWiring("foundations", _root, dryRun: false);

        var content = File.ReadAllText(Path.Combine(_root, "Business", "RegisterBusiness.cs"));
        Assert.Contains("TryAddScoped<IUserContext, NullUserContext>", content);
        Assert.Contains("TryAddScoped<ITenantContext, NullTenantContext>", content);
    }

    [Fact]
    public void ApplyWiring_tenant_injects_TenantStore_into_RegisterBusiness()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Business"));
        File.WriteAllText(Path.Combine(_root, "Business", "RegisterBusiness.cs"),
            "namespace Business;\n\npublic static class RegisterBusiness\n{\n    public static IServiceCollection AddBusiness(this IServiceCollection services)\n    {\n        return services;\n    }\n}\n");

        var scaffolder = new AuthScaffolder(new FileWriter());
        scaffolder.ApplyWiring("tenant", _root, dryRun: false);

        var content = File.ReadAllText(Path.Combine(_root, "Business", "RegisterBusiness.cs"));
        Assert.Contains("TryAddSingleton<ITenantStore, InMemoryTenantStore>", content);
    }
}
