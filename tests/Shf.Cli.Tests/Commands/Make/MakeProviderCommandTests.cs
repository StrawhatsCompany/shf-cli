using Shf.Cli.Commands.Make;
using Shf.Cli.Services;
using Spectre.Console.Cli;

namespace Shf.Cli.Tests.Commands.Make;

public class MakeProviderCommandTests
{
    [Fact]
    public void Generates_seven_files_when_no_first_driver()
    {
        var (cmd, writer, _, _) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeProviderCommand.Settings { Name = "Sms", DryRun = true });

        Assert.Equal(0, exit);
        AssertWroteEndingWith(writer, "Business/Providers/Sms/ISmsProvider.cs");
        AssertWroteEndingWith(writer, "Business/Providers/Sms/SmsProviderCredential.cs");
        AssertWroteEndingWith(writer, "Business/Providers/Sms/SmsProviderType.cs");
        AssertWroteEndingWith(writer, "Providers.Sms/Providers.Sms.csproj");
        AssertWroteEndingWith(writer, "Providers.Sms/ProviderFactory.cs");
        AssertWroteEndingWith(writer, "Providers.Sms/RegisterSmsProvider.cs");
        AssertWroteEndingWith(writer, "Providers.Sms/SmsProviderResultCode.cs");
        // No driver folder when --first-driver isn't passed.
        writer.DidNotReceive().Write(
            Arg.Is<string>(p => p.Replace('\\', '/').Contains("Providers.Sms/Twilio/")),
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>());
    }

    [Fact]
    public void First_driver_adds_an_eighth_file_under_Providers_Name_Driver()
    {
        var (cmd, writer, _, _) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeProviderCommand.Settings { Name = "Sms", FirstDriver = "Twilio", DryRun = true });

        Assert.Equal(0, exit);
        AssertWroteEndingWith(writer, "Providers.Sms/Twilio/TwilioProvider.cs");
    }

    [Fact]
    public void First_driver_seeds_the_enum_with_a_zero_indexed_member()
    {
        var (cmd, _, _, capture) = BuildCommand();

        cmd.Execute(Ctx(), new MakeProviderCommand.Settings { Name = "Sms", FirstDriver = "Twilio", DryRun = true });

        var enumMembers = ModelProperty(capture, "EnumMembers");
        Assert.Equal("    Twilio = 0,", enumMembers);
    }

    [Fact]
    public void No_driver_emits_empty_enum_members()
    {
        var (cmd, _, _, capture) = BuildCommand();

        cmd.Execute(Ctx(), new MakeProviderCommand.Settings { Name = "Sms", DryRun = true });

        Assert.Equal(string.Empty, ModelProperty(capture, "EnumMembers"));
    }

    [Fact]
    public void First_driver_seeds_the_factory_switch_case()
    {
        var (cmd, _, _, capture) = BuildCommand();

        cmd.Execute(Ctx(), new MakeProviderCommand.Settings { Name = "Sms", FirstDriver = "Twilio", DryRun = true });

        var cases = ModelProperty(capture, "FactoryCases");
        Assert.Contains("SmsProviderType.Twilio => new TwilioProvider(credential),", cases);
    }

    [Fact]
    public void NameUpper_is_propagated_for_the_ResultCode_category()
    {
        var (cmd, _, _, capture) = BuildCommand();

        cmd.Execute(Ctx(), new MakeProviderCommand.Settings { Name = "Sms", DryRun = true });

        Assert.Equal("SMS", ModelProperty(capture, "NameUpper"));
    }

    [Fact]
    public void Edits_solution_file_to_add_the_new_project()
    {
        var (cmd, writer, _, _) = BuildCommand(slnxExists: true);

        cmd.Execute(Ctx(), new MakeProviderCommand.Settings { Name = "Sms", DryRun = true });

        writer.Received().ApplyEdit(
            Arg.Is<string>(p => p.EndsWith(".slnx")),
            Arg.Any<Func<string, string>>(),
            Arg.Any<bool>());
    }

    [Fact]
    public void Discovers_solution_file_by_extension_not_by_fixed_name()
    {
        var (cmd, writer, _, _) = BuildCommand(slnxExists: true, slnxFileName: "MyService.slnx");

        cmd.Execute(Ctx(), new MakeProviderCommand.Settings { Name = "Sms", DryRun = true });

        writer.Received().ApplyEdit(
            Arg.Is<string>(p => p.EndsWith("MyService.slnx")),
            Arg.Any<Func<string, string>>(),
            Arg.Any<bool>());
    }

    [Fact]
    public void Returns_nonzero_when_name_is_not_pascal_case()
    {
        var (cmd, _, _, _) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeProviderCommand.Settings { Name = "sms", DryRun = true });

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void Returns_nonzero_when_first_driver_is_not_pascal_case()
    {
        var (cmd, _, _, _) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeProviderCommand.Settings { Name = "Sms", FirstDriver = "twilio", DryRun = true });

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void Returns_nonzero_when_business_project_cannot_be_found()
    {
        var locator = Substitute.For<IProjectLocator>();
        locator.FindBusinessProject(Arg.Any<string>()).Returns((string?)null);
        var renderer = Substitute.For<ITemplateRenderer>();
        var writer = Substitute.For<IFileWriter>();
        var cmd = new MakeProviderCommand(locator, renderer, writer);

        var exit = cmd.Execute(Ctx(), new MakeProviderCommand.Settings { Name = "Sms", DryRun = true });

        Assert.NotEqual(0, exit);
    }

    private sealed class ModelCapture { public object? Last; }

    private static (MakeProviderCommand cmd, IFileWriter writer, ITemplateRenderer renderer, ModelCapture capture) BuildCommand(bool slnxExists = false, string slnxFileName = "SHFramework.slnx")
    {
        var locator = Substitute.For<IProjectLocator>();
        locator.FindBusinessProject(Arg.Any<string>()).Returns("/repo/src/Business");
        var renderer = Substitute.For<ITemplateRenderer>();
        var capture = new ModelCapture();
        renderer.Render(Arg.Any<string>(), Arg.Do<object>(m => capture.Last = m)).Returns("// generated");
        var writer = Substitute.For<IFileWriter>();
        writer.Write(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(true);
        writer.ApplyEdit(Arg.Any<string>(), Arg.Any<Func<string, string>>(), Arg.Any<bool>()).Returns("<Solution/>");
        if (slnxExists)
        {
            var fixtureDir = EnsureFixtureSlnx(slnxFileName);
            locator.FindBusinessProject(Arg.Any<string>()).Returns(Path.Combine(fixtureDir, "Business"));
            locator.FindSolutionFile(fixtureDir).Returns(Path.Combine(fixtureDir, slnxFileName));
        }
        return (new MakeProviderCommand(locator, renderer, writer), writer, renderer, capture);
    }

    private static string EnsureFixtureSlnx(string fileName)
    {
        var dir = Path.Combine(Path.GetTempPath(), "shf-cli-tests", Path.GetFileNameWithoutExtension(fileName), "src");
        Directory.CreateDirectory(Path.Combine(dir, "Business"));
        var slnx = Path.Combine(dir, fileName);
        if (!File.Exists(slnx))
        {
            File.WriteAllText(slnx, "<Solution>\n  <Folder Name=\"/src/\">\n  </Folder>\n</Solution>\n");
        }
        return dir;
    }

    private static CommandContext Ctx() => new([], Substitute.For<IRemainingArguments>(), "make:provider", null);

    private static void AssertWroteEndingWith(IFileWriter writer, string relativeSuffix) =>
        writer.Received().Write(
            Arg.Is<string>(p => p.Replace('\\', '/').EndsWith(relativeSuffix)),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>());

    private static string ModelProperty(ModelCapture capture, string propertyName)
    {
        Assert.NotNull(capture.Last);
        var prop = capture.Last.GetType().GetProperty(propertyName);
        Assert.NotNull(prop);
        return prop.GetValue(capture.Last)?.ToString() ?? string.Empty;
    }
}
