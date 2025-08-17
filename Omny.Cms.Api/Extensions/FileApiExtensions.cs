using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Omny.Api;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Omny.Cms.Api.Extensions;

public static class FileApiExtensions
{
    public static void AddFileApis(this RouteGroupBuilder apiGroup)
    {
        apiGroup.MapGet("files", (
            IOptions<RepositoryOptions> repositoryOptions) =>
        {
            var repository = repositoryOptions.Value.Available.FirstOrDefault(r => r.UseApiFileService);
            if (repository == null)
            {
                return Results.NotFound();
            }

            string rootPath = repository.LocalPath!;
            var items = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories)
                .Select(f => new Omny.Api.Models.CacheableTreeItem(
                    Path.GetRelativePath(rootPath, f).Replace("\\", "/"),
                    Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(File.ReadAllBytes(f)))));
            return Results.Ok(items);
        });

        apiGroup.MapGet("file", (string path,
            IOptions<RepositoryOptions> repositoryOptions) =>
        {
            var repository = repositoryOptions.Value.Available.FirstOrDefault(r => r.UseApiFileService);
            if (repository == null)
            {
                return Results.NotFound();
            }

            string rootPath = repository.LocalPath!;
            string full = Path.GetFullPath(Path.Combine(rootPath, path));
            if (!full.StartsWith(Path.GetFullPath(rootPath)))
            {
                return Results.BadRequest();
            }

            if (!File.Exists(full))
            {
                return Results.NotFound();
            }

            string ext = Path.GetExtension(full).ToLowerInvariant();
            string[] imageExts = [".png", ".jpg", ".jpeg", ".gif", ".webp"];
            string contents;
            if (imageExts.Contains(ext))
            {
                contents = Convert.ToBase64String(File.ReadAllBytes(full));
            }
            else
            {
                contents = File.ReadAllText(full);
            }

            return Results.Ok(new Omny.Api.Models.RemoteFileContents(repository.Branch, path, contents));
        });

        apiGroup.MapPost("write-files", async (Dictionary<string, string> files,
            IOptions<RepositoryOptions> repositoryOptions) =>
        {
            var repository = repositoryOptions.Value.Available.FirstOrDefault(r => r.UseApiFileService);
            if (repository == null)
            {
                return Results.NotFound();
            }

            string rootPath = repository.LocalPath!;
            foreach (var fileEntry in files)
            {
                string full = Path.GetFullPath(Path.Combine(rootPath, fileEntry.Key));
                if (!full.StartsWith(Path.GetFullPath(rootPath)))
                {
                    return Results.BadRequest();
                }

                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                await File.WriteAllTextAsync(full, fileEntry.Value);
            }

            return Results.Ok();
        });

        apiGroup.MapPost("write-binary-files", async (Dictionary<string, string> files,
            IOptions<RepositoryOptions> repositoryOptions) =>
        {
            var repository = repositoryOptions.Value.Available.FirstOrDefault(r => r.UseApiFileService);
            if (repository == null)
            {
                return Results.NotFound();
            }

            string rootPath = repository.LocalPath!;
            foreach (var fileEntry in files)
            {
                string full = Path.GetFullPath(Path.Combine(rootPath, fileEntry.Key));
                if (!full.StartsWith(Path.GetFullPath(rootPath)))
                {
                    return Results.BadRequest();
                }

                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                byte[] data = Convert.FromBase64String(fileEntry.Value);
                await File.WriteAllBytesAsync(full, data);
            }

            return Results.Ok();
        });

        apiGroup.MapPost("rename-folder", (
            IOptions<RepositoryOptions> repositoryOptions,
            string oldFolderPath, string newFolderPath) =>
        {
            var repository = repositoryOptions.Value.Available.FirstOrDefault(r => r.UseApiFileService);
            if (repository == null)
            {
                return Results.NotFound();
            }

            string rootPath = repository.LocalPath!;
            string oldFull = Path.GetFullPath(Path.Combine(rootPath, oldFolderPath));
            string newFull = Path.GetFullPath(Path.Combine(rootPath, newFolderPath));
            if (!oldFull.StartsWith(Path.GetFullPath(rootPath)) || !newFull.StartsWith(Path.GetFullPath(rootPath)))
            {
                return Results.BadRequest();
            }

            if (Directory.Exists(oldFull))
            {
                Directory.Move(oldFull, newFull);
            }

            return Results.Ok();
        });

        apiGroup.MapDelete("folder", (
            IOptions<RepositoryOptions> repositoryOptions,
            [FromQuery] string folderPath) =>
        {
            var repository = repositoryOptions.Value.Available.FirstOrDefault(r => r.UseApiFileService);
            if (repository == null)
            {
                return Results.NotFound();
            }

            string rootPath = repository.LocalPath!;
            string full = Path.GetFullPath(Path.Combine(rootPath, folderPath));
            if (!full.StartsWith(Path.GetFullPath(rootPath)))
            {
                return Results.BadRequest();
            }

            if (Directory.Exists(full))
            {
                Directory.Delete(full, true);
            }

            return Results.Ok();
        });

        apiGroup.MapPost("delete-files", (
            IOptions<RepositoryOptions> repositoryOptions,
            string[] filePaths) =>
        {
            var repository = repositoryOptions.Value.Available.FirstOrDefault(r => r.UseApiFileService);
            if (repository == null)
            {
                return Results.NotFound();
            }

            string rootPath = repository.LocalPath!;
            foreach (var filePath in filePaths)
            {
                string full = Path.GetFullPath(Path.Combine(rootPath, filePath));
                if (!full.StartsWith(Path.GetFullPath(rootPath)))
                {
                    return Results.BadRequest();
                }
                if (File.Exists(full))
                {
                    File.Delete(full);
                }
            }

            return Results.Ok();
        });
    }
}

