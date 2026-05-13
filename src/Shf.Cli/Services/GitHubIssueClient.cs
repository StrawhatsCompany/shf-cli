using System.Text.RegularExpressions;

namespace Shf.Cli.Services;

internal sealed partial class GitHubIssueClient(IProcessRunner processes) : IGitHubIssueClient
{
    // Matches https://github.com/<owner>/<repo>/issues/<number> in gh's create output.
    [GeneratedRegex(@"github\.com/([^/]+)/([^/]+)/issues/(\d+)", RegexOptions.Compiled)]
    private static partial Regex IssueUrlPattern();

    // Parses origin URLs: git@github.com:owner/repo.git OR https://github.com/owner/repo[.git].
    [GeneratedRegex(@"github\.com[:/](?<owner>[^/]+)/(?<repo>[^/\s.]+)(?:\.git)?", RegexOptions.Compiled)]
    private static partial Regex GitHubRemotePattern();

    public string? DetectRepoFromGit(string startDirectory)
    {
        var result = processes.RunCapturing("git", ["-C", startDirectory, "remote", "get-url", "origin"]);
        if (result.ExitCode != 0) return null;
        var match = GitHubRemotePattern().Match(result.StdOut);
        return match.Success ? $"{match.Groups["owner"].Value}/{match.Groups["repo"].Value}" : null;
    }

    public bool EnsureLabel(string repo, string name, string description, string colorHex)
    {
        // gh label create exits non-zero if the label already exists — treat that as success.
        var result = processes.RunCapturing("gh",
            ["label", "create", name, "--description", description, "--color", colorHex, "-R", repo]);
        return result.ExitCode == 0 || result.StdErr.Contains("already exists", StringComparison.OrdinalIgnoreCase);
    }

    public int? CreateIssue(string repo, string title, string body, IReadOnlyCollection<string> labels)
    {
        var args = new List<string> { "issue", "create", "--title", title, "--body", body, "-R", repo };
        if (labels.Count > 0)
        {
            args.Add("--label");
            args.Add(string.Join(",", labels));
        }
        var result = processes.RunCapturing("gh", args);
        if (result.ExitCode != 0) return null;

        var match = IssueUrlPattern().Match(result.StdOut);
        return match.Success && int.TryParse(match.Groups[3].Value, out var n) ? n : null;
    }

    public bool EditIssueBody(string repo, int issueNumber, string body)
    {
        var result = processes.RunCapturing("gh",
            ["issue", "edit", issueNumber.ToString(), "--body", body, "-R", repo]);
        return result.ExitCode == 0;
    }
}
