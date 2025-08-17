using Omny.Cms.UiRepositories.Models;

namespace Omny.Cms.UiRepositories.Services;

public interface IRepositoryManagerService
{
    Task<List<RepositoryInfo>> GetRepositoriesAsync();
    Task<RepositoryInfo?> GetCurrentRepositoryAsync();
    Task SetCurrentRepositoryAsync(RepositoryInfo repository);
    event Action<RepositoryInfo>? CurrentRepositoryChanged;
}