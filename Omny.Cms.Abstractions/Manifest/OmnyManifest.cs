namespace Omny.Cms.Manifest;

public record BuildConfiguration(string OutputDirectory, string[] StaticAssetPaths);

public class OmnyManifest
{
    public const string ManifestFileName = "omny.manifest.json";
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    // Changed ContentTypeDefinitions to use string as key for easier JSON deserialization
    public Dictionary<string, ContentTypeMetadata> ContentTypeDefinitions { get; set; } = new();
    public Dictionary<string, FieldTypeDefinition>? FieldTypeDefinitions { get; set; }

    public string? ImageLocation { get; set; }

    public bool RandomImageNames { get; set; }

    public string? HtmlEditor { get; set; }

    public Dictionary<string, ContentTypePluginConfiguration[]>? ContentTypePluginConfigurations { get; set; }

    public int MaxImageSizeKb { get; set; } = 1024;
    
    public BuildConfiguration? BuildConfiguration { get; set; }
    public Dictionary<string, object>? CustomData { get; set; }
}

public record ContentTypePluginConfiguration(string Name, string? Folder);

public record ContentTypeMetadata
{
    public string? PluginType { get; init; }
    public Dictionary<string, string>? FileExtensionToIdMapping { get; init; }
    public string? Folder { get; init; }
    public string? FolderField { get; init; }
    public FieldDefinition[]? Fields { get; init; }
    public string? Icon { get; init; }
}

public record FieldDefinition(string Name, string FieldType, string? Extension, string? Label = null, bool IsAdvanced = false);

public record SubFieldDefinition(string FieldType, string? Label);
public record FieldTypeDefinition(string Type, string[]? AllowedFieldTypes = null, SubFieldDefinition[]? SubFields = null);
