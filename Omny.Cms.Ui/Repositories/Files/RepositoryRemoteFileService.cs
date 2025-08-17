using Omny.Cms.UiRepositories.Files;
using Omny.Cms.UiRepositories.Files.GitHub;
using Omny.Cms.UiRepositories.Services;
using Omny.Cms.UiRepositories.Models;

namespace Omny.Cms.UiRepositories.Files;

public class RepositoryRemoteFileService : IRemoteFileService
{
    private readonly IRepositoryManagerService _repoManager;
    private readonly GitHubFileService _gitHubService;
    private readonly ApiFileService _apiService;

    public RepositoryRemoteFileService(IRepositoryManagerService repoManager, GitHubFileService gitHubService, ApiFileService apiService)
    {
        _repoManager = repoManager;
        _gitHubService = gitHubService;
        _apiService = apiService;
    }

    private async Task<IRemoteFileService> GetServiceAsync()
    {
        var repo = await _repoManager.GetCurrentRepositoryAsync();
        if (repo?.UseApiFileService == true)
        {
            return _apiService;
        }

        return _gitHubService;
    }

    public async Task<IEnumerable<CacheableTreeItem>> GetFilesAsync()
    {
        var service = await GetServiceAsync();
        return await service.GetFilesAsync();
    }

    public async Task<RemoteFileContents> GetFileContentsAsync(string path)
    {
        var service = await GetServiceAsync();
        return await service.GetFileContentsAsync(path);
    }

    public async Task<string?> GetLatestMarkdownContentAsync()
    {
        var service = await GetServiceAsync();
        return await service.GetLatestMarkdownContentAsync();
    }

    public async Task RenameFolderAsync(string oldFolderPath, string newFolderPath)
    {
        var service = await GetServiceAsync();
        await service.RenameFolderAsync(oldFolderPath, newFolderPath);
    }

    public async Task DeleteFolderAsync(string folderPath)
    {
        var service = await GetServiceAsync();
        await service.DeleteFolderAsync(folderPath);
    }

    public async Task DeleteFilesAsync(string[] filePaths)
    {
        var service = await GetServiceAsync();
        await service.DeleteFilesAsync(filePaths);
    }

    public async Task WriteFilesAsync(Dictionary<string, string> filesWithContents)
    {
        var service = await GetServiceAsync();
        await service.WriteFilesAsync(filesWithContents);
    }

    public async Task WriteBinaryFilesAsync(Dictionary<string, byte[]> filesWithContents)
    {
        var service = await GetServiceAsync();
        await service.WriteBinaryFilesAsync(filesWithContents);
    }
}

