using System.Net.Http.Json;
using Omny.Cms.UiRepositories.Models;

namespace Omny.Cms.UiRepositories.Services;

public interface IRepositoryService
{
    Task<List<RepositoryInfo>> GetRepositoriesAsync();
}

public class RepositoryService : IRepositoryService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public RepositoryService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<RepositoryInfo>> GetRepositoriesAsync()
    {
        var httpClient = _httpClientFactory.CreateClient("ApiClient");
        var response = await httpClient.GetAsync("repositories");
        response.EnsureSuccessStatusCode();
        
        var repositories = await response.Content.ReadFromJsonAsync<List<RepositoryInfo>>();
        return repositories ?? new List<RepositoryInfo>();
    }
}