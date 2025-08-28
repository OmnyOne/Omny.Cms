using Octokit;
using Omny.Cms.UiRepositories.Files.GitHub;
using Omny.Cms.UiRepositories.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Omny.Cms.UiRepositories.Services
{
    public class BuildWatcherService : IDisposable
    {
        private readonly IGitHubClientProvider _gitHubClientProvider;
        private readonly IRepositoryManagerService _repositoryManager;

        private Timer? _timer;
        private DateTimeOffset _startTime;
        private string[]? _actions;
        private RepositoryInfo? _repo;
        private WorkflowRun? _latestRun;
        private DateTimeOffset? _completedAt;
        private string _branch = string.Empty;
        private string _operationName = string.Empty;

        public event Action? BuildCompleted;

        public event Action? BuildFailed;

        public event Action? StatusChanged;

        public string? CurrentBuildUrl { get; private set; }
        public string Status { get; private set; } = string.Empty;
        public bool IsUpdating { get; private set; }

        public BuildWatcherService(IGitHubClientProvider gitHubClientProvider, IRepositoryManagerService repositoryManager)
        {
            _gitHubClientProvider = gitHubClientProvider;
            _repositoryManager = repositoryManager;
        }

        public async Task StartWatchingAsync(bool useTargetBranch = false, string? operationName = null)
        {
            _repo = await _repositoryManager.GetCurrentRepositoryAsync();
            if (_repo == null || _repo.UseApiFileService || string.IsNullOrWhiteSpace(_repo.BuildActionsToWatch))
            {
                return;
            }

            _actions = _repo.BuildActionsToWatch == "*" ? null :
                _repo.BuildActionsToWatch.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            await _gitHubClientProvider.GetClientAsync();
            await _gitHubClientProvider.GetRepositoryAsync();

            _branch = useTargetBranch ? _repo.TargetBranch : _repo.Branch;
            _operationName = operationName ?? (useTargetBranch ? "Deployment" : "Update Preview");

            _startTime = DateTimeOffset.UtcNow;
            _completedAt = null;
            _latestRun = null;
            _timer?.Dispose();
            Status = $"Watching {_operationName}";
            IsUpdating = true;
            StatusChanged?.Invoke();
            _timer = new Timer(async _ => await CheckAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(3));
        }

        public async Task TriggerWorkflowAsync()
        {
            _repo = await _repositoryManager.GetCurrentRepositoryAsync();
            if (_repo == null || _repo.UseApiFileService || string.IsNullOrWhiteSpace(_repo.BuildActionsToWatch))
            {
                return;
            }

            var client = await _gitHubClientProvider.GetClientAsync();
            var workflows = await client.Actions.Workflows.List(_repo.Owner, _repo.RepoName);
            var workflow = workflows.Workflows.FirstOrDefault(w => string.Equals(w.Name, _repo.BuildActionsToWatch, StringComparison.OrdinalIgnoreCase));
            if (workflow != null)
            {
                await client.Actions.Workflows.CreateDispatch(_repo.Owner, _repo.RepoName, workflow.Id, new CreateWorkflowDispatch(_repo.Branch));
                await StartWatchingAsync(false, "Update Preview");
            }
        }

        private async Task CheckAsync()
        {
            if (_repo == null)
            {
                return;
            }

            try
            {
                var client = await _gitHubClientProvider.GetClientAsync();
                string headSha = await _gitHubClientProvider.GetBranchShaAsync(_branch, true);
                var request = new WorkflowRunsRequest
                {
                    HeadSha = headSha
                };

                var response = await client.Actions.Workflows.Runs.List(_repo.Owner, _repo.RepoName, request);
                var run = response.WorkflowRuns
                    .Where(r => r.CreatedAt >= _startTime)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefault(r => _actions == null || _actions.Contains(r.Name, StringComparer.OrdinalIgnoreCase));

                string updateCompleted = $"{_operationName} Completed";
                string updateFailed = $"{_operationName} Failed";
                if (run != null)
                {
                    _latestRun = run;
                    CurrentBuildUrl = run.HtmlUrl;
                    if (run.Status == WorkflowRunStatus.Completed)
                    {
                        IsUpdating = false;
                        if (run.Conclusion == WorkflowRunConclusion.Success)
                        {
                            if (Status != updateCompleted)
                            {
                                Status = updateCompleted;
                                _completedAt ??= DateTimeOffset.UtcNow;
                                BuildCompleted?.Invoke();
                            }
                        }
                        else
                        {
                            if (Status != updateFailed)
                            {
                                Status = updateFailed;
                                _completedAt ??= DateTimeOffset.UtcNow;
                                BuildFailed?.Invoke();
                            }
                        }

                        if (DateTimeOffset.UtcNow - _completedAt > TimeSpan.FromMinutes(1))
                        {
                            Clear();
                            return;
                        }
                    }
                    else
                    {
                        string updatingSite = $"{_operationName} Running";
                        Status = updatingSite;
                        _completedAt = null;
                        IsUpdating = true;
                    }
                }
                else if (_latestRun != null && _latestRun.Status == WorkflowRunStatus.Completed)
                {
                    CurrentBuildUrl = _latestRun.HtmlUrl;
                    IsUpdating = false;
                    if (_latestRun.Conclusion == WorkflowRunConclusion.Success)
                    {
                        if (Status != updateCompleted)
                        {
                            Status = updateCompleted;
                            _completedAt ??= DateTimeOffset.UtcNow;
                            BuildCompleted?.Invoke();
                        }
                    }
                    else
                    {
                        if (Status != updateFailed)
                        {
                            Status = updateFailed;
                            _completedAt ??= DateTimeOffset.UtcNow;
                            BuildFailed?.Invoke();
                        }
                    }

                    _completedAt ??= DateTimeOffset.UtcNow;
                    if (DateTimeOffset.UtcNow - _completedAt > TimeSpan.FromMinutes(1))
                    {
                        Clear();
                        return;
                    }

                }

                StatusChanged?.Invoke();
            }
            catch(Exception ex)
            {
                // ignore errors
                Console.WriteLine(ex);
            }
        }

        private void Clear()
        {
            _timer?.Dispose();
            _timer = null;
            _latestRun = null;
            CurrentBuildUrl = null;
            Status = string.Empty;
            IsUpdating = false;
            _branch = string.Empty;
            _operationName = string.Empty;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
