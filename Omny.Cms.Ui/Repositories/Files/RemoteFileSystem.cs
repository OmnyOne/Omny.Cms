using Omny.Cms.Files;
using System.Threading.Tasks;

namespace Omny.Cms.UiRepositories.Files;

public class RemoteFileSystem : IFileSystem
{
    private readonly IRemoteFileService _service;

    public RemoteFileSystem(IRemoteFileService service)
    {
        _service = service;
    }

    public async Task<bool> FileExistsAsync(string path)
    {
        var result = await _service.GetFileContentsAsync(path);
        return result.Contents != null;
    }

    public async Task<string> ReadAllTextAsync(string path)
    {
        var result = await _service.GetFileContentsAsync(path);
        return result.Contents ?? string.Empty;
    }
}
