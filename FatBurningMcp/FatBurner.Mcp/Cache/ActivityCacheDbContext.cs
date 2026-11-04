using Microsoft.EntityFrameworkCore;

namespace FatBurner.Mcp.Cache;

public class ActivityCacheDbContext(DbContextOptions<ActivityCacheDbContext> options) : DbContext(options)
{
    public DbSet<ActivityEntity> Activities => Set<ActivityEntity>();
    public DbSet<ActivityStreamsEntity> ActivityStreams => Set<ActivityStreamsEntity>();

    /// <summary>
    /// EnsureCreated does nothing when the database already has tables, so caches
    /// created before ActivityStreams existed need that table added explicitly.
    /// </summary>
    public void EnsureSchema()
    {
        Database.EnsureCreated();
        Database.ExecuteSqlRaw(
            """
            CREATE TABLE IF NOT EXISTS "ActivityStreams" (
                "ActivityId" TEXT NOT NULL CONSTRAINT "PK_ActivityStreams" PRIMARY KEY,
                "SerializedStreams" TEXT NOT NULL
            )
            """);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActivityEntity>().HasKey(a => a.ActivityId);
        modelBuilder.Entity<ActivityStreamsEntity>().HasKey(s => s.ActivityId);
    }
}
