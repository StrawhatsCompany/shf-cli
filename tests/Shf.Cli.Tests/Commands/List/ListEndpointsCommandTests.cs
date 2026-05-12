using Shf.Cli.Commands.List;
using Shf.Cli.Services;
using Spectre.Console.Cli;

namespace Shf.Cli.Tests.Commands.List;

public class ListEndpointsCommandTests
{
    [Fact]
    public void Returns_zero_when_endpoints_exist()
    {
        var (cmd, _) = Build([
            new("GET", "api/v1/a", "GetA", "Endpoints/A.cs"),
            new("POST", "api/v1/b", "PostB", "Endpoints/B.cs"),
        ]);

        var exit = cmd.Execute(Ctx(), new ListEndpointsCommand.Settings());

        Assert.Equal(0, exit);
    }

    [Fact]
    public void Returns_zero_when_no_endpoints_found()
    {
        var (cmd, _) = Build([]);

        var exit = cmd.Execute(Ctx(), new ListEndpointsCommand.Settings());

        Assert.Equal(0, exit);
    }

    [Fact]
    public void Returns_nonzero_when_webapi_project_cannot_be_located()
    {
        var locator = Substitute.For<IProjectLocator>();
        locator.FindWebApiProject(Arg.Any<string>()).Returns((string?)null);
        var scanner = Substitute.For<IEndpointScanner>();
        var cmd = new ListEndpointsCommand(locator, scanner);

        var exit = cmd.Execute(Ctx(), new ListEndpointsCommand.Settings());

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void Filter_matches_route_substring_case_insensitive()
    {
        var (cmd, scanner) = Build([
            new("GET", "api/v1/weather/forecasts", "GetForecasts", "Endpoints/Weather/GetForecasts.cs"),
            new("POST", "api/v1/mails/send", "SendMail", "Endpoints/Mails/Send.cs"),
        ]);

        var exit = cmd.Execute(Ctx(), new ListEndpointsCommand.Settings { Filter = "WEATHER" });

        Assert.Equal(0, exit);
        scanner.Received().Scan(Arg.Any<string>());
    }

    [Fact]
    public void Filter_matches_name_substring()
    {
        var (cmd, _) = Build([
            new("GET", "api/v1/x", "Alpha", "Endpoints/X.cs"),
            new("GET", "api/v1/y", "Beta", "Endpoints/Y.cs"),
        ]);

        var exit = cmd.Execute(Ctx(), new ListEndpointsCommand.Settings { Filter = "alph" });

        Assert.Equal(0, exit);
    }

    [Fact]
    public void Honors_project_override()
    {
        var locator = Substitute.For<IProjectLocator>();
        var scanner = Substitute.For<IEndpointScanner>();
        scanner.Scan(Arg.Any<string>()).Returns([]);
        var cmd = new ListEndpointsCommand(locator, scanner);

        cmd.Execute(Ctx(), new ListEndpointsCommand.Settings { Project = "/explicit/path/WebApi" });

        locator.DidNotReceive().FindWebApiProject(Arg.Any<string>());
        scanner.Received().Scan("/explicit/path/WebApi");
    }

    [Fact]
    public void Json_mode_returns_zero_with_empty_list()
    {
        var (cmd, _) = Build([]);

        var exit = cmd.Execute(Ctx(), new ListEndpointsCommand.Settings { Json = true });

        Assert.Equal(0, exit);
    }

    private static (ListEndpointsCommand cmd, IEndpointScanner scanner) Build(IReadOnlyList<EndpointInfo> endpoints)
    {
        var locator = Substitute.For<IProjectLocator>();
        locator.FindWebApiProject(Arg.Any<string>()).Returns("/repo/src/WebApi");
        var scanner = Substitute.For<IEndpointScanner>();
        scanner.Scan(Arg.Any<string>()).Returns(endpoints);
        return (new ListEndpointsCommand(locator, scanner), scanner);
    }

    private static CommandContext Ctx() => new([], Substitute.For<IRemainingArguments>(), "list:endpoints", null);
}
