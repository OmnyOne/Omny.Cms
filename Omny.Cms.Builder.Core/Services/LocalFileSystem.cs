using System.IO;
using System.Threading.Tasks;
using Omny.Cms.Files;

namespace Omny.Cms.Builder.Services;

public class LocalFileSystem : IFileSystem
{
    public string BasePath { get; set; }

    public LocalFileSystem(string basePath)
    {
        BasePath = basePath;
    }

    private string Resolve(string path)
    {
        return Path.IsPathRooted(path) ? path : Path.Combine(BasePath, path);
    }

    public Task<bool> FileExistsAsync(string path)
    {
        return Task.FromResult(File.Exists(Resolve(path)));
    }

    public Task<string> ReadAllTextAsync(string path)
    {
        return File.ReadAllTextAsync(Resolve(path));
    }
}
