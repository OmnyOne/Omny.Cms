namespace Omny.Cms.UiRepositories.Models;

public class RepositoryInfo
{
    public string Owner { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string TargetBranch { get; set; } = "main";
    public string Token { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    
    public bool ShowAdvancedOptions { get; set; } = false;

    public string? BuildActionsToWatch { get; set; }

    public bool HasWorkflowDispatch { get; set; } = false;

    public bool? NeedsPrToMerge { get; set; }

    public string? PreviewUrl { get; set; }

    public ImageStorageOptions? ImageStorage { get; set; }

    public bool UseLeftItemSelector { get; set; } = false;

    public bool UseApiFileService { get; set; } = false;
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
