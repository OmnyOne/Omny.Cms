using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Octokit;
using Octokit.Caching;
using Octokit.Internal;
using Omny.Cms.UiRepositories.Services;
using Omny.Cms.UiRepositories.Models;

namespace Omny.Cms.UiRepositories.Files.GitHub;

public record TokenResponse(string Token);



public class GitHubClientProvider : IGitHubClientProvider
{
    private readonly IRepositoryManagerService _repositoryManager;
    private readonly ConcurrentDictionary<string, GitHubClient> _clientCache = new();
    private readonly ConcurrentDictionary<string, string> _shaCache = new();
    private readonly ConcurrentDictionary<string, Repository> _repoCache = new();

    public GitHubClientProvider(IRepositoryManagerService repositoryManager)
    {
        _repositoryManager = repositoryManager;
    }

    public async Task<GitHubClient> GetClientAsync()
    {
        var currentRepository = await _repositoryManager.GetCurrentRepositoryAsync();
        if (currentRepository == null || string.IsNullOrEmpty(currentRepository.Token))
        {
            throw new InvalidOperationException("No current repository with token available");
        }

        var cacheKey = $"{currentRepository.Owner}/{currentRepository.RepoName}";
        return _clientCache.GetOrAdd(cacheKey, _ =>
        {
            var handler = HttpMessageHandlerFactory.CreateDefault();
            var adapter = new HttpClientAdapter(() => handler);
            IHttpClient httpClient = new NonCachingHttpClient(adapter);
            
            
            var connection = new Connection(
                new ProductHeaderValue("omny-cms"),
                httpClient);
            var client = new GitHubClient(connection)
            {
                Credentials = new Credentials(currentRepository.Token),
            };
            
            return client;
        });
    }

    public async Task<Repository> GetRepositoryAsync()
    {
        var repoInfo = await _repositoryManager.GetCurrentRepositoryAsync();
        if (repoInfo == null)
        {
            throw new InvalidOperationException("No current repository configured");
        }

        string key = $"{repoInfo.Owner}/{repoInfo.RepoName}";
        if (_repoCache.TryGetValue(key, out Repository cached))
        {
            return cached;
        }

        var client = await GetClientAsync();
        Repository repository = await client.Repository.Get(repoInfo.Owner, repoInfo.RepoName);
        _repoCache[key] = repository;
        return repository;
    }

    public async Task<string> GetBranchShaAsync(bool refresh = false)
    {
        RepositoryInfo? repo = await _repositoryManager.GetCurrentRepositoryAsync();
        if (repo == null)
        {
            return string.Empty;
        }

        string result = await GetBranchShaAsync(repo.Branch, refresh);
        return result;
    }

    public async Task<string> GetBranchShaAsync(string branch, bool refresh = false)
    {
        RepositoryInfo? repo = await _repositoryManager.GetCurrentRepositoryAsync();
        if (repo == null)
        {
            return string.Empty;
        }

        string key = $"{repo.Owner}/{repo.RepoName}/{branch}";
        if (!refresh && _shaCache.TryGetValue(key, out string sha))
        {
            return sha;
        }

        var client = await GetClientAsync();
        Repository repository = await GetRepositoryAsync();
        Reference reference = await client.Git.Reference.Get(repository.Id, $"heads/{branch}");
        string resultSha = reference.Object.Sha;
        _shaCache[key] = resultSha;
        return resultSha;
    }

    public async Task UpdateCachedBranchShaAsync(string sha)
    {
        var repo = await _repositoryManager.GetCurrentRepositoryAsync();
        if (repo == null)
        {
            return;
        }

        string key = $"{repo.Owner}/{repo.RepoName}/{repo.Branch}";
        _shaCache[key] = sha;
    }
}

public class NonCachingHttpClient(IHttpClient baseClient) : IHttpClient
{
    
    public void Dispose()
    {
        baseClient.Dispose();
    }

    public Task<IResponse> Send(
        IRequest request, 
        CancellationToken cancellationToken, 
        Func<object, object> preprocessResponseBody = null)
    {
        // disable caching except for content with a reference
        if (!request.Endpoint.OriginalString.Contains("?ref="))
        {
            request.Headers["If-None-Match"] = "\"" + Guid.NewGuid().ToString() + "\"";
        }

        //request.Headers["If-Modified-Since"] = DateTimeOffset.UtcNow.ToString("R");
        //request.Headers["Cache-Control"] = "no-cache";
        //request.Headers["Pragma"] = "no-cache";
        return baseClient.Send(request, cancellationToken, preprocessResponseBody);
    }

    public void SetRequestTimeout(TimeSpan timeout)
    {
        baseClient.SetRequestTimeout(timeout);
    }
}

