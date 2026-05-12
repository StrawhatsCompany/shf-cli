namespace Shf.Cli.Services;

public interface IEndpointScanner
{
    /// <summary>
    /// Scans the given WebApi project root for files implementing <c>IEndpoint</c> and extracts
    /// the HTTP method, route pattern, operation name, and relative file path for each.
    /// Returns an empty list when the directory does not exist or no endpoints are found.
    /// </summary>
    IReadOnlyList<EndpointInfo> Scan(string webApiRoot);
}

public sealed record EndpointInfo(string Method, string Route, string Name, string RelativePath);
