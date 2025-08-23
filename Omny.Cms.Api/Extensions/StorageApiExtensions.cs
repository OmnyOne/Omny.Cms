using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Omny.Api;
using Omny.Cms.Api.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Omny.Cms.Api.Extensions;

public static class StorageApiExtensions
{
    public static void AddStorageApis(this RouteGroupBuilder apiGroup)
    {
        apiGroup.MapGet("items", async (IOptions<RepositoryOptions> repositoryOptions) =>
        {
            var repository = repositoryOptions.Value.Available.FirstOrDefault(r => r.UseApiFileService);
            if (repository == null)
            {
                return Results.NotFound();
            }

            if (!string.IsNullOrEmpty(repository.LocalPath))
            {
                string rootPath = repository.LocalPath!;
                var items = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories)
                    .Select(f => new Omny.Api.Models.CacheableTreeItem(
                        Path.GetRelativePath(rootPath, f).Replace("\\", "/"),
                        Convert.ToHexString(SHA1.HashData(File.ReadAllBytes(f)))));
                return Results.Ok(items);
            }
            else
            {
                var options = new DbContextOptionsBuilder<FileDbContext>()
                    .UseNpgsql(repository.DatabaseConnectionString)
                    .Options;
                await using var db = new FileDbContext(options);
                await db.Database.EnsureCreatedAsync();
                var items = await db.Files
                    .Select(f => new Omny.Api.Models.CacheableTreeItem(
                        f.Path,
                        Convert.ToHexString(SHA1.HashData(f.Contents))))
                    .ToListAsync();
                return Results.Ok(items);
            }
        });

        apiGroup.MapGet("items/{*path}", async (string path, IOptions<RepositoryOptions> repositoryOptions) =>
        {
            var repository = repositoryOptions.Value.Available.FirstOrDefault(r => r.UseApiFileService);
            if (repository == null)
            {
                return Results.NotFound();
            }

            if (!string.IsNullOrEmpty(repository.LocalPath))
            {
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
                    contents = await File.ReadAllTextAsync(full);
                }

                return Results.Ok(new Omny.Api.Models.RemoteFileContents(repository.Branch, path, contents));
            }
            else
            {
                var options = new DbContextOptionsBuilder<FileDbContext>()
                    .UseNpgsql(repository.DatabaseConnectionString)
                    .Options;
                await using var db = new FileDbContext(options);
                await db.Database.EnsureCreatedAsync();
                var file = await db.Files.FirstOrDefaultAsync(f => f.Path == path);
                if (file == null)
                {
                    return Results.NotFound();
                }

                string ext = Path.GetExtension(path).ToLowerInvariant();
                string[] imageExts = [".png", ".jpg", ".jpeg", ".gif", ".webp"];
                string contents;
                if (imageExts.Contains(ext))
                {
                    contents = Convert.ToBase64String(file.Contents);
                }
                else
                {
                    contents = System.Text.Encoding.UTF8.GetString(file.Contents);
                }

                return Results.Ok(new Omny.Api.Models.RemoteFileContents(repository.Branch, path, contents));
            }
        });

        apiGroup.MapPut("items", async (Dictionary<string, string> files, IOptions<RepositoryOptions> repositoryOptions) =>
        {
            var repository = repositoryOptions.Value.Available.FirstOrDefault(r => r.UseApiFileService);
            if (repository == null)
            {
                return Results.NotFound();
            }

            if (!string.IsNullOrEmpty(repository.LocalPath))
            {
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
            }
            else
            {
                var options = new DbContextOptionsBuilder<FileDbContext>()
                    .UseNpgsql(repository.DatabaseConnectionString)
                    .Options;
                await using var db = new FileDbContext(options);
                await db.Database.EnsureCreatedAsync();
                foreach (var entry in files)
                {
                    var existing = await db.Files.FirstOrDefaultAsync(f => f.Path == entry.Key);
                    var data = System.Text.Encoding.UTF8.GetBytes(entry.Value);
                    if (existing == null)
                    {
                        db.Files.Add(new StoredFile { Path = entry.Key, Contents = data });
                    }
                    else
                    {
                        existing.Contents = data;
                    }
                }

                await db.SaveChangesAsync();
                return Results.Ok();
            }
        });

        apiGroup.MapPut("items/binary", async (Dictionary<string, string> files, IOptions<RepositoryOptions> repositoryOptions) =>
        {
            var repository = repositoryOptions.Value.Available.FirstOrDefault(r => r.UseApiFileService);
            if (repository == null)
            {
                return Results.NotFound();
            }

            if (!string.IsNullOrEmpty(repository.LocalPath))
            {
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
            }
            else
            {
                var options = new DbContextOptionsBuilder<FileDbContext>()
                    .UseNpgsql(repository.DatabaseConnectionString)
                    .Options;
                await using var db = new FileDbContext(options);
                await db.Database.EnsureCreatedAsync();
                foreach (var entry in files)
                {
                    var data = Convert.FromBase64String(entry.Value);
                    var existing = await db.Files.FirstOrDefaultAsync(f => f.Path == entry.Key);
                    if (existing == null)
                    {
                        db.Files.Add(new StoredFile { Path = entry.Key, Contents = data });
                    }
                    else
                    {
                        existing.Contents = data;
                    }
                }

                await db.SaveChangesAsync();
                return Results.Ok();
            }
        });

        apiGroup.MapPost("folders/rename", (IOptions<RepositoryOptions> repositoryOptions, string oldFolderPath, string newFolderPath) =>
        {
            var repository = repositoryOptions.Value.Available.FirstOrDefault(r => r.UseApiFileService);
            if (repository == null)
            {
                return Results.NotFound();
            }

            if (!string.IsNullOrEmpty(repository.LocalPath))
            {
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
            }
            else
            {
                var options = new DbContextOptionsBuilder<FileDbContext>()
                    .UseNpgsql(repository.DatabaseConnectionString)
                    .Options;
                using var db = new FileDbContext(options);
                db.Database.EnsureCreated();
                var items = db.Files.Where(f => f.Path.StartsWith(oldFolderPath));
                foreach (var item in items)
                {
                    item.Path = newFolderPath + item.Path.Substring(oldFolderPath.Length);
                }

                db.SaveChanges();
                return Results.Ok();
            }
        });

        apiGroup.MapDelete("folders/{*folderPath}", (IOptions<RepositoryOptions> repositoryOptions, string folderPath) =>
        {
            var repository = repositoryOptions.Value.Available.FirstOrDefault(r => r.UseApiFileService);
            if (repository == null)
            {
                return Results.NotFound();
            }

            if (!string.IsNullOrEmpty(repository.LocalPath))
            {
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
            }
            else
            {
                var options = new DbContextOptionsBuilder<FileDbContext>()
                    .UseNpgsql(repository.DatabaseConnectionString)
                    .Options;
                using var db = new FileDbContext(options);
                db.Database.EnsureCreated();
                var items = db.Files.Where(f => f.Path.StartsWith(folderPath));
                db.Files.RemoveRange(items);
                db.SaveChanges();
                return Results.Ok();
            }
        });

        apiGroup.MapDelete("items", async ([FromBody] string[] filePaths, IOptions<RepositoryOptions> repositoryOptions) =>
        {
            var repository = repositoryOptions.Value.Available.FirstOrDefault(r => r.UseApiFileService);
            if (repository == null)
            {
                return Results.NotFound();
            }

            if (!string.IsNullOrEmpty(repository.LocalPath))
            {
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
            }
            else
            {
                var options = new DbContextOptionsBuilder<FileDbContext>()
                    .UseNpgsql(repository.DatabaseConnectionString)
                    .Options;
                await using var db = new FileDbContext(options);
                await db.Database.EnsureCreatedAsync();
                var items = await db.Files.Where(f => filePaths.Contains(f.Path)).ToListAsync();
                db.Files.RemoveRange(items);
                await db.SaveChangesAsync();
                return Results.Ok();
            }
        });
    }
}
