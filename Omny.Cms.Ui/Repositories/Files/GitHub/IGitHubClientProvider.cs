using System.Threading.Tasks;
using Octokit;

namespace Omny.Cms.UiRepositories.Files.GitHub
{
    public interface IGitHubClientProvider
    {
        Task<GitHubClient> GetClientAsync();
        Task<Repository> GetRepositoryAsync();
        Task<string> GetBranchShaAsync(bool refresh = false);
        Task<string> GetBranchShaAsync(string branch, bool refresh = false);
        Task UpdateCachedBranchShaAsync(string sha);
    }
}
