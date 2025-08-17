using System;

namespace Omny.Cms.Editor;

public record ContentType(string Name, string? Icon = null);

public record ContentItem(string ContentType, string Name, string[] FilePaths)
{
    public Dictionary<string, object>? FieldValues { get; init; }
    public bool IsNew { get; init; }
    public string[] OriginalFilePaths { get; init; } = Array.Empty<string>();
    public string? OriginalFolderPath { get; init; }
}

public class ContentItemData
{
    public string ContentType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object>? FieldValues { get; set; }
}
