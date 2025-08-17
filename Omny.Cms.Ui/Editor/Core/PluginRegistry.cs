using Omny.Cms.Editor;
using Omny.Cms.Editor.Fields;
using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.Plugins.Fields;
using Omny.Cms.Editor.Plugins;

namespace Omny.Cms.Editor;

public class PluginRegistry(
    IEnumerable<IContentTypePlugin> contentTypePlugins, 
    IEditorService editorService,
    IEnumerable<IFieldPlugin> fieldPlugins) : IPluginRegistry
{
    public IFieldPlugin GetEditorPlugin(Type pluginType)
    {
        var plugin = fieldPlugins.FirstOrDefault(p => p.GetType() == pluginType);
        if (plugin == null)
        {
            throw new InvalidOperationException($"Plugin of type {pluginType.Name} is not registered");
        }
        return plugin;
    }

    public IEnumerable<IFieldPlugin> GetAllEditorPlugins()
    {
        var htmlEditor = editorService.GetHtmlEditor();
        List<IFieldPlugin> pluginsToSkip = new();
       if (string.Equals(htmlEditor, "quill", StringComparison.OrdinalIgnoreCase))
       {
            pluginsToSkip.AddRange(fieldPlugins.Where(p => p.GetType() == typeof(TinyMceHtmlEditorPlugin)));
        }
        else
        {
            pluginsToSkip.AddRange(fieldPlugins.Where(p => p.GetType() == typeof(QuillHtmlEditorPlugin)));
        }


        var plugins = fieldPlugins.Where(p => !pluginsToSkip.Contains(p)).ToList();
        return plugins;
    }

    public IFieldPlugin GetFieldPlugin(string fieldType)
    {
        foreach (var plugin in GetAllFieldPlugins())
        {
            if (string.Equals(plugin.FieldType, fieldType, StringComparison.OrdinalIgnoreCase))
            {
                return plugin;
            }
        }
        throw new InvalidOperationException($"Field plugin for {fieldType} not registered");
    }

    public IEnumerable<IFieldPlugin> GetAllFieldPlugins()
    {
        var htmlEditor = editorService.GetHtmlEditor();
    
                List<IFieldPlugin> pluginsToSkip = new();
        if (string.Equals(htmlEditor, "quill", StringComparison.OrdinalIgnoreCase))
        {
            pluginsToSkip.AddRange(fieldPlugins.Where(p => p.GetType() == typeof(TinyMceHtmlEditorPlugin)));
        }
        else
        {
            pluginsToSkip.AddRange(fieldPlugins.Where(p => p.GetType() == typeof(QuillHtmlEditorPlugin)));
        }


        var plugins = fieldPlugins.Where(p => !pluginsToSkip.Contains(p)).ToList();
        return plugins;
    }

    public IContentTypePlugin GetContentTypePlugin(string name)
    {
        foreach (var plugin in contentTypePlugins)
        {
            if (string.Equals(plugin.Name, name, StringComparison.OrdinalIgnoreCase))
                return plugin;
        }
        throw new InvalidOperationException($"Content type plugin {name} not registered");
    }

    public IEnumerable<IContentTypePlugin> GetAllContentTypePlugins()
    {
        return contentTypePlugins;
    }
}
