using Shf.Cli.Commands.Make;
using Shf.Cli.Services;
using Spectre.Console.Cli;

namespace Shf.Cli.Tests.Commands.Make;

public class MakeEndpointCommandTests
{
    [Fact]
    public void Query_heuristic_picks_GET_template_for_Get_prefix()
    {
        var (cmd, writer, renderer) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeEndpointCommand.Settings { Name = "Weather/GetForecastsByCity", DryRun = true });

        Assert.Equal(0, exit);
        AssertRenderedTemplate(renderer, "Get.cs.sbn");
        AssertWrote(writer, "GetForecastsByCityEndpoint.cs");
    }

    [Fact]
    public void Command_heuristic_picks_POST_template_for_non_query_prefix()
    {
        var (cmd, writer, renderer) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeEndpointCommand.Settings { Name = "Orders/PlaceOrder", DryRun = true });

        Assert.Equal(0, exit);
        AssertRenderedTemplate(renderer, "Post.cs.sbn");
        AssertWrote(writer, "PlaceOrderEndpoint.cs");
    }

    [Fact]
    public void Force_command_overrides_query_heuristic()
    {
        var (cmd, writer, renderer) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeEndpointCommand.Settings
        {
            Name = "Weather/GetSomething",
            ForceCommand = true,
            DryRun = true,
        });

        Assert.Equal(0, exit);
        AssertRenderedTemplate(renderer, "Post.cs.sbn");
    }

    [Fact]
    public void Force_query_overrides_command_heuristic()
    {
        var (cmd, writer, renderer) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeEndpointCommand.Settings
        {
            Name = "Orders/Settle",
            ForceQuery = true,
            DryRun = true,
        });

        Assert.Equal(0, exit);
        AssertRenderedTemplate(renderer, "Get.cs.sbn");
    }

    [Fact]
    public void Query_and_command_together_is_an_error()
    {
        var (cmd, _, _) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeEndpointCommand.Settings
        {
            Name = "Foo/Bar",
            ForceQuery = true,
            ForceCommand = true,
            DryRun = true,
        });

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void Default_route_is_api_v1_domain_operation()
    {
        var (cmd, _, renderer) = BuildCommand();

        cmd.Execute(Ctx(), new MakeEndpointCommand.Settings { Name = "Weather/GetForecastsByCity", DryRun = true });

        AssertRenderedWithRoute(renderer, "api/v1/Weather/GetForecastsByCity");
    }

    [Fact]
    public void Route_override_takes_precedence()
    {
        var (cmd, _, renderer) = BuildCommand();

        cmd.Execute(Ctx(), new MakeEndpointCommand.Settings
        {
            Name = "Weather/GetForecastsByCity",
            Route = "/weather/forecasts/{city:alpha}",
            DryRun = true,
        });

        AssertRenderedWithRoute(renderer, "/weather/forecasts/{city:alpha}");
    }

    [Fact]
    public void Returns_nonzero_when_name_is_malformed()
    {
        var (cmd, _, _) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeEndpointCommand.Settings { Name = "MissingSlash", DryRun = true });

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void Returns_nonzero_when_webapi_project_cannot_be_found()
    {
        var locator = Substitute.For<IProjectLocator>();
        locator.FindWebApiProject(Arg.Any<string>()).Returns((string?)null);
        var renderer = Substitute.For<ITemplateRenderer>();
        var writer = Substitute.For<IFileWriter>();
        var cmd = new MakeEndpointCommand(locator, renderer, writer);

        var exit = cmd.Execute(Ctx(), new MakeEndpointCommand.Settings { Name = "Weather/GetForecastsByCity", DryRun = true });

        Assert.NotEqual(0, exit);
    }

    private static (MakeEndpointCommand cmd, IFileWriter writer, ITemplateRenderer renderer) BuildCommand()
    {
        var locator = Substitute.For<IProjectLocator>();
        locator.FindWebApiProject(Arg.Any<string>()).Returns("/repo/src/WebApi");
        var renderer = Substitute.For<ITemplateRenderer>();
        renderer.Render(Arg.Any<string>(), Arg.Any<object>()).Returns("// generated");
        var writer = Substitute.For<IFileWriter>();
        writer.Write(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(true);
        return (new MakeEndpointCommand(locator, renderer, writer), writer, renderer);
    }

    private static CommandContext Ctx() =>
        new([], Substitute.For<IRemainingArguments>(), "make:endpoint", null);

    private static void AssertRenderedTemplate(ITemplateRenderer renderer, string templateFile) =>
        renderer.Received().Render(Arg.Is<string>(p => p.EndsWith(templateFile)), Arg.Any<object>());

    private static void AssertWrote(IFileWriter writer, string fileName) =>
        writer.Received().Write(Arg.Is<string>(p => p.EndsWith(fileName)), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>());

    private static void AssertRenderedWithRoute(ITemplateRenderer renderer, string expectedRoute) =>
        renderer.Received().Render(
            Arg.Any<string>(),
            Arg.Is<object>(m => m.GetType().GetProperty("Route")!.GetValue(m)!.ToString() == expectedRoute));
}
