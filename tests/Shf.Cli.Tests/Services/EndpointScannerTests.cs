using Shf.Cli.Services;

namespace Shf.Cli.Tests.Services;

public class EndpointScannerTests : IDisposable
{
    private readonly string _root;
    private readonly EndpointScanner _scanner = new();

    public EndpointScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "shf-cli-scanner-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void Empty_directory_returns_empty()
    {
        Assert.Empty(_scanner.Scan(_root));
    }

    [Fact]
    public void Missing_directory_returns_empty()
    {
        Assert.Empty(_scanner.Scan(Path.Combine(_root, "does-not-exist")));
    }

    [Fact]
    public void Parses_get_endpoint_with_arrow_route()
    {
        Write("Endpoints/Weather/GetForecastsByCityEndpoint.cs", """
            using SH.Framework.Library.AspNetCore;
            namespace WebApi.Endpoints.Weather;
            public sealed class GetForecastsByCityEndpoint : IEndpoint
            {
                public static string Route => "api/v1/weather/forecasts/{city:alpha}";
                public static void Map(IEndpointRouteBuilder app) =>
                    app.MapGet(Route, () => 0).WithName("GetForecastsByCity");
            }
            """);

        var endpoints = _scanner.Scan(_root);

        var e = Assert.Single(endpoints);
        Assert.Equal("GET", e.Method);
        Assert.Equal("api/v1/weather/forecasts/{city:alpha}", e.Route);
        Assert.Equal("GetForecastsByCity", e.Name);
    }

    [Theory]
    [InlineData("MapPost", "POST")]
    [InlineData("MapPut", "PUT")]
    [InlineData("MapPatch", "PATCH")]
    [InlineData("MapDelete", "DELETE")]
    public void Detects_verb_for_each_map_method(string mapCall, string expectedVerb)
    {
        Write($"Endpoints/X/X{expectedVerb}Endpoint.cs", $$"""
            public sealed class XEndpoint : IEndpoint
            {
                public static string Route => "api/v1/x";
                public static void Map(IEndpointRouteBuilder app) =>
                    app.{{mapCall}}(Route, () => 0).WithName("X{{expectedVerb}}");
            }
            """);

        var e = Assert.Single(_scanner.Scan(_root));
        Assert.Equal(expectedVerb, e.Method);
    }

    [Fact]
    public void Skips_files_that_do_not_implement_IEndpoint()
    {
        Write("Common/Helpers.cs", """
            public sealed class Helpers
            {
                public static string Route => "api/v1/nope";
            }
            """);

        Assert.Empty(_scanner.Scan(_root));
    }

    [Fact]
    public void Records_unnamed_when_WithName_is_missing()
    {
        Write("Endpoints/X/NoNameEndpoint.cs", """
            public sealed class NoNameEndpoint : IEndpoint
            {
                public static string Route => "api/v1/no-name";
                public static void Map(IEndpointRouteBuilder app) => app.MapGet(Route, () => 0);
            }
            """);

        var e = Assert.Single(_scanner.Scan(_root));
        Assert.Equal("(unnamed)", e.Name);
    }

    [Fact]
    public void Skips_bin_and_obj_directories()
    {
        Write("bin/Debug/Generated.cs", """
            public sealed class Stale : IEndpoint
            {
                public static string Route => "api/v1/stale";
                public static void Map(IEndpointRouteBuilder app) => app.MapGet(Route, () => 0).WithName("Stale");
            }
            """);
        Write("obj/Debug/Generated.cs", """
            public sealed class Stale2 : IEndpoint
            {
                public static string Route => "api/v1/stale2";
                public static void Map(IEndpointRouteBuilder app) => app.MapGet(Route, () => 0).WithName("Stale2");
            }
            """);

        Assert.Empty(_scanner.Scan(_root));
    }

    [Fact]
    public void Results_are_ordered_by_route()
    {
        Write("Endpoints/B.cs", """
            public sealed class B : IEndpoint
            {
                public static string Route => "api/v1/bbb";
                public static void Map(IEndpointRouteBuilder app) => app.MapGet(Route, () => 0).WithName("B");
            }
            """);
        Write("Endpoints/A.cs", """
            public sealed class A : IEndpoint
            {
                public static string Route => "api/v1/aaa";
                public static void Map(IEndpointRouteBuilder app) => app.MapGet(Route, () => 0).WithName("A");
            }
            """);

        var endpoints = _scanner.Scan(_root);

        Assert.Collection(endpoints,
            e => Assert.Equal("api/v1/aaa", e.Route),
            e => Assert.Equal("api/v1/bbb", e.Route));
    }

    [Fact]
    public void Relative_path_is_anchored_at_webapi_root()
    {
        Write("Endpoints/Weather/GetEndpoint.cs", """
            public sealed class GetEndpoint : IEndpoint
            {
                public static string Route => "api/v1/weather";
                public static void Map(IEndpointRouteBuilder app) => app.MapGet(Route, () => 0).WithName("Get");
            }
            """);

        var e = Assert.Single(_scanner.Scan(_root));
        Assert.Equal(Path.Combine("Endpoints", "Weather", "GetEndpoint.cs"), e.RelativePath);
    }

    private void Write(string relativePath, string contents)
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, contents);
    }
}
