using System.IO;
using System.Collections.Generic;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System.Threading.Tasks;
using System.Linq;
using Omny.Cms.UiRepositories.Models;
using Omny.Cms.UiRepositories.Services;

namespace Omny.Cms.UiImages.Services;

public class S3ImageStorageService : IImageStorageService
{
    private readonly IRepositoryManagerService _repositoryManager;

    public S3ImageStorageService(IRepositoryManagerService repositoryManager)
    {
        _repositoryManager = repositoryManager;
    }

    public async Task UploadImageAsync(string path, byte[] data)
    {
        var repo = await _repositoryManager.GetCurrentRepositoryAsync();
        var storage = repo?.ImageStorage;
        
        using var amazonS3Client = await GetS3Client(storage);
        if (storage is null || amazonS3Client is null)
        {
            return;
        }
        
        using var stream = new MemoryStream(data);
        
        string prefix = string.IsNullOrEmpty(storage.BucketPrefix)
            ? string.Empty
            : storage.BucketPrefix.TrimEnd('/') + "/";
        var request = new PutObjectRequest
        {
            BucketName = storage.BucketName,
            Key = prefix + path,
            InputStream = stream
        };
        await amazonS3Client!.PutObjectAsync(request);
    }

    public async Task<string> GetPublicUrlAsync(string path)
    {
        var repo = await _repositoryManager.GetCurrentRepositoryAsync();
        var cdn = repo?.ImageStorage?.CdnUrl;
        if (cdn is null)
        {
            return path;
        }
        var prefix = repo.ImageStorage?.BucketPrefix;
        var key = string.IsNullOrEmpty(prefix)
            ? path
            : $"{prefix.TrimEnd('/')}/{path}";
        return $"{cdn.TrimEnd('/')}/{key}";
    }

    public async Task<IEnumerable<string>> ListImagesAsync(string folder)
    {
        var repo = await _repositoryManager.GetCurrentRepositoryAsync();
        var storage = repo?.ImageStorage;
        using var amazonS3Client = await GetS3Client(storage);
        if (storage is null || amazonS3Client is null)
        {
            return [];
        }
        var bucketName = storage.BucketName;
        string prefix = string.IsNullOrEmpty(storage.BucketPrefix)
            ? string.Empty
            : storage.BucketPrefix.TrimEnd('/') + "/";

        var request = new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = prefix + folder.TrimEnd('/') + "/"
        };
        var response = await amazonS3Client.ListObjectsV2Async(request);
        return response.S3Objects.Select(o => Path.GetFileName(o.Key));
    }

    private async Task<AmazonS3Client?> GetS3Client(ImageStorageOptions? storage)
    {
        AmazonS3Client? client = null;
        try
        {
            if (storage?.BucketName is null || storage.AccessKey is null || storage.SecretKey is null || storage.Region is null)
            {
                return client;
            }

            var creds = new BasicAWSCredentials(storage.AccessKey, storage.SecretKey);
            var config = new AmazonS3Config();
            if (storage.BucketServiceUrl is not null)
            {
                config.ServiceURL = storage.BucketServiceUrl.TrimEnd('/');
                config.ForcePathStyle = true; // Crucial for LocalStack S3 compatibility
            }
            else
            {
                config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(storage.Region);  
                config.AuthenticationRegion = storage.Region; // Can be any valid region, LocalStack doesn't enforce
            }
            client = new AmazonS3Client(creds, config);
        }
        catch
        {
            client?.Dispose();
            throw;
        }
        return client;
    }
}
