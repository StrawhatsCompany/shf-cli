using Shf.Cli.Commands.Make;
using Shf.Cli.Services;
using Spectre.Console.Cli;

namespace Shf.Cli.Tests.Commands.Make;

public class MakeFeatureCommandTests
{
    [Fact]
    public void Query_with_response_writes_three_files()
    {
        var (cmd, writer, _) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeFeatureCommand.Settings { Name = "Weather/GetForecastsByCity", DryRun = true });

        Assert.Equal(0, exit);
        AssertWrote(writer, "GetForecastsByCityQuery.cs");
        AssertWrote(writer, "GetForecastsByCityHandler.cs");
        AssertWrote(writer, "GetForecastsByCityResponse.cs");
    }

    [Fact]
    public void Command_when_name_does_not_start_with_query_prefix()
    {
        var (cmd, writer, _) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeFeatureCommand.Settings { Name = "Mails/SendMail", DryRun = true });

        Assert.Equal(0, exit);
        AssertWrote(writer, "SendMailCommand.cs");
        AssertWrote(writer, "SendMailHandler.cs");
        AssertDidNotWrite(writer, "SendMailResponse.cs");
    }

    [Fact]
    public void Force_command_overrides_query_heuristic()
    {
        var (cmd, writer, _) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeFeatureCommand.Settings { Name = "Weather/GetSomething", ForceCommand = true, DryRun = true });

        Assert.Equal(0, exit);
        AssertWrote(writer, "GetSomethingCommand.cs");
        AssertDidNotWrite(writer, "GetSomethingQuery.cs");
    }

    [Fact]
    public void Query_with_no_response_flag_skips_response_file()
    {
        var (cmd, writer, _) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeFeatureCommand.Settings { Name = "Weather/GetForecastsByCity", NoResponse = true, DryRun = true });

        Assert.Equal(0, exit);
        AssertWrote(writer, "GetForecastsByCityQuery.cs");
        AssertDidNotWrite(writer, "GetForecastsByCityResponse.cs");
    }

    [Fact]
    public void Returns_nonzero_when_name_is_malformed()
    {
        var (cmd, _, _) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeFeatureCommand.Settings { Name = "MissingSlash", DryRun = true });

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void Returns_nonzero_when_business_project_cannot_be_found()
    {
        var locator = Substitute.For<IProjectLocator>();
        locator.FindBusinessProject(Arg.Any<string>()).Returns((string?)null);
        var renderer = Substitute.For<ITemplateRenderer>();
        renderer.Render(Arg.Any<string>(), Arg.Any<object>()).Returns("// generated");
        var writer = Substitute.For<IFileWriter>();
        var cmd = new MakeFeatureCommand(locator, renderer, writer);

        var exit = cmd.Execute(Ctx(), new MakeFeatureCommand.Settings { Name = "Weather/GetForecastsByCity", DryRun = true });

        Assert.NotEqual(0, exit);
    }

    private static (MakeFeatureCommand cmd, IFileWriter writer, ITemplateRenderer renderer) BuildCommand()
    {
        var locator = Substitute.For<IProjectLocator>();
        locator.FindBusinessProject(Arg.Any<string>()).Returns("/repo/src/Business");
        var renderer = Substitute.For<ITemplateRenderer>();
        renderer.Render(Arg.Any<string>(), Arg.Any<object>()).Returns("// generated");
        var writer = Substitute.For<IFileWriter>();
        writer.Write(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(true);
        return (new MakeFeatureCommand(locator, renderer, writer), writer, renderer);
    }

    private static CommandContext Ctx() =>
        new([], Substitute.For<IRemainingArguments>(), "make:feature", null);

    private static void AssertWrote(IFileWriter writer, string fileName) =>
        writer.Received().Write(Arg.Is<string>(p => p.EndsWith(fileName)), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>());

    private static void AssertDidNotWrite(IFileWriter writer, string fileName) =>
        writer.DidNotReceive().Write(Arg.Is<string>(p => p.EndsWith(fileName)), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>());
}
