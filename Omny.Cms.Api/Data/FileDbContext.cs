using Microsoft.EntityFrameworkCore;

namespace Omny.Cms.Api.Data;

public class FileDbContext : DbContext
{
    public FileDbContext(DbContextOptions<FileDbContext> options) : base(options)
    {
    }

    public DbSet<StoredFile> Files => Set<StoredFile>();
}

public class StoredFile
{
    public int Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public byte[] Contents { get; set; } = Array.Empty<byte>();
}
