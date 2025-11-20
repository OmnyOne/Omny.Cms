using System.Collections.Generic;
using Omny.Cms.Editor;
using Omny.Cms.Manifest;

namespace Omny.Cms.Rendering;

public interface IPagePlugin
{
    string Name { get; }

    Dictionary<string, string> Render(ContentItem contentItem, OmnyManifest manifest, string pagePath);
}
