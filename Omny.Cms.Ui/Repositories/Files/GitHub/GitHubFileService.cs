using Octokit;
using Omny.Cms.UiRepositories.Files;
using Omny.Cms.Editor;
using Omny.Cms.UiRepositories.Models;
using Omny.Cms.UiRepositories.Services;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using Omny.Cms.Manifest;

namespace Omny.Cms.UiRepositories.Files.GitHub
{
    public class GitHubFileService : IRemoteFileService
    {
        private readonly IGitHubClientProvider _gitHubClientProvider;
        private readonly IRepositoryManagerService _repositoryManager;
        private const string RemoteContentStateKey = "remote-content-state";
        private bool _initialized = false;

        public GitHubFileService(IGitHubClientProvider gitHubClientProvider, IRepositoryManagerService repositoryManager)
        {
            _gitHubClientProvider = gitHubClientProvider;
            _repositoryManager = repositoryManager;

            _repositoryManager.CurrentRepositoryChanged += async _ => await PreloadRepositoryDataAsync();
            _ = PreloadRepositoryDataAsync();
        }

        private async Task<RepositoryInfo> GetCurrentRepositoryAsync()
        {
            var repository = await _repositoryManager.GetCurrentRepositoryAsync();
            return repository ?? new RepositoryInfo();
        }

        private static string GetRepoKey(RepositoryInfo info) => $"{info.Owner}/{info.RepoName}/{info.Branch}";

        private async Task<IEnumerable<CacheableTreeItem>> GetRemoteFilesAsync(RepositoryInfo repositoryInfo)
        {
            // Return empty list if no valid repository is configured
            if (string.IsNullOrEmpty(repositoryInfo.Owner) || string.IsNullOrEmpty(repositoryInfo.RepoName))
            {
                return new List<CacheableTreeItem>();
            }

            var client = await _gitHubClientProvider.GetClientAsync();
            Repository repo = await _gitHubClientProvider.GetRepositoryAsync();
            string sha = await _gitHubClientProvider.GetBranchShaAsync();
            var data = await client.Git.Tree.GetRecursive(repo.Id, sha);
            return data.Tree.Select(t => new CacheableTreeItem(t.Path, t.Sha)).ToArray();
        }
        
        public async Task RenameFolderAsync(string oldFolderPath, string newFolderPath)
        {
            IGitHubClient client = await _gitHubClientProvider.GetClientAsync();
            RepositoryInfo repositoryInfo = await GetCurrentRepositoryAsync();
            Repository repo = await _gitHubClientProvider.GetRepositoryAsync();
            string sha = await _gitHubClientProvider.GetBranchShaAsync();
            Commit latestCommit = await client.Git.Commit.Get(repo.Id, sha);

            TreeResponse tree = await client.Git.Tree.GetRecursive(repo.Id, sha);
            List<NewTreeItem> newTreeItems = new();

            // 1. Collect the files to move (those in oldFolderPath)
            List<string> pathsToMove = tree.Tree
                .Where(t => t.Type == TreeType.Blob && t.Path.StartsWith(oldFolderPath, StringComparison.Ordinal))
                .Select(t => t.Path)
                .ToList();

            // 2. Add renamed files
            foreach (TreeItem item in tree.Tree.Where(t =>
                         t.Type == TreeType.Blob &&
                         t.Path.StartsWith(oldFolderPath, StringComparison.Ordinal)))
            {
                string newPath = newFolderPath + item.Path.Substring(oldFolderPath.Length);

                newTreeItems.Add(new NewTreeItem
                {
                    Path = newPath,
                    Mode = item.Mode,
                    Type = item.Type!.Value,
                    Sha = item.Sha
                });
            }

            // 3. Add the rest of the files (not moved/renamed)
            foreach (TreeItem item in tree.Tree
                .Where(t => t.Type == TreeType.Blob && !pathsToMove.Contains(t.Path)))
            {
                newTreeItems.Add(new NewTreeItem
                {
                    Path = item.Path,
                    Mode = item.Mode,
                    Type = item.Type!.Value,
                    Sha = item.Sha
                });
            }

            NewTree newTree = new();
            foreach (NewTreeItem item in newTreeItems)
            {
                newTree.Tree.Add(item);
            }

            TreeResponse createdTree = await client.Git.Tree.Create(repo.Id, newTree);
            NewCommit newCommit = new NewCommit($"Rename folder {oldFolderPath} to {newFolderPath}", createdTree.Sha, latestCommit.Sha);
            Commit commit = await client.Git.Commit.Create(repo.Id, newCommit);

            await client.Git.Reference.Update(repo.Id, $"heads/{repositoryInfo.Branch}", new ReferenceUpdate(commit.Sha));
            await _gitHubClientProvider.UpdateCachedBranchShaAsync(commit.Sha);

            _initialized = false;
            await PreloadRepositoryDataAsync();
        }

        public async Task DeleteFolderAsync(string folderPath)
        {
            IGitHubClient client = await _gitHubClientProvider.GetClientAsync();
            RepositoryInfo repositoryInfo = await GetCurrentRepositoryAsync();
            Repository repo = await _gitHubClientProvider.GetRepositoryAsync();
            string sha = await _gitHubClientProvider.GetBranchShaAsync();
            Commit latestCommit = await client.Git.Commit.Get(repo.Id, sha);

            TreeResponse tree = await client.Git.Tree.GetRecursive(repo.Id, sha);

            List<NewTreeItem> remaining = tree.Tree
                .Where(t => t.Type == TreeType.Blob && !t.Path.StartsWith(folderPath, StringComparison.Ordinal))
                .Select(t => new NewTreeItem
                {
                    Path = t.Path,
                    Mode = t.Mode,
                    Type = t.Type!.Value,
                    Sha = t.Sha
                })
                .ToList();

            NewTree newTree = new NewTree();
            foreach (NewTreeItem item in remaining)
            {
                newTree.Tree.Add(item);
            }

            TreeResponse createdTree = await client.Git.Tree.Create(repo.Id, newTree);
            NewCommit newCommit = new NewCommit($"Delete folder {folderPath}", createdTree.Sha, latestCommit.Sha);
            Commit commit = await client.Git.Commit.Create(repo.Id, newCommit);

            await client.Git.Reference.Update(repo.Id, $"heads/{repositoryInfo.Branch}", new ReferenceUpdate(commit.Sha));
            await _gitHubClientProvider.UpdateCachedBranchShaAsync(commit.Sha);

            _initialized = false;
            await PreloadRepositoryDataAsync();
        }

        public async Task DeleteFilesAsync(string[] filePaths)
        {
            IGitHubClient client = await _gitHubClientProvider.GetClientAsync();
            RepositoryInfo repositoryInfo = await GetCurrentRepositoryAsync();
            Repository repo = await _gitHubClientProvider.GetRepositoryAsync();
            string sha = await _gitHubClientProvider.GetBranchShaAsync();
            Commit latestCommit = await client.Git.Commit.Get(repo.Id, sha);

            TreeResponse tree = await client.Git.Tree.GetRecursive(repo.Id, sha);

            HashSet<string> toDelete = filePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            List<NewTreeItem> remaining = tree.Tree
                .Where(t => t.Type == TreeType.Blob && !toDelete.Contains(t.Path))
                .Select(t => new NewTreeItem
                {
                    Path = t.Path,
                    Mode = t.Mode,
                    Type = t.Type!.Value,
                    Sha = t.Sha
                })
                .ToList();

            NewTree newTree = new NewTree();
            foreach (NewTreeItem item in remaining)
            {
                newTree.Tree.Add(item);
            }

            TreeResponse createdTree = await client.Git.Tree.Create(repo.Id, newTree);
            NewCommit newCommit = new NewCommit($"Delete files", createdTree.Sha, latestCommit.Sha);
            Commit commit = await client.Git.Commit.Create(repo.Id, newCommit);

            await client.Git.Reference.Update(repo.Id, $"heads/{repositoryInfo.Branch}", new ReferenceUpdate(commit.Sha));
            await _gitHubClientProvider.UpdateCachedBranchShaAsync(commit.Sha);

            _initialized = false;
            await PreloadRepositoryDataAsync();
        }

        private async Task<RemoteFileContents> GetRemoteFileContentsAsync(RepositoryInfo repositoryInfo, string path)
        {
            // Return empty content if no valid repository is configured
            if (string.IsNullOrEmpty(repositoryInfo.Owner) || string.IsNullOrEmpty(repositoryInfo.RepoName))
            {
                return new RemoteFileContents(repositoryInfo.Branch, path, null);
            }

            var client = await _gitHubClientProvider.GetClientAsync();
            Repository repo = await _gitHubClientProvider.GetRepositoryAsync();
            string sha = await _gitHubClientProvider.GetBranchShaAsync();

            try
            {
                var fileData = await client.Repository.Content.GetAllContentsByRef(
                    repositoryId: repo.Id,
                    path: path,
                    reference: sha);

                var first = fileData.FirstOrDefault();
                string extension = Path.GetExtension(path);
                if (first != null)
                {
                    var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
                    // Octokit decodes base64 automatically. If the file was stored as base64
                    // we fetch the raw bytes instead so callers receive a base64 string.
                    if ( !imageExtensions.Contains(extension, System.StringComparer.OrdinalIgnoreCase) )
                        
                    //string.Equals(first.Encoding, "base64", System.StringComparison.OrdinalIgnoreCase))
                    {
                        return new RemoteFileContents(repositoryInfo.Branch, path, first.Content);
                    }
                    

                    
                }
                var raw = await client.Repository.Content.GetRawContentByRef(repositoryInfo.Owner, repositoryInfo.RepoName, path, sha);
                return new RemoteFileContents(repositoryInfo.Branch, path, System.Convert.ToBase64String(raw));
            }
            catch (NotFoundException)
            {
                // File might be binary or not returned via Contents API; try raw access below
            }

            

            return new RemoteFileContents(repositoryInfo.Branch, path, null);
        }

        private async Task<string> GetImageFolderAsync()
        {
            var repoInfo = await GetCurrentRepositoryAsync();
            var manifest = await GetRemoteFileContentsAsync(repoInfo, "omny.manifest.json");
            if (manifest.Contents != null)
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<OmnyManifest>(manifest.Contents);
                    if (parsed?.ImageLocation != null)
                    {
                        return parsed.ImageLocation;
                    }
                }
                catch { }
            }
            return "content/images";
        }

        private async Task EnsureInitializedAsync()
        {
            if (!_initialized)
            {
                await PreloadRepositoryDataAsync();
            }
        }

        private async Task PreloadRepositoryDataAsync()
        {
            var repositoryInfo = await GetCurrentRepositoryAsync();
            if (string.IsNullOrEmpty(repositoryInfo.Owner))
            {
                _initialized = true;
                return;
            }

            // Preload manifest to warm up caches if desired
            await GetRemoteFileContentsAsync(repositoryInfo, "omny.manifest.json");
            _initialized = true;
        }



        public async Task<IEnumerable<CacheableTreeItem>> GetFilesAsync()
        {
            await EnsureInitializedAsync();
            var repositoryInfo = await GetCurrentRepositoryAsync();
            return await GetRemoteFilesAsync(repositoryInfo);
        }

        public async Task<RemoteFileContents> GetFileContentsAsync(string path)
        {
            await EnsureInitializedAsync();
            var repositoryInfo = await GetCurrentRepositoryAsync();
            return await GetRemoteFileContentsAsync(repositoryInfo, path);
        }

        public async Task<string?> GetLatestMarkdownContentAsync()
        {
            var repositoryInfo = await GetCurrentRepositoryAsync();
            var cacheableTree = (await GetFilesAsync()).ToArray();
            var file = cacheableTree.FirstOrDefault(f => f.Path.EndsWith(".md"));
            if (file != null)
            {
                var remoteFileContents = await GetFileContentsAsync(file.Path);
                return remoteFileContents.Contents;
            }
            return null;
        }

       
        public async Task WriteFilesAsync(Dictionary<string, string> filesWithContents)
        {
            IGitHubClient client = await _gitHubClientProvider.GetClientAsync();
            var repositoryInfo = await GetCurrentRepositoryAsync();
            Repository repo = await _gitHubClientProvider.GetRepositoryAsync();
            string sha = await _gitHubClientProvider.GetBranchShaAsync();
            Commit latestCommit = await client.Git.Commit.Get(repo.Id, sha);

            List<NewTreeItem> treeItems = new List<NewTreeItem>();

            foreach (KeyValuePair<string, string> kvp in filesWithContents)
            {
                string path = kvp.Key;
                string content = kvp.Value;

                BlobReference blob = await client.Git.Blob.Create(
                    repo.Id,
                    new NewBlob
                    {
                        Encoding = EncodingType.Utf8,
                        Content = content
                    });

                treeItems.Add(new NewTreeItem
                {
                    Path = path,
                    Mode = Octokit.FileMode.File,
                    Type = TreeType.Blob,
                    Sha = blob.Sha
                });
            }

            NewTree newTree = new NewTree { BaseTree = latestCommit.Tree.Sha };
            foreach (NewTreeItem item in treeItems)
            {
                
                newTree.Tree.Add(item);
            }

            TreeResponse createdTree = await client.Git.Tree.Create(repo.Id, newTree);

            NewCommit newCommit = new NewCommit($"Update via Omny CMS", createdTree.Sha, sha);
            Commit commit = await client.Git.Commit.Create(repo.Id, newCommit);

            await client.Git.Reference.Update(repo.Id, $"heads/{repositoryInfo.Branch}", new ReferenceUpdate(commit.Sha));
            await _gitHubClientProvider.UpdateCachedBranchShaAsync(commit.Sha);

            _initialized = false;
            await PreloadRepositoryDataAsync();
        }

        public async Task WriteBinaryFilesAsync(Dictionary<string, byte[]> filesWithContents)
        {
            IGitHubClient client = await _gitHubClientProvider.GetClientAsync();
            var repositoryInfo = await GetCurrentRepositoryAsync();
            Repository repo = await _gitHubClientProvider.GetRepositoryAsync();
            string sha = await _gitHubClientProvider.GetBranchShaAsync();
            Commit latestCommit = await client.Git.Commit.Get(repo.Id, sha);

            List<NewTreeItem> treeItems = new();
            foreach (var kvp in filesWithContents)
            {
                string path = kvp.Key;
                byte[] data = kvp.Value;

                BlobReference blob = await client.Git.Blob.Create(
                    repo.Id,
                    new NewBlob
                    {
                        Encoding = EncodingType.Base64,
                        Content = Convert.ToBase64String(data)
                    });

                treeItems.Add(new NewTreeItem
                {
                    Path = path,
                    Mode = Octokit.FileMode.File,
                    Type = TreeType.Blob,
                    Sha = blob.Sha
                });
            }

            NewTree newTree = new NewTree { BaseTree = latestCommit.Tree.Sha };
            foreach (NewTreeItem item in treeItems)
            {
                newTree.Tree.Add(item);
            }

            TreeResponse createdTree = await client.Git.Tree.Create(repo.Id, newTree);
            NewCommit newCommit = new NewCommit($"Update via Omny CMS", createdTree.Sha, sha);
            Commit commit = await client.Git.Commit.Create(repo.Id, newCommit);

            await client.Git.Reference.Update(repo.Id, $"heads/{repositoryInfo.Branch}", new ReferenceUpdate(commit.Sha));
            await _gitHubClientProvider.UpdateCachedBranchShaAsync(commit.Sha);

            _initialized = false;
            bool exists = false;
            while(!exists)
            {
                var results = await GetRemoteFilesAsync(repositoryInfo);
                exists = filesWithContents.Keys.All(k => results.Any(r => r.Path.Equals(k, StringComparison.OrdinalIgnoreCase)));
                // check that the files exist in the list
                await Task.Delay(200);
                
            }
            await PreloadRepositoryDataAsync();
        }

    }
}
