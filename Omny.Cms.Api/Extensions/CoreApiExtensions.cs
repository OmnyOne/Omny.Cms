using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Omny.Api;
using Omny.Api.Auth;
using System.Linq;

namespace Omny.Cms.Api.Extensions;

public static class CoreApiExtensions
{
    public static void AddCoreApis(this RouteGroupBuilder apiGroup)
    {
        apiGroup.MapGet("ping", [AllowAnonymous]() => TypedResults.Ok("pong"));

        apiGroup.MapGet("debug", async (
            IUserInfoProvider userInfoProvider,
            IUserAdminChecker adminChecker,
            IOptions<RepositoryOptions> repositoryOptions,
            IConfiguration configuration) =>
        {
            var userInfo = await userInfoProvider.GetCurrentUserAsync();
            var isAdmin = await adminChecker.IsUserAdminAsync();
            return TypedResults.Ok(new
            {
                Email = userInfo.Email,
                IsAdmin = isAdmin,
                ReposCount = repositoryOptions.Value.Available.Count,
                Host = configuration.GetValue<string>("OverrideHost")
            });
        });

        apiGroup.MapGet("github/token", (IConfiguration configuration) =>
            TypedResults.Ok(new { token = configuration["Github:Token"] }));

        apiGroup.MapGet("repositories", async (
            IOptions<RepositoryOptions> repositoryOptions,
            IUserAdminChecker adminChecker,
            IUserInfoProvider userInfoProvider,
            IConfiguration configuration) =>
        {
            bool isAdmin = await adminChecker.IsUserAdminAsync();
            var userInfo = await userInfoProvider.GetCurrentUserAsync();
            var repositories = repositoryOptions.Value.Available
                .Where(r => r.UserEmails.Contains(userInfo.Email!))
                .Select(repository => new
                {
                    Owner = repository.Owner,
                    Name = repository.Name,
                    RepoName = repository.RepoName,
                    Branch = repository.Branch,
                    ShowAdvancedOptions = repository.ShowAdvancedOptions && isAdmin,
                    Token = !string.IsNullOrEmpty(repository.Token) ? repository.Token : configuration["Github:Token"] ?? string.Empty,
                    BuildActionsToWatch = repository.BuildActionsToWatch,
                    PreviewUrl = repository.PreviewUrl,
                    HasWorkflowDispatch = repository.HasWorkflowDispatch,
                    ImageStorage = repository.ImageStorage,
                    UseLeftItemSelector = repository.UseLeftItemSelector,
                    UseApiFileService = repository.UseApiFileService
                }).ToArray();

            return TypedResults.Ok(repositories);
        });
    }
}
