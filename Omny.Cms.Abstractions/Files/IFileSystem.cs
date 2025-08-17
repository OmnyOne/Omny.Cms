using System.Threading.Tasks;

namespace Omny.Cms.Files;

public interface IFileSystem
{
    Task<bool> FileExistsAsync(string path);
    Task<string> ReadAllTextAsync(string path);
}
