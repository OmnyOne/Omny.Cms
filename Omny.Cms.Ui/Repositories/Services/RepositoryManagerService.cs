using Blazored.LocalStorage;
using Omny.Cms.UiRepositories.Models;

namespace Omny.Cms.UiRepositories.Services;

public class RepositoryManagerService : IRepositoryManagerService, IAdvancedUserCheck
{
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

    public async Task SetCurrentRepositoryAsync(RepositoryInfo repository)
    {
        _currentRepository = repository;
        await _localStorage.SetItemAsync(CurrentRepositoryKey, repository);
        CurrentRepositoryChanged?.Invoke(repository);
    }

    public async Task<bool> IsAdvancedUserAsync()
    {
        var repo = await GetCurrentRepositoryAsync();
        return repo!.ShowAdvancedOptions;
    }
}