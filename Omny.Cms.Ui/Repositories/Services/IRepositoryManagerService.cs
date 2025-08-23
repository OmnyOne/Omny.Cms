using Omny.Cms.UiRepositories.Models;

namespace Omny.Cms.UiRepositories.Services;

public interface IRepositoryManagerService
{
    Task<List<RepositoryInfo>> GetRepositoriesAsync();
    Task<RepositoryInfo?> GetCurrentRepositoryAsync();
    Task AddRepositoryAsync(RepositoryInfo repository);
    Task SetCurrentRepositoryAsync(RepositoryInfo repository);
    event Action<RepositoryInfo>? CurrentRepositoryChanged;
}