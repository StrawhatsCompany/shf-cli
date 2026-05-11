using Shf.Cli.Commands.Make;
using Shf.Cli.Services;
using Spectre.Console.Cli;

namespace Shf.Cli.Tests.Commands.Make;

public class MakeProviderDriverCommandTests : IDisposable
{
    private readonly string _fixtureRoot;
    private readonly string _businessRoot;
    private readonly string _providerProjectRoot;

    public MakeProviderDriverCommandTests()
    {
        _fixtureRoot = Path.Combine(Path.GetTempPath(), "shf-cli-driver-tests", Guid.NewGuid().ToString("N"));
        _businessRoot = Path.Combine(_fixtureRoot, "src", "Business");
        _providerProjectRoot = Path.Combine(_fixtureRoot, "src", "Providers.Sms");
        Directory.CreateDirectory(Path.Combine(_businessRoot, "Providers", "Sms"));
        Directory.CreateDirectory(_providerProjectRoot);

        File.WriteAllText(Path.Combine(_providerProjectRoot, "Providers.Sms.csproj"), "<Project/>");
        File.WriteAllText(Path.Combine(_businessRoot, "Providers", "Sms", "SmsProviderType.cs"),
            """
            namespace Business.Providers.Sms;

            public enum SmsProviderType
            {
            }
            """);
        File.WriteAllText(Path.Combine(_providerProjectRoot, "ProviderFactory.cs"),
            """
            using Business.Providers;
            using Business.Providers.Sms;

            namespace Providers.Sms;

            internal sealed class ProviderFactory : IProviderFactory<SmsProviderCredential, ISmsProvider>
            {
                public ISmsProvider Create(SmsProviderCredential credential) =>
                    credential.ProviderType switch
                    {
                        _ => throw new NotSupportedException($"{credential.ProviderType} is not supported from Providers.Sms")
                    };
            }
            """);
    }

    public void Dispose()
    {
        try { Directory.Delete(_fixtureRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Writes_driver_file_and_edits_enum_and_factory()
    {
        var (cmd, writer) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeProviderDriverCommand.Settings { Provider = "Sms", Driver = "Twilio" });

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(_providerProjectRoot, "Twilio", "TwilioProvider.cs")));

        var enumText = File.ReadAllText(Path.Combine(_businessRoot, "Providers", "Sms", "SmsProviderType.cs"));
        Assert.Contains("Twilio = 0,", enumText);

        var factoryText = File.ReadAllText(Path.Combine(_providerProjectRoot, "ProviderFactory.cs"));
        Assert.Contains("using Providers.Sms.Twilio;", factoryText);
        Assert.Contains("SmsProviderType.Twilio => new TwilioProvider(credential),", factoryText);
    }

    [Fact]
    public void Second_run_for_a_different_driver_appends_without_touching_the_first()
    {
        var (cmd, _) = BuildCommand();

        cmd.Execute(Ctx(), new MakeProviderDriverCommand.Settings { Provider = "Sms", Driver = "Twilio" });
        cmd.Execute(Ctx(), new MakeProviderDriverCommand.Settings { Provider = "Sms", Driver = "Vonage" });

        var enumText = File.ReadAllText(Path.Combine(_businessRoot, "Providers", "Sms", "SmsProviderType.cs"));
        Assert.Contains("Twilio = 0,", enumText);
        Assert.Contains("Vonage = 1,", enumText);

        var factoryText = File.ReadAllText(Path.Combine(_providerProjectRoot, "ProviderFactory.cs"));
        Assert.Contains("SmsProviderType.Twilio", factoryText);
        Assert.Contains("SmsProviderType.Vonage", factoryText);
    }

    [Fact]
    public void Returns_nonzero_when_provider_project_is_missing()
    {
        var (cmd, _) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeProviderDriverCommand.Settings { Provider = "Unknown", Driver = "X" });

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void Returns_nonzero_when_names_are_not_pascal_case()
    {
        var (cmd, _) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeProviderDriverCommand.Settings { Provider = "sms", Driver = "twilio" });

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void Dry_run_leaves_the_file_system_alone()
    {
        var (cmd, _) = BuildCommand();
        var enumBefore = File.ReadAllText(Path.Combine(_businessRoot, "Providers", "Sms", "SmsProviderType.cs"));

        cmd.Execute(Ctx(), new MakeProviderDriverCommand.Settings { Provider = "Sms", Driver = "Twilio", DryRun = true });

        Assert.False(File.Exists(Path.Combine(_providerProjectRoot, "Twilio", "TwilioProvider.cs")));
        var enumAfter = File.ReadAllText(Path.Combine(_businessRoot, "Providers", "Sms", "SmsProviderType.cs"));
        Assert.Equal(enumBefore, enumAfter);
    }

    private (MakeProviderDriverCommand cmd, IFileWriter writer) BuildCommand()
    {
        var locator = Substitute.For<IProjectLocator>();
        locator.FindBusinessProject(Arg.Any<string>()).Returns(_businessRoot);
        var renderer = new TokenTemplateRendererForTest();
        var writer = new FileWriter();
        return (new MakeProviderDriverCommand(locator, renderer, writer), writer);
    }

    private static CommandContext Ctx() => new([], Substitute.For<IRemainingArguments>(), "make:provider-driver", null);

    /// <summary>
    /// Renders the actual template files shipped with the CLI, so the command's interaction with
    /// disk state is exercised end-to-end (driver file written, enum + factory mutated).
    /// </summary>
    private sealed class TokenTemplateRendererForTest : ITemplateRenderer
    {
        private readonly TokenTemplateRenderer _real = new();
        public string Render(string templatePath, object model) => _real.Render(templatePath, model);
    }
}
