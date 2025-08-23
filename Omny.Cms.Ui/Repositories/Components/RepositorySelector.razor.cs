using Microsoft.AspNetCore.Components;
using Omny.Cms.UiRepositories.Models;
using Omny.Cms.UiRepositories.Services;
using MudBlazor;

namespace Omny.Cms.UiRepositories.Components;

public class RepositorySelectorBase : ComponentBase, IDisposable
{
    [Inject] protected IRepositoryManagerService RepositoryManager { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
    [Inject] protected BuildWatcherService BuildWatcher { get; set; } = default!;
    [Inject] protected ISnackbar Snackbar { get; set; } = default!;
    [Inject] protected IDialogService DialogService { get; set; } = default!;

    protected List<RepositoryInfo>? repositories;
    protected RepositoryInfo? currentRepository;

    protected static bool IsFreeVersion =>
#if FREE_VERSION
        true;
#else
        false;
#endif

    protected override async Task OnInitializedAsync()
    {
#if FREE_VERSION
        repositories = await RepositoryManager.GetRepositoriesAsync();
        currentRepository = await RepositoryManager.GetCurrentRepositoryAsync();
#else
        repositories = await RepositoryManager.GetRepositoriesAsync();
        currentRepository = await RepositoryManager.GetCurrentRepositoryAsync();
#endif
        RepositoryManager.CurrentRepositoryChanged += OnCurrentRepositoryChanged;
        BuildWatcher.StatusChanged += OnBuildStatusChanged;
        BuildWatcher.BuildCompleted += OnBuildCompleted;
        StateHasChanged();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
#if FREE_VERSION
        if (firstRender)
        {
            if (repositories == null)
            {
                return;
            }

            if (repositories.Count == 0)
            {
                await OpenAddRepositoryDialogAsync();
            }
        }
#endif
    }

    protected void OnCurrentRepositoryChanged(RepositoryInfo repository)
    {
        currentRepository = repository;
        InvokeAsync(StateHasChanged);
    }

    private void OnBuildStatusChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    protected async Task OnRepositoryChanged(ChangeEventArgs e)
    {
        var selectedId = e.Value?.ToString();
        if (string.IsNullOrEmpty(selectedId) || repositories == null)
            return;

        var selectedRepo = repositories.FirstOrDefault(r => GetRepositoryId(r) == selectedId);
        if (selectedRepo != null)
        {
            await RepositoryManager.SetCurrentRepositoryAsync(selectedRepo);
        }
        NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);
    }

    protected string GetRepositoryId(RepositoryInfo repo)
    {
        return $"{repo.Owner}/{repo.Name}#{repo.Branch}";
    }

    protected string GetCurrentRepositoryId()
    {
        return currentRepository != null ? GetRepositoryId(currentRepository) : "";
    }

    protected async Task OpenAddRepositoryDialogAsync()
    {
        var dialog = await DialogService.ShowAsync<AddRepositoryDialog>("Add Repository");
        var result = await dialog.Result;
        if (result.Canceled)
        {
            return;
        }

        if (result.Data is RepositoryInfo repo)
        {
            await RepositoryManager.AddRepositoryAsync(repo);
            repositories = await RepositoryManager.GetRepositoriesAsync();
            currentRepository = repo;
            StateHasChanged();
        }
    }

    protected async Task TriggerUpdate()
    {
        await BuildWatcher.TriggerWorkflowAsync();
    }

    private void OnBuildCompleted()
    {
        Snackbar.Add("Site updated", Severity.Success);
    }

    public void Dispose()
    {
        RepositoryManager.CurrentRepositoryChanged -= OnCurrentRepositoryChanged;
        BuildWatcher.StatusChanged -= OnBuildStatusChanged;
        BuildWatcher.BuildCompleted -= OnBuildCompleted;
    }
}
