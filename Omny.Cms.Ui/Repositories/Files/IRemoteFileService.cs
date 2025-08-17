using System.Collections.Generic;
using System.Threading.Tasks;
using Omny.Cms.UiRepositories.Models;

namespace Omny.Cms.UiRepositories.Files
{
    public interface IRemoteFileService
    {
        Task<IEnumerable<CacheableTreeItem>> GetFilesAsync();
        Task<RemoteFileContents> GetFileContentsAsync(string path);
        Task<string?> GetLatestMarkdownContentAsync();

        Task RenameFolderAsync(string oldFolderPath, string newFolderPath);

        Task DeleteFolderAsync(string folderPath);

        Task DeleteFilesAsync(string[] filePaths);

        Task WriteFilesAsync(Dictionary<string, string> filesWithContents);

        Task WriteBinaryFilesAsync(Dictionary<string, byte[]> filesWithContents);
    }
}
