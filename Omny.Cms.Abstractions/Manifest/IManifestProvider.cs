using Omny.Cms.Manifest;

namespace Omny.Cms.Abstractions.Manifest;

public interface IManifestProvider
{
    OmnyManifest? Manifest { get; }
}
