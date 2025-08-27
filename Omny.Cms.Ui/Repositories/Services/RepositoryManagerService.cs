using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Omny.Cms.UiRepositories.Models;
using System.Text;
using System.Text.Json;

namespace Omny.Cms.UiRepositories.Services;

public class RepositoryManagerService : IRepositoryManagerService, IAdvancedUserCheck
{
#if FREE_VERSION
    private readonly ILocalStorageService _localStorage;
    private readonly NavigationManager _navigationManager;
    private readonly List<RepositoryInfo> _repositories = new();
    private RepositoryInfo? _currentRepository;
    private bool _repoParamChecked;
    private const string RepositoriesKey = "repositories";
    private const string CurrentRepositoryKey = "current-repository";

    public event Action<RepositoryInfo>? CurrentRepositoryChanged;

    public RepositoryManagerService(ILocalStorageService localStorage, NavigationManager navigationManager)
    {
        _localStorage = localStorage;
        _navigationManager = navigationManager;
    }

    private async Task CheckRepoQueryParameterAsync()
    {
        if (_repoParamChecked)
        {
            return;
        }

        _repoParamChecked = true;

        var uri = _navigationManager.ToAbsoluteUri(_navigationManager.Uri);
        var query = QueryHelpers.ParseQuery(uri.Query);
        if (!query.TryGetValue("repo", out var repoParam))
        {
            return;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(repoParam.ToString()));
            var repo = JsonSerializer.Deserialize<RepositoryInfo>(json);
            if (repo == null)
            {
                return;
            }

            if (_repositories.Count == 0)
            {
                var stored = await _localStorage.GetItemAsync<List<RepositoryInfo>>(RepositoriesKey);
                if (stored != null)
                {
                    _repositories.AddRange(stored);
                }
            }

            _repositories.RemoveAll(r => r.Name == repo.Name);
            _repositories.Add(repo);
            await _localStorage.SetItemAsync(RepositoriesKey, _repositories);
            await SetCurrentRepositoryAsync(repo);
        }
        catch
        {
            // Ignore invalid repo parameter
        }
    }

    public async Task<List<RepositoryInfo>> GetRepositoriesAsync()
    {
        await CheckRepoQueryParameterAsync();
        if (_repositories.Count == 0)
        {
            var stored = await _localStorage.GetItemAsync<List<RepositoryInfo>>(RepositoriesKey);
            if (stored != null)
            {
                _repositories.AddRange(stored);
            }
        }

        return _repositories;
    }

    public async Task<RepositoryInfo?> GetCurrentRepositoryAsync()
    {
        await CheckRepoQueryParameterAsync();
        if (_currentRepository == null)
        {
            _currentRepository = await _localStorage.GetItemAsync<RepositoryInfo>(CurrentRepositoryKey);
        }

        return _currentRepository;
    }

    public async Task AddRepositoryAsync(RepositoryInfo repository)
    {
        _repositories.RemoveAll(r => r.Name == repository.Name);
        _repositories.Add(repository);
        await _localStorage.SetItemAsync(RepositoriesKey, _repositories);
        await SetCurrentRepositoryAsync(repository);
    }

    public async Task SetCurrentRepositoryAsync(RepositoryInfo repository)
    {
        _currentRepository = repository;
        await _localStorage.SetItemAsync(CurrentRepositoryKey, repository);
        CurrentRepositoryChanged?.Invoke(repository);
    }

    public async Task<bool> IsAdvancedUserAsync()
    {
        var repo = await GetCurrentRepositoryAsync();
        if (repo == null)
        {
            return false;
        }

        return repo.ShowAdvancedOptions;
    }
#else
    private readonly IRepositoryService _repositoryService;
    private readonly ILocalStorageService _localStorage;
    private List<RepositoryInfo>? _cachedRepositories;
    private RepositoryInfo? _currentRepository;
    private const string CurrentRepositoryKey = "current-repository";

    public event Action<RepositoryInfo>? CurrentRepositoryChanged;

    public RepositoryManagerService(IRepositoryService repositoryService, ILocalStorageService localStorage)
    {
        _repositoryService = repositoryService;
        _localStorage = localStorage;
    }

    public async Task<List<RepositoryInfo>> GetRepositoriesAsync()
    {
        if (_cachedRepositories == null)
        {
            _cachedRepositories = await _repositoryService.GetRepositoriesAsync();
        }

        return _cachedRepositories;
    }

    public async Task<RepositoryInfo?> GetCurrentRepositoryAsync()
    {
        if (_currentRepository == null)
        {
            // Try to load from localStorage first
            _currentRepository = await _localStorage.GetItemAsync<RepositoryInfo>(CurrentRepositoryKey);

            // If not found, use the first available repository
            if (_currentRepository == null)
            {
                var repositories = await GetRepositoriesAsync();
                _currentRepository = repositories.FirstOrDefault();

                if (_currentRepository != null)
                {
                    await _localStorage.SetItemAsync(CurrentRepositoryKey, _currentRepository);
                }
            }
        }

        return _currentRepository;
    }

    public Task AddRepositoryAsync(RepositoryInfo repository)
    {
        return Task.CompletedTask;
    }

    public async Task SetCurrentRepositoryAsync(RepositoryInfo repository)
    {
        _currentRepository = repository;
        await _localStorage.SetItemAsync(CurrentRepositoryKey, repository);
        CurrentRepositoryChanged?.Invoke(repository);
    }

    public async Task<bool> IsAdvancedUserAsync()
    {
        var repo = await GetCurrentRepositoryAsync();
        if (repo == null)
        {
            return false;
        }

        return repo.ShowAdvancedOptions;
    }
#endif
}
