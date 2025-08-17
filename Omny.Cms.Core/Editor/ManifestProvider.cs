using Omny.Cms.Abstractions.Manifest;
using Omny.Cms.Manifest;

namespace Omny.Cms.Core.Editor;

public class ManifestProvider : IManifestProvider
{
    public OmnyManifest? Manifest { get; set; }
}
