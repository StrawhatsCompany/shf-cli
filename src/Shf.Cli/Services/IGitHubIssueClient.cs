namespace Shf.Cli.Services;

public interface IGitHubIssueClient
{
    /// <summary>
    /// Detect the GitHub repo from the cwd's git remote (origin → URL → owner/name).
    /// Returns null if cwd is not a git repo or origin isn't a GitHub URL.
    /// </summary>
    string? DetectRepoFromGit(string startDirectory);

    /// <summary>
    /// Ensures the given label exists on the repo. Idempotent — silently succeeds if the
    /// label is already there. Returns false only if the gh CLI itself fails.
    /// </summary>
    bool EnsureLabel(string repo, string name, string description, string colorHex);

    /// <summary>
    /// Creates an issue. Returns the issue number (parsed from the URL) on success,
    /// or null on failure.
    /// </summary>
    int? CreateIssue(string repo, string title, string body, IReadOnlyCollection<string> labels);

    /// <summary>
    /// Edits an issue body — used after the initial create to substitute
    /// {{slug:foo}} placeholders with the resolved issue numbers from other slugs.
    /// </summary>
    bool EditIssueBody(string repo, int issueNumber, string body);
}