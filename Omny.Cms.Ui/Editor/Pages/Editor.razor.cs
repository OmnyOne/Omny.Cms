using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Omny.Cms.UiRepositories.Models;
using Omny.Cms.UiRepositories.Files;
using Omny.Cms.Editor;
using Microsoft.JSInterop;
using Omny.Cms.Manifest;
using Omny.Cms.UiEditor.Components;
using Omny.Cms.UiRepositories.Services;
using Omny.Cms.UiImages.Components;
using System.Text.Json;
using System.Linq;

namespace Omny.Cms.UiEditor.Pages;

public class EditorBase : ComponentBase, IDisposable
{
    [Inject] private HttpClient ApiClient { get; set; } = default!;
    [Inject] private IRemoteFileService RemoteFileService { get; set; } = default!;
    [Inject] private IEditorService EditorService { get; set; } = default!;
    [Inject] private IPluginRegistry PluginRegistry { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private IAdvancedUserCheck AdvancedUserCheck { get; set; } = default!;
    [Inject] private BuildWatcherService BuildWatcher { get; set; } = default!;
    [Inject] private IRepositoryManagerService RepositoryManager { get; set; } = default!;
    
    protected ContentItem? _selectedContentItem = null;
    protected string? _selectedContentType = null;
    protected readonly Dictionary<string, object?> _currentFieldContents = new();
    protected readonly Dictionary<string, Omny.Cms.Editor.Fields.IFieldPlugin> _currentFieldEditors = new();
    protected readonly Dictionary<string, Omny.Cms.Editor.Fields.IFieldPlugin> _currentFieldFieldPlugins = new();
    protected readonly Dictionary<string, string> _fieldKinds = new();
    protected List<FieldDefinition> _currentFieldDefs = new();
    protected bool _changesMade = false;
    protected bool _isSaving = false;

    protected bool _changingContent = false;
    protected bool _fullScreen = false;
    protected bool _isAdvancedMode = false;
    protected IEnumerable<ContentType> _contentTypesList = new List<ContentType>();
    protected Dictionary<string, List<ContentItem>> _contentTypesMap = new();
    protected RepositoryInfo? _currentRepo;
    protected string _searchTerm = string.Empty;
    protected bool _isRepositoryLoading = true;

    protected override async Task OnInitializedAsync()
    {
        _currentRepo = await RepositoryManager.GetCurrentRepositoryAsync();

        // Subscribe to repository changes to reinitialize when a repository is added
        RepositoryManager.CurrentRepositoryChanged += OnRepositoryChanged;

        _isRepositoryLoading = false;

        // Don't initialize editor if no repository is configured
        if (_currentRepo == null)
        {
            return;
        }

        await InitializeEditorAsync();
    }

    private async void OnRepositoryChanged(RepositoryInfo repository)
    {
        _currentRepo = repository;
        await InitializeEditorAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task InitializeEditorAsync()
    {
        var contentTypes = (await EditorService.GetContentTypesAsync()).ToList();
        _contentTypesList = contentTypes.ToList();

        if (_currentRepo?.UseLeftItemSelector == true)
        {
            foreach (var ct in _contentTypesList)
            {
                await LoadContentItemsAsync(ct.Name);
            }
        }
        else if (_contentTypesList.Any())
        {
            var firstType = _contentTypesList.First().Name;
            _selectedContentType = firstType;
            await LoadContentItemsAsync(firstType);
        }

        _isAdvancedMode = await AdvancedUserCheck.IsAdvancedUserAsync();
    }

    protected void OnFieldChanged(string field, object? newContent)
    {
        _currentFieldContents[field] = newContent;
        _changesMade = true;
        //StateHasChanged();
    }

    protected async Task SaveContentAsync()
    {
        if (_selectedContentItem == null || _selectedContentType == null)
        {
            return;
        }

        _isSaving = true;
        StateHasChanged();

        await EditorService.SaveContentItemAsync(
            _selectedContentType,
            _selectedContentItem,
            new Dictionary<string, object>(_currentFieldContents!));
        var newItem = _selectedContentItem with
        {
            FieldValues = new Dictionary<string, object>(_currentFieldContents!),
            IsNew = false
        };
        newItem = await EditorService.RefreshContentItemAsync(newItem);
        _changesMade = false;
        if (_contentTypesMap.TryGetValue(_selectedContentType, out var list))
        {
            var index = list.IndexOf(_selectedContentItem);
            if (index >= 0)
            {
                list[index] = newItem;
            }
        }
        
        _selectedContentItem = newItem;
        await BuildWatcher.StartWatchingAsync(quiet: true);

        _isSaving = false;
        StateHasChanged();
    }

    protected async Task LoadContentItemsAsync(string typeName)
    {
        var items = await EditorService.GetContentItemsAsync(typeName);
        _contentTypesMap[typeName] = items?.ToList() ?? new List<ContentItem>();
    }

    protected async Task AddNewItemAsync(string typeName)
    {
        var name = await JS.InvokeAsync<string?>("prompt", $"Enter name for new {typeName}");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        string? folderField = await EditorService.GetFolderFieldAsync(typeName);
        
        string? folderValue = null;
        if (!string.IsNullOrEmpty(folderField))
        {
            string folderFieldName = folderField.Replace("{", "").Replace("}", "");
            folderValue = await JS.InvokeAsync<string?>("prompt", $"Enter {folderFieldName} for new {typeName}");
            if (folderValue is null)
            {
                return;
            }
        }

        var item = await EditorService.CreateContentItemAsync(typeName, name);
        if (!string.IsNullOrEmpty(folderField))
        {
            var values = item.FieldValues ?? new Dictionary<string, object>();
            values[folderField!] = folderValue ?? string.Empty;
            item = item with { FieldValues = values };
        }
        if (!_contentTypesMap.ContainsKey(typeName))
        {
            _contentTypesMap[typeName] = new List<ContentItem>();
        }
        _contentTypesMap[typeName].Add(item);
        await SelectContentItem(typeName, item);
    }

    protected async Task SelectContentType(string typeName)
    {
        _selectedContentItem = null;
        _selectedContentType = typeName;
        if (!_contentTypesMap.ContainsKey(typeName))
        {
            await LoadContentItemsAsync(typeName);
        }
        StateHasChanged();
    }

    protected async Task SelectContentItem(string typeName, ContentItem item)
    {
        if (_selectedContentItem != null && _changesMade)
        {
            var discard = await JS.InvokeAsync<bool>("confirm", "Discard current changes?");
            if (!discard) return;
        }

        _changingContent = true;
        StateHasChanged();

        await Task.Yield();
        
        _selectedContentItem = item;
        _selectedContentType = typeName;
        _currentFieldDefs = (await EditorService.GetFieldDefinitionsAsync(typeName)).ToList();
        _currentFieldContents.Clear();
        _currentFieldEditors.Clear();
        _currentFieldFieldPlugins.Clear();
        _fieldKinds.Clear();

        foreach (var field in _currentFieldDefs)
        {
            var kind = await EditorService.GetFieldKindAsync(field.FieldType);
            _fieldKinds[field.Name] = kind;
            
            if (PluginRegistry.GetAllFieldPlugins().FirstOrDefault(p => p.FieldType == field.FieldType) is { } fp)
            {
                _currentFieldFieldPlugins[field.Name] = fp;
                if (item.FieldValues != null && item.FieldValues.TryGetValue(field.Name, out var val))
                {
                    _currentFieldContents[field.Name] = val;
                }
                else
                {
                    _currentFieldContents[field.Name] = fp.DefaultValue;
                }
            }
            else if (kind == "compound")
            {
                if (item.FieldValues != null && item.FieldValues.TryGetValue(field.Name, out var existing))
                {
                    if (existing is JsonElement je)
                    {
                        _currentFieldContents[field.Name] = je.GetRawText();
                    }
                    else if (existing is string s)
                    {
                        _currentFieldContents[field.Name] = s;
                    }
                    else
                    {
                        _currentFieldContents[field.Name] = System.Text.Json.JsonSerializer.Serialize(existing);
                    }
                }
                else
                {
                    var extFields = _currentFieldDefs.Where(f => f.Extension is not null && !f.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase)).ToList();
                    int idx = extFields.FindIndex(f => f.Name == field.Name);
                    if (idx >= 0 && idx < item.FilePaths.Length)
                    {
                        var compPath = item.FilePaths[idx];
                        var data = await RemoteFileService.GetFileContentsAsync(compPath);
                        _currentFieldContents[field.Name] = data?.Contents ?? "{}";
                    }
                    else
                    {
                        _currentFieldContents[field.Name] = "{}";
                    }
                }
            }
            else if (kind == "collection")
            {
                var entries = new List<FieldContent>();
                bool handled = false;
                if (item.FieldValues != null && item.FieldValues.TryGetValue(field.Name, out var collVal))
                {
                    if (collVal is JsonElement collElem)
                    {
                        var manifest = await EditorService.GetManifestAsync();
                        if (manifest.ContentTypeDefinitions.TryGetValue(typeName, out var meta))
                        {
                            string folderName = item.Name;
                            if (!string.IsNullOrEmpty(meta.FolderField) && item.FieldValues.TryGetValue(meta.FolderField, out var fldVal))
                            {
                                if (fldVal is JsonElement je && je.ValueKind == JsonValueKind.String)
                                    folderName = je.GetString() ?? folderName;
                                else if (fldVal is string s)
                                    folderName = s;
                            }
                            string baseDir = meta.Folder is not null ? System.IO.Path.Combine(meta.Folder, folderName).Replace("\\", "/") : string.Empty;

                            var arrayElem = collElem.ValueKind == System.Text.Json.JsonValueKind.Array ? collElem :
                                (collElem.TryGetProperty("items", out var it) && it.ValueKind == System.Text.Json.JsonValueKind.Array ? it : default);
                            if (arrayElem.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                for (int i = 0; i < arrayElem.GetArrayLength(); i++)
                                {
                                    var entry = arrayElem[i];
                                    string type = entry.GetProperty("type").GetString() ?? "text";
                                    if (entry.TryGetProperty("value", out var v))
                                    {
                                        entries.Add(new FieldContent(type, v.GetRawText()));
                                    }
                                    else if (entry.TryGetProperty("file", out var f))
                                    {
                                        string fileName = f.GetString() ?? string.Empty;
                                        string path = string.IsNullOrEmpty(baseDir) ? fileName : System.IO.Path.Combine(baseDir, fileName).Replace("\\", "/");
                                        var data = await RemoteFileService.GetFileContentsAsync(path);
                                        entries.Add(new FieldContent(type, data.Contents ?? string.Empty));
                                    }
                                }
                                handled = true;
                            }
                        }
                    }
                    else if (collVal is System.Collections.IEnumerable enumerable && collVal is not string)
                    {
                        var def = await EditorService.GetFieldTypeDefinitionAsync(field.FieldType);
                        string entryType = def?.AllowedFieldTypes?.FirstOrDefault() ?? "text";
                        foreach (var obj in enumerable)
                        {
                            entries.Add(new FieldContent(entryType, obj?.ToString() ?? string.Empty));
                        }
                        handled = true;
                    }
                }

                if (!handled)
                {
                    var extFields = _currentFieldDefs.Where(f => f.Extension is not null && !f.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase)).ToList();
                    int idx = extFields.FindIndex(f => f.Name == field.Name);
                    if (idx < 0 || idx >= item.FilePaths.Length)
                    {
                        _currentFieldContents[field.Name] = new CollectionFieldContent(new List<FieldContent>());
                    }
                    else
                    {
                        var collPath = item.FilePaths[idx];
                        var listData = await RemoteFileService.GetFileContentsAsync(collPath);
                        if (!string.IsNullOrEmpty(listData.Contents))
                        {
                            try
                            {
                                var doc = System.Text.Json.JsonDocument.Parse(listData.Contents);
                                var arrayElem = doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array ? doc.RootElement :
                                    (doc.RootElement.TryGetProperty("items", out var it) && it.ValueKind == System.Text.Json.JsonValueKind.Array ? it : default);
                                if (arrayElem.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    var def = await EditorService.GetFieldTypeDefinitionAsync(field.FieldType);
                                    for (int i = 0; i < arrayElem.GetArrayLength(); i++)
                                    {
                                        var elem = arrayElem[i];
                                        string type = def?.AllowedFieldTypes?.FirstOrDefault() ?? "text";
                                        entries.Add(new FieldContent(type, elem.GetRawText()));
                                    }
                                }
                            }
                            catch { }
                        }
                        _currentFieldContents[field.Name] = new CollectionFieldContent(entries);
                    }
                }
                else
                {
                    _currentFieldContents[field.Name] = new CollectionFieldContent(entries);
                }
            }
            else
            {
                var extFields = _currentFieldDefs.Where(f => f.Extension is not null && !f.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase)).ToList();
                int idx = extFields.FindIndex(f => f.Name == field.Name);
                if (idx >= 0 && idx < item.FilePaths.Length)
                {
                    var path = item.FilePaths[idx];
                    var pluginType = EditorService.GetEditorPluginType(path);
                    var plugin = PluginRegistry.GetEditorPlugin(pluginType);
                    _currentFieldEditors[field.Name] = plugin;
                    var data = await RemoteFileService.GetFileContentsAsync(path);
                    _currentFieldContents[field.Name] = data?.Contents ?? plugin.DefaultValue;
                }
                else if (field.Extension is not null)
                {
                    var pluginType = EditorService.GetEditorPluginType($"dummy{field.Extension}");
                    var plugin = PluginRegistry.GetEditorPlugin(pluginType);
                    _currentFieldEditors[field.Name] = plugin;
                    _currentFieldContents[field.Name] = plugin.DefaultValue;
                }
            }
        }

        _changesMade = item.IsNew;
        _changingContent = false;
        StateHasChanged();
    }

    protected void CloseItem()
    {
        _selectedContentItem = null;
        StateHasChanged();
    }

    protected IEnumerable<ContentItem> GetFilteredItems()
    {
        if (_selectedContentType == null)
            return Enumerable.Empty<ContentItem>();
        if (!_contentTypesMap.TryGetValue(_selectedContentType, out var list))
            return Enumerable.Empty<ContentItem>();
        if (string.IsNullOrWhiteSpace(_searchTerm))
            return list;
        return list.Where(i => i.Name.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase));
    }

    protected async Task RenameItemAsync(string typeName, ContentItem item)
    {
        var newName = await JS.InvokeAsync<string?>("prompt", $"Enter new name for {item.Name}", item.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == item.Name)
            return;

        await EditorService.RenameContentItemAsync(typeName, item, newName);

        if (_contentTypesMap.TryGetValue(typeName, out var list))
        {
            var index = list.FindIndex(i => i.Name == item.Name);
            if (index >= 0)
            {
                var newItem = item with { Name = newName };
                list[index] = newItem;
                if (_selectedContentItem?.Name == item.Name && _selectedContentType == typeName)
                {
                    _selectedContentItem = newItem;
                }
            }
        }

        StateHasChanged();
    }

    protected async Task DeleteItemAsync(string typeName, ContentItem item)
    {
        var confirm = await JS.InvokeAsync<bool>("confirm", $"Delete {item.Name}?");
        if (!confirm) return;

        await EditorService.DeleteContentItemAsync(typeName, item);

        if (_contentTypesMap.TryGetValue(typeName, out var list))
        {
            list.Remove(item);
        }

        if (_selectedContentItem?.Name == item.Name && _selectedContentType == typeName)
        {
            _selectedContentItem = null;
        }

        StateHasChanged();
    }

    private static string DetermineFieldTypeFromExtension(string ext, string[] allowed)
    {
        ext = ext.ToLowerInvariant();
        if ((ext == ".html" || ext == ".htm") && allowed.Any(a => a.Equals("HTML", StringComparison.OrdinalIgnoreCase)))
            return allowed.First(a => a.Equals("HTML", StringComparison.OrdinalIgnoreCase));
        if ((ext == ".md" || ext == ".markdown") && allowed.Any(a => a.Equals("markdown", StringComparison.OrdinalIgnoreCase)))
            return allowed.First(a => a.Equals("markdown", StringComparison.OrdinalIgnoreCase));
        return allowed.FirstOrDefault() ?? "text";
    }

    protected async Task OpenImageSelector()
    {
        await DialogService.ShowAsync<ImageSelector>("Images",
            new DialogParameters<ImageSelector>()
            {
                { x => x.ShouldSelectImage, false }
            },
            new DialogOptions { FullScreen = true, CloseButton = true }
        );
    }

    protected async Task ToggleFullScreen()
    {
        _fullScreen = !_fullScreen;
        var module = await JS.InvokeAsync<IJSObjectReference>("import", "../Layout/MainLayout.razor.js");
        await module.InvokeVoidAsync("setTopMenuVisible", !_fullScreen);
        StateHasChanged();
    }

    public void Dispose()
    {
        RepositoryManager.CurrentRepositoryChanged -= OnRepositoryChanged;
    }
}
