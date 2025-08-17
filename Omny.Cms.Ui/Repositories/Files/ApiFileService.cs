using System.Net;
using System.Net.Http.Json;
using Octokit;
using Omny.Cms.UiRepositories.Files;
using Omny.Cms.UiRepositories.Models;

namespace Omny.Cms.UiRepositories.Files;

public class ApiFileService : IRemoteFileService
{
    private readonly HttpClient _client;

    public ApiFileService(HttpClient client)
    {
        _client = client;
    }

    public async Task<IEnumerable<CacheableTreeItem>> GetFilesAsync()
    {
        var items = await _client.GetFromJsonAsync<List<CacheableTreeItem>>("files");
        return items ?? new List<CacheableTreeItem>();
    }

    public async Task<RemoteFileContents> GetFileContentsAsync(string path)
    {
       var res = await _client.GetAsync($"file?path={Uri.EscapeDataString(path)}");
        if (res.StatusCode == HttpStatusCode.NotFound)
        {
            return new RemoteFileContents(string.Empty, path, null);
        }

        var result = await res.Content.ReadFromJsonAsync<RemoteFileContents>();
        return result ?? new RemoteFileContents(string.Empty, path, null);
    }

    public async Task<string?> GetLatestMarkdownContentAsync()
    {
        var files = await GetFilesAsync();
        var file = files.FirstOrDefault(f => f.Path.EndsWith(".md"));
        if (file != null)
        {
            var c = await GetFileContentsAsync(file.Path);
            return c.Contents;
        }
        return null;
    }

    public async Task RenameFolderAsync(string oldFolderPath, string newFolderPath)
    {
        await _client.PostAsJsonAsync("rename-folder", new { oldFolderPath, newFolderPath });
    }

    public async Task DeleteFolderAsync(string folderPath)
    {
        await _client.DeleteAsync($"folder?folderPath={folderPath}");
    }

    public async Task DeleteFilesAsync(string[] filePaths)
    {
        await _client.PostAsJsonAsync("delete-files", filePaths);
    }

    public async Task WriteFilesAsync(Dictionary<string, string> filesWithContents)
    {
        await _client.PostAsJsonAsync("write-files", filesWithContents);
    }

    public async Task WriteBinaryFilesAsync(Dictionary<string, byte[]> filesWithContents)
    {
        var payload = filesWithContents.ToDictionary(kvp => kvp.Key, kvp => Convert.ToBase64String(kvp.Value));
        await _client.PostAsJsonAsync("write-binary-files", payload);
    }
}
