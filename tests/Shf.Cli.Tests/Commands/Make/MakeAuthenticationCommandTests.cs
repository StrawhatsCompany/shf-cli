using Shf.Cli.Commands.Make;
using Shf.Cli.Services;
using Spectre.Console.Cli;

namespace Shf.Cli.Tests.Commands.Make;

public class MakeAuthenticationCommandTests
{
    [Fact]
    public void Returns_nonzero_when_tenant_and_no_tenant_both_set()
    {
        var (cmd, _, _, _) = Build();

        var exit = cmd.Execute(Ctx(), new MakeAuthenticationCommand.Settings
        {
            Types = "jwt", Tenant = true, NoTenant = true, DryRun = true,
        });

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void Returns_nonzero_when_business_project_cannot_be_located()
    {
        var (cmd, _, _, locator) = Build();
        locator.FindBusinessProject(Arg.Any<string>()).Returns((string?)null);

        var exit = cmd.Execute(Ctx(), new MakeAuthenticationCommand.Settings
        {
            Types = "jwt", Tenant = true, DryRun = true,
        });

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void DryRun_does_not_emit_files()
    {
        var (cmd, _, scaffolder, _) = Build();
        scaffolder.HasCodeTemplate(Arg.Any<string>()).Returns(true);

        var exit = cmd.Execute(Ctx(), new MakeAuthenticationCommand.Settings
        {
            Types = "jwt", Tenant = true, DryRun = true,
        });

        Assert.Equal(0, exit);
        scaffolder.DidNotReceive().EmitFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>());
        scaffolder.DidNotReceive().ApplyWiring(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public void Returns_nonzero_when_types_csv_is_invalid()
    {
        var (cmd, _, _, _) = Build();

        var exit = cmd.Execute(Ctx(), new MakeAuthenticationCommand.Settings
        {
            Types = "doesnotexist", Tenant = true, DryRun = true,
        });

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void All_keyword_includes_every_known_type()
    {
        var (cmd, _, scaffolder, _) = Build();
        scaffolder.HasCodeTemplate(Arg.Any<string>()).Returns(true);

        var exit = cmd.Execute(Ctx(), new MakeAuthenticationCommand.Settings
        {
            Types = "all", Tenant = true, DryRun = true,
        });

        Assert.Equal(0, exit);
        // foundations + tenant + 8 selected types = 10; HasCodeTemplate queried for each.
        scaffolder.Received(10).HasCodeTemplate(Arg.Any<string>());
    }

    private static (
        MakeAuthenticationCommand cmd,
        IAuthTemplateLoader templates,
        IAuthScaffolder scaffolder,
        IProjectLocator locator) Build()
    {
        var templates = Substitute.For<IAuthTemplateLoader>();
        templates.LoadAll().Returns(FakeTemplates());
        var scaffolder = Substitute.For<IAuthScaffolder>();
        var locator = Substitute.For<IProjectLocator>();
        locator.FindBusinessProject(Arg.Any<string>()).Returns("/repo/src/Business");
        return (new MakeAuthenticationCommand(templates, scaffolder, locator), templates, scaffolder, locator);
    }

    private static IReadOnlyList<AuthTemplate> FakeTemplates() =>
    [
        new("foundations", "Foundations",  [], [], "body"),
        new("tenant",      "Tenant",       [], ["foundations"], "body"),
        new("identity",    "Identity",     [], ["foundations"], "body"),
        new("jwt",         "JWT",          [], ["identity"], "body"),
        new("refresh",     "Refresh",      [], ["jwt"], "body"),
        new("apikey",      "ApiKey",       [], ["identity"], "body"),
        new("mfa-totp",    "MFA TOTP",     [], ["jwt"], "body"),
        new("mfa-email",   "MFA Email",    [], ["mfa-totp"], "body"),
        new("mfa-sms",     "MFA SMS",      [], ["mfa-totp"], "body"),
        new("sso",         "SSO",          [], ["refresh"], "body"),
    ];

    private static CommandContext Ctx() => new([], Substitute.For<IRemainingArguments>(), "make:authentication", null);
}
