using Omny.Cms.Manifest;

namespace Omny.Cms.Editor.ContentTypes;

public interface IContentTypeRenderer
{
    public string ContentType { get; }

    public string GetOutputFileName(ContentItem contentItem, OmnyManifest manifest);
    public string RenderContentType(ContentItem contentItem, OmnyManifest manifest);
}