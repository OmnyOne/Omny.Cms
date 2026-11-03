using Omny.Cms.Editor;
using Omny.Cms.Manifest;

namespace Omny.Cms.Rendering;

public class DefaultTemplatePlugin : ITemplatePlugin
{
    public string GetTemplatePath(ContentItem contentItem, OmnyManifest manifest, string pagePath)
    {
        string? templateName = null;
        if (manifest.CustomData != null && manifest.CustomData.TryGetValue("PageTemplate", out var value))
        {
            templateName = value?.ToString();
        }

        string templatePath = string.IsNullOrEmpty(templateName) ? "content/template.html" : templateName;
        return templatePath;
    }
}
