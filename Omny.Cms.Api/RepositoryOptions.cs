namespace Omny.Api;

public class RepositoryInfo
{
    public string Owner { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty; // Friendly name for people
    public string Branch { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    
    public bool ShowAdvancedOptions { get; set; } = true;

    public string? BuildActionsToWatch { get; set; }

    public string? PreviewUrl { get; set; }

    public bool HasWorkflowDispatch { get; set; } = false;

    public ImageStorageOptions? ImageStorage { get; set; }

    public bool UseLeftItemSelector { get; set; } = false;

    public string? LocalPath { get; set; }
    public string? DatabaseConnectionString { get; set; }

    public bool UseApiFileService => !string.IsNullOrEmpty(LocalPath) || !string.IsNullOrEmpty(DatabaseConnectionString);
    
    public HashSet<string> UserEmails { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}


public class ImageStorageOptions
{
    public string? BucketName { get; set; }
    public string? BucketServiceUrl { get; set; }
    public string? BucketPrefix { get; set; }
    public string? CdnUrl { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string? Region { get; set; }
}

public class RepositoryOptions
{
    public const string SectionName = "Repositories";
    
    public List<RepositoryInfo> Available { get; set; } = new();
}