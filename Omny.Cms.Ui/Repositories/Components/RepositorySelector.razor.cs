using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Omny.Cms.UiRepositories.Models;
using Omny.Cms.UiRepositories.Services;
using MudBlazor;
using System;

namespace Omny.Cms.UiRepositories.Components;

public class RepositorySelectorBase : ComponentBase, IDisposable
{
    [Inject] protected IRepositoryManagerService RepositoryManager { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
    [Inject] protected BuildWatcherService BuildWatcher { get; set; } = default!;
    [Inject] protected ISnackbar Snackbar { get; set; } = default!;
    [Inject] protected IDialogService DialogService { get; set; } = default!;
    [Inject] protected DeploymentService DeploymentService { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;

    protected List<RepositoryInfo>? repositories;
    protected RepositoryInfo? currentRepository;
    protected string? selectedRepositoryId;
    protected string? DeploymentRequestUrl;
    private string? _lastStatus;

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
        selectedRepositoryId = GetCurrentRepositoryId();
        RepositoryManager.CurrentRepositoryChanged += OnCurrentRepositoryChanged;
        BuildWatcher.StatusChanged += OnBuildStatusChanged;
        await RefreshDeploymentStatusAsync();
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

    protected async void OnCurrentRepositoryChanged(RepositoryInfo repository)
    {
        currentRepository = repository;
        selectedRepositoryId = GetRepositoryId(repository);
        await RefreshDeploymentStatusAsync();
        InvokeAsync(StateHasChanged);
    }

    private void OnBuildStatusChanged()
    {
        InvokeAsync(() =>
        {
            if (BuildWatcher.Status != _lastStatus && !string.IsNullOrEmpty(BuildWatcher.Status))
            {
                Severity severity;
                if (BuildWatcher.Status.EndsWith("Completed", StringComparison.OrdinalIgnoreCase))
                {
                    severity = Severity.Success;
                }
                else if (BuildWatcher.Status.EndsWith("Failed", StringComparison.OrdinalIgnoreCase))
                {
                    severity = Severity.Error;
                }
                else
                {
                    severity = Severity.Info;
                }
                Snackbar.Clear();
                Snackbar.Add(BuildWatcher.Status, severity, options =>
                {
                    if (BuildWatcher.CurrentBuildUrl != null)
                    {
                        options.Action = "View build";
                        var url = BuildWatcher.CurrentBuildUrl;
                        options.OnClick = _ =>
                        {
                            JS.InvokeVoidAsync("open", url, "_blank");
                            return Task.CompletedTask;
                        };
                    }
                    options.RequireInteraction = BuildWatcher.IsUpdating;
                    if (BuildWatcher.IsUpdating)
                    {
                        options.VisibleStateDuration = int.MaxValue;
                    }
                });
                _lastStatus = BuildWatcher.Status;
            }
            StateHasChanged();
        });
    }

    protected async Task OnRepositoryChanged(string? selectedId)
    {
        if (string.IsNullOrEmpty(selectedId) || repositories == null)
        {
            return;
        }

        selectedRepositoryId = selectedId;
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
            selectedRepositoryId = GetRepositoryId(repo);
            StateHasChanged();
        }
    }

    protected async Task TriggerUpdate()
    {
        await BuildWatcher.TriggerWorkflowAsync();
    }

    protected async Task Deploy()
    {
        await DeploymentService.MergeAsync();
        await BuildWatcher.StartWatchingAsync(true, "Deployment");
    }

    protected async Task RequestDeployment()
    {
        DeploymentRequestUrl = await DeploymentService.CreatePullRequestAsync();
        StateHasChanged();
    }

    private async Task RefreshDeploymentStatusAsync()
    {
        if (currentRepository?.NeedsPrToMerge == true)
        {
            DeploymentRequestUrl = await DeploymentService.GetOpenPullRequestUrlAsync();
        }
        else
        {
            DeploymentRequestUrl = null;
        }
    }

    public void Dispose()
    {
        RepositoryManager.CurrentRepositoryChanged -= OnCurrentRepositoryChanged;
        BuildWatcher.StatusChanged -= OnBuildStatusChanged;
    }
}
