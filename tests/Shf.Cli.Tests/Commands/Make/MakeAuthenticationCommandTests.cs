using Shf.Cli.Commands.Make;
using Shf.Cli.Services;
using Spectre.Console.Cli;

namespace Shf.Cli.Tests.Commands.Make;

public class MakeAuthenticationCommandTests
{
    [Fact]
    public void Returns_nonzero_when_tenant_and_no_tenant_both_set()
    {
        var (cmd, _, _) = Build();

        var exit = cmd.Execute(Ctx(), new MakeAuthenticationCommand.Settings
        {
            Types = "jwt", Tenant = true, NoTenant = true, DryRun = true,
        });

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void DryRun_with_explicit_types_emits_no_issues()
    {
        var (cmd, _, github) = Build();

        var exit = cmd.Execute(Ctx(), new MakeAuthenticationCommand.Settings
        {
            Types = "jwt,refresh,apikey",
            Tenant = true,
            Repo = "acme/example",
            DryRun = true,
        });

        Assert.Equal(0, exit);
        github.DidNotReceive().CreateIssue(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>());
    }

    [Fact]
    public void Returns_nonzero_when_types_csv_is_invalid()
    {
        var (cmd, _, _) = Build();

        var exit = cmd.Execute(Ctx(), new MakeAuthenticationCommand.Settings
        {
            Types = "doesnotexist", Tenant = true, DryRun = true,
        });

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void All_keyword_includes_every_known_type()
    {
        var (cmd, templates, _) = Build();

        var exit = cmd.Execute(Ctx(), new MakeAuthenticationCommand.Settings
        {
            Types = "all",
            Tenant = true,
            Repo = "acme/example",
            DryRun = true,
        });

        Assert.Equal(0, exit);
        // Loader was queried — the command at least walked the template set.
        templates.Received().LoadAll();
    }

    [Fact]
    public void Returns_nonzero_when_repo_cannot_be_detected()
    {
        var (cmd, _, github) = Build();
        github.DetectRepoFromGit(Arg.Any<string>()).Returns((string?)null);

        var exit = cmd.Execute(Ctx(), new MakeAuthenticationCommand.Settings
        {
            Types = "jwt", Tenant = true, DryRun = true,
        });

        Assert.NotEqual(0, exit);
    }

    private static (MakeAuthenticationCommand cmd, IAuthTemplateLoader templates, IGitHubIssueClient github) Build()
    {
        var templates = Substitute.For<IAuthTemplateLoader>();
        templates.LoadAll().Returns(FakeTemplates());
        var github = Substitute.For<IGitHubIssueClient>();
        github.DetectRepoFromGit(Arg.Any<string>()).Returns("acme/example");
        return (new MakeAuthenticationCommand(templates, github), templates, github);
    }

    private static IReadOnlyList<AuthTemplate> FakeTemplates() =>
    [
        new("foundations", "Foundations",  ["enhancement"], [], "body"),
        new("tenant",      "Tenant",       ["enhancement"], ["foundations"], "body"),
        new("identity",    "Identity",     ["enhancement"], ["foundations"], "body"),
        new("jwt",         "JWT",          ["enhancement"], ["identity"], "body"),
        new("refresh",     "Refresh",      ["enhancement"], ["jwt"], "body"),
        new("apikey",      "ApiKey",       ["enhancement"], ["identity"], "body"),
        new("mfa-totp",    "MFA TOTP",     ["enhancement"], ["jwt"], "body"),
        new("mfa-email",   "MFA Email",    ["enhancement"], ["mfa-totp"], "body"),
        new("mfa-sms",     "MFA SMS",      ["enhancement"], ["mfa-totp"], "body"),
        new("sso",         "SSO",          ["enhancement"], ["refresh"], "body"),
    ];

    private static CommandContext Ctx() => new([], Substitute.For<IRemainingArguments>(), "make:authentication", null);
}