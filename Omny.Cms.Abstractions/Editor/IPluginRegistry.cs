using Omny.Cms.Editor.Fields;
using Omny.Cms.Editor.ContentTypes;

namespace Omny.Cms.Editor;

public interface IPluginRegistry
{
    IFieldPlugin GetEditorPlugin(Type pluginType);

    IFieldPlugin GetFieldPlugin(string fieldType);
    IEnumerable<IFieldPlugin> GetAllFieldPlugins();

    IContentTypePlugin GetContentTypePlugin(string name);
    IEnumerable<IContentTypePlugin> GetAllContentTypePlugins();
}
