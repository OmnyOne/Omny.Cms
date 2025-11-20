using System.Collections.Generic;
using System.IO;
using Omny.Cms.Files;
using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.Manifest;
using Omny.Cms.Editor;
using System.Text.Json;
using System.Text.Json.Serialization;
using Omny.Cms.Rendering;


namespace Omny.Cms.Rendering.ContentRendering;

public class OmnyPageRenderer(
    IFileSystem fileSystem,
    IContentTypeSerializer serializer,
    IEnumerable<IPagePlugin> pagePlugins) : IContentTypeRenderer
{
    private readonly IEnumerable<IPagePlugin> _pagePlugins = pagePlugins;

    public string ContentType => "Omny.Page";
    public string GetOutputFileName(ContentItem contentItem, OmnyManifest manifest)
    {
        if (contentItem.FieldValues != null &&
            contentItem.FieldValues.TryGetValue("Path", out var pathObj))
        {
            string? path = pathObj switch
            {
                string s => s,
                JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString(),
                _ => null
            };
            if (!string.IsNullOrEmpty(path))
            {
                return $"{path.Trim('/')}.html";
            }
        }

        return $"{contentItem.Name}.html";
    }

    public string RenderContentType(ContentItem contentItem, OmnyManifest manifest)
    {
        string pagePath = GetOutputFileName(contentItem, manifest);
        string bodyHtml = string.Empty;
        if (manifest.ContentTypeDefinitions.TryGetValue(contentItem.ContentType, out var meta) && meta.Folder is not null)
        {
            if (meta.PluginType != ContentType)
            {
                throw new InvalidOperationException($"Content type {contentItem.ContentType} is not supported by this renderer. Expected an implementation of {ContentType}.");
            }
            string folderName = serializer.GetContentItemFolderName(contentItem, meta, contentItem.FieldValues);
            string folder = Path.Combine(meta.Folder, folderName);

            if (contentItem.FieldValues != null && contentItem.FieldValues.TryGetValue("Body", out var bodyVal))
            {
                bodyHtml = bodyVal switch
                {
                    string s => s,
                    JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? string.Empty,
                    _ => string.Empty
                };
            }

            if (contentItem.FieldValues != null && contentItem.FieldValues.TryGetValue("MoreContent", out var moreObj) && moreObj is JsonElement listElem)
            {
                if (listElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in listElem.EnumerateArray())
                    {
                        AppendEntry(entry);
                    }
                }
                else if (listElem.ValueKind == JsonValueKind.Object && listElem.TryGetProperty("fields", out _))
                {
                    var list = listElem.Deserialize<CollectionFileList>();
                    if (list?.Fields != null)
                    {
                        for (int i = 0; i < list.Fields.Length; i++)
                        {
                            var file = list.Fields[i];
                            var type = list.Types?[i] ?? "html";
                            var full = Path.Combine(folder, file);
                            if (!fileSystem.FileExistsAsync(full).GetAwaiter().GetResult())
                                continue;
                            var content = fileSystem.ReadAllTextAsync(full).GetAwaiter().GetResult();
                            ProcessContent(type, content);
                        }
                    }
                }
            }

            void AppendEntry(JsonElement entry)
            {
                string type = entry.GetProperty("type").GetString() ?? "html";
                if (entry.TryGetProperty("value", out var val))
                {
                    string content = val.ValueKind == JsonValueKind.String ? val.GetString() ?? string.Empty : val.GetRawText();
                    ProcessContent(type, content);
                }
                else if (entry.TryGetProperty("file", out var fileElem))
                {
                    string file = fileElem.GetString() ?? string.Empty;
                    if (string.IsNullOrEmpty(file)) return;
                    string full = Path.Combine(folder, file);
                    if (!fileSystem.FileExistsAsync(full).GetAwaiter().GetResult()) return;
                    string content = fileSystem.ReadAllTextAsync(full).GetAwaiter().GetResult();
                    ProcessContent(type, content);
                }
            }

            void ProcessContent(string type, string content)
            {
                if (type.Equals("imagetext", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var obj = JsonSerializer.Deserialize<ImageText>(content);
                        if (obj != null)
                        {
                            var link = $"<img src=\"/{obj.image}\"' alt=\"{obj.text}\" />";
                            bodyHtml += "\n" + link;
                        }
                    }
                    catch { }
                }
                else if (type.Equals("image", StringComparison.OrdinalIgnoreCase))
                {
                    bodyHtml += $"<a href='/{content}'>Image</a>";
                }
                else
                {
                    bodyHtml += "\n" + content;
                }
            }
        }

        string menuHtml = BuildMenuHtml(manifest);

        string templateName = manifest.CustomData.TryGetValue("PageTemplate", out var val) ? val?.ToString() ?? "" : "";
            string templatePath = string.IsNullOrEmpty(templateName)
                ? Path.Combine("content/template.html")
                : templateName;
            if (fileSystem.FileExistsAsync(templatePath).GetAwaiter().GetResult())
            {
                var template = fileSystem.ReadAllTextAsync(templatePath).GetAwaiter().GetResult();
                string renderedTemplate = template
                    .Replace("{{Body}}", bodyHtml)
                    .Replace("{{Menu}}", menuHtml)
                    .Replace("{{Title}}", contentItem.Name);

            return ApplyPagePlugins(renderedTemplate, contentItem, manifest, pagePath);
            }

        return ApplyPagePlugins(bodyHtml, contentItem, manifest, pagePath);
    }

    private record CollectionFileList(
        [property: JsonPropertyName("fields")] string[] Fields,
        [property: JsonPropertyName("types")] string[]? Types);
    private record ImageText(string image, string? text);
    private record ContentItemData(
        [property: JsonPropertyName("ContentType")] string ContentType,
        [property: JsonPropertyName("Name")] string Name,
        [property: JsonPropertyName("FieldValues")] Dictionary<string, object>? FieldValues);
    private record ValueTypeSet<T>(
        [property: JsonPropertyName("Type")] string Type,
        [property: JsonPropertyName("Value")] T Value);
    
    private record MenuItemData(
        [property: JsonPropertyName("External")] bool External,
        [property: JsonPropertyName("Name")] string? Name,
        [property: JsonPropertyName("Link")] string? Link,
        [property: JsonPropertyName("Target")] string? Target);

    private string BuildMenuHtml(OmnyManifest manifest)
    {
        if (!manifest.ContentTypeDefinitions.TryGetValue("Menu", out var meta) || meta.Folder is null)
            return string.Empty;

        string menuFolder = Path.Combine(meta.Folder, "Main Menu");
        string fieldsJson = Path.Combine(menuFolder, "fields.json");
        if (!fileSystem.FileExistsAsync(fieldsJson).GetAwaiter().GetResult())
            return string.Empty;

        ContentItemData? data;
        try
        {
            data = JsonSerializer.Deserialize<ContentItemData>(fileSystem.ReadAllTextAsync(fieldsJson).GetAwaiter().GetResult());
        }
        catch
        {
            return string.Empty;
        }

        if (data?.FieldValues == null || !data.FieldValues.TryGetValue("Items", out var listObj))
            return string.Empty;
        
        if (listObj is JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.Object && elem.TryGetProperty("fields", out _))
            {
                // write object to temp file string for parsing
                var list = elem.Deserialize<CollectionFileList>();
                if (list?.Fields != null)
                {
                    List<MenuItemData> items = new();
                    for (int i = 0; i < list.Fields.Length; i++)
                    {
                        var file = list.Fields[i];
                        var type = list.Types?[i] ?? "MenuItem";
                    if (!string.Equals(type, "MenuItem", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var itemPath = Path.Combine(menuFolder, file);
                    if (fileSystem.FileExistsAsync(itemPath).GetAwaiter().GetResult())
                    {
                        try
                        {
                            var mi = JsonSerializer.Deserialize<MenuItemData>(fileSystem.ReadAllTextAsync(itemPath).GetAwaiter().GetResult());
                            if (mi != null)
                                items.Add(mi);
                        }
                        catch { }
                    }
                    }
                    if (items.Count == 0) return string.Empty;
                    return BuildMenuHtmlFromItems(items, manifest);
                }
                return string.Empty;
            }
            else if (elem.ValueKind == JsonValueKind.Array)
            {
                var items = elem.Deserialize<List<ValueTypeSet<MenuItemData>>>(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
                if (items == null || items.Count == 0) return string.Empty;
                return BuildMenuHtmlFromItems(items.Select(i => i.Value!).ToList(), manifest);
            }
        }

        return string.Empty;
        string BuildMenuHtmlFromItems(List<MenuItemData> itemsList, OmnyManifest manifest)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<ul class=\"menu\">");
            foreach (var item in itemsList)
            {
                string href = item.External
                    ? item.Link ?? string.Empty
                    : "/" + (item.Target ?? string.Empty).TrimStart('/');
                string target = item.External ? " target=\"_blank\"" : string.Empty;
                string text = item.Name ?? href;
            if (item is { External: false, Target: not null })
            {
                // fetch the name from the content item if available
                if (manifest.ContentTypeDefinitions.TryGetValue("Page", out var targetMeta) &&
                    targetMeta.Folder is not null)
                {
                    string itemPath = Path.Combine(targetMeta.Folder, item.Target, "fields.json");
                    if (fileSystem.FileExistsAsync(itemPath).GetAwaiter().GetResult())
                    {
                        try
                        {
                            var contentItemData = JsonSerializer.Deserialize<ContentItemData>(fileSystem.ReadAllTextAsync(itemPath).GetAwaiter().GetResult());
                            if (!string.IsNullOrEmpty(contentItemData?.Name) )
                            {
                                text = contentItemData.Name;
                            }
                        }
                        catch { }
                    }
                }
                if (!item.Target.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                {
                    href += ".html";
                }
            }

            sb.AppendLine($"<li><a href=\"{href}\"{target}>{text}</a></li>");
        }
        sb.AppendLine("</ul>");
        return sb.ToString();
    }

    }

    private string ApplyPagePlugins(string content, ContentItem contentItem, OmnyManifest manifest, string pagePath)
    {
        string updatedContent = content;

        foreach (var plugin in _pagePlugins)
        {
            Dictionary<string, string>? replacements = plugin.Render(contentItem, manifest, pagePath);

            if (replacements == null)
            {
                continue;
            }

            foreach (var replacement in replacements)
            {
                string token = $"{{{{{replacement.Key}}}}}";
                updatedContent = updatedContent.Replace(token, replacement.Value);
            }
        }

        return updatedContent;
    }
}
