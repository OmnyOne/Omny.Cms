namespace Omny.Cms.UiImages.Services;

public interface IImageStorageService
{
    Task UploadImageAsync(string path, byte[] data);
    Task<string> GetPublicUrlAsync(string path);
    Task<IEnumerable<string>> ListImagesAsync(string folder);
}
