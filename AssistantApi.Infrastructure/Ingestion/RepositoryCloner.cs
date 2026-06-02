using AssistantApi.Core.Interfaces;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantApi.Infrastructure.Ingestion;

public class IngestionOptions
{
    public const string SectionName = "Ingestion";
    public string RepositoryCachePath { get; set; } = "/data/repository-cache";
    public string UploadPath { get; set; } = "/data/uploads";
}

public class RepositoryCloner : IRepositoryCloner
{
    private readonly IngestionOptions _options;
    private readonly ILogger<RepositoryCloner> _logger;

    public RepositoryCloner(IOptions<IngestionOptions> options, ILogger<RepositoryCloner> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<string> CloneOrFetchAsync(string url, string branch, string? pat, CancellationToken ct = default)
    {
        var repoName = DeriveRepoName(url);
        var localPath = Path.Combine(_options.RepositoryCachePath, repoName);

        Directory.CreateDirectory(_options.RepositoryCachePath);

        if (Repository.IsValid(localPath))
        {
            _logger.LogInformation("Fetching existing repo {RepoName} at {LocalPath}", repoName, localPath);
            using var repo = new Repository(localPath);
            var fetchOptions = BuildFetchOptions(pat);
            var remote = repo.Network.Remotes["origin"];
            var refSpecs = remote.FetchRefSpecs.Select(r => r.Specification);
            Commands.Fetch(repo, remote.Name, refSpecs, fetchOptions, "fetch");

            // Checkout the requested branch
            var targetBranch = repo.Branches[$"origin/{branch}"]
                               ?? repo.Branches[branch];
            if (targetBranch is not null)
                Commands.Checkout(repo, targetBranch);
        }
        else
        {
            _logger.LogInformation("Cloning {Url} branch {Branch} to {LocalPath}", url, branch, localPath);
            if (Directory.Exists(localPath)) Directory.Delete(localPath, recursive: true);

            var cloneOptions = new CloneOptions { BranchName = branch };
            if (!string.IsNullOrWhiteSpace(pat))
            {
                cloneOptions.FetchOptions.CredentialsProvider = (_, _, _) =>
                    new UsernamePasswordCredentials { Username = "oauth2", Password = pat };
            }
            Repository.Clone(url, localPath, cloneOptions);
        }

        return Task.FromResult(localPath);
    }

    private static FetchOptions BuildFetchOptions(string? pat)
    {
        var opts = new FetchOptions();
        if (!string.IsNullOrWhiteSpace(pat))
        {
            opts.CredentialsProvider = (_, _, _) =>
                new UsernamePasswordCredentials { Username = "oauth2", Password = pat };
        }
        return opts;
    }

    private static string DeriveRepoName(string url)
    {
        var name = url.TrimEnd('/').Split('/').Last();
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
        return string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));
    }
}
