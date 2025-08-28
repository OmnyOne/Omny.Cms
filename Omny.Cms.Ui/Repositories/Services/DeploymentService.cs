using Octokit;
using Omny.Cms.UiRepositories.Models;
using System.Collections.Generic;
using System.Linq;
using Omny.Cms.UiRepositories.Files.GitHub;

namespace Omny.Cms.UiRepositories.Services;

public class DeploymentService
{
    private readonly IGitHubClientProvider _gitHubClientProvider;
    private readonly IRepositoryManagerService _repositoryManager;

    public DeploymentService(IGitHubClientProvider gitHubClientProvider, IRepositoryManagerService repositoryManager)
    {
        _gitHubClientProvider = gitHubClientProvider;
        _repositoryManager = repositoryManager;
    }

    public async Task MergeAsync()
    {
        RepositoryInfo? repo = await _repositoryManager.GetCurrentRepositoryAsync();
        if (repo == null)
        {
            return;
        }

        IGitHubClient client = await _gitHubClientProvider.GetClientAsync();
        NewMerge merge = new(repo.TargetBranch, repo.Branch);
        await client.Repository.Merging.Create(repo.Owner, repo.RepoName, merge);
    }

    public async Task<string?> GetOpenPullRequestUrlAsync()
    {
        RepositoryInfo? repo = await _repositoryManager.GetCurrentRepositoryAsync();
        if (repo == null)
        {
            return null;
        }

        IGitHubClient client = await _gitHubClientProvider.GetClientAsync();
        PullRequestRequest prRequest = new()
        {
            State = ItemStateFilter.Open,
            Head = $"{repo.Owner}:{repo.Branch}",
            Base = repo.TargetBranch
        };

        IReadOnlyList<PullRequest> prs = await client.PullRequest.GetAllForRepository(repo.Owner, repo.RepoName, prRequest);
        PullRequest? pr = prs.FirstOrDefault();
        return pr?.HtmlUrl;
    }

    public async Task<string?> CreatePullRequestAsync()
    {
        RepositoryInfo? repo = await _repositoryManager.GetCurrentRepositoryAsync();
        if (repo == null)
        {
            return null;
        }

        IGitHubClient client = await _gitHubClientProvider.GetClientAsync();
        NewPullRequest newPr = new($"Deployment from {repo.Branch} to {repo.TargetBranch}", repo.Branch, repo.TargetBranch);
        PullRequest pr = await client.PullRequest.Create(repo.Owner, repo.RepoName, newPr);
        return pr.HtmlUrl;
    }
}
