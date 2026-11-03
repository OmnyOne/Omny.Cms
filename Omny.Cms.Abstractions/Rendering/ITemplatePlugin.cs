using Omny.Cms.Editor;
using Omny.Cms.Manifest;

namespace Omny.Cms.Rendering;

public interface ITemplatePlugin
{
    /// <summary>Selects the template file to use when rendering a page.</summary>
    /// <param name="contentItem">The content item being rendered.</param>
    /// <param name="manifest">The site's manifest.</param>
    /// <param name="pagePath">The page's output file name, including its extension.</param>
    /// <returns>A template path resolved by the configured file system.</returns>
    string GetTemplatePath(ContentItem contentItem, OmnyManifest manifest, string pagePath);
}
