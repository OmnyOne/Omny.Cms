using System.IO;
using System.Threading.Tasks;
using Omny.Cms.Files;

namespace Omny.Cms.Plugins.Infrastructure;

public class PluginLocalFileSystem : IFileSystem
{
    public string BasePath { get; set; }

    public PluginLocalFileSystem(string basePath)
    {
        BasePath = basePath;
    }

    private string Resolve(string path) => Path.IsPathRooted(path) ? path : Path.Combine(BasePath, path);

    public Task<bool> FileExistsAsync(string path) => Task.FromResult(File.Exists(Resolve(path)));

    public Task<string> ReadAllTextAsync(string path) => File.ReadAllTextAsync(Resolve(path));
}
