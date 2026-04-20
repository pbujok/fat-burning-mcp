using Microsoft.EntityFrameworkCore;

namespace FatBurner.Mcp.Cache;

public class ActivityCacheDbContext(DbContextOptions<ActivityCacheDbContext> options) : DbContext(options)
{
    public DbSet<ActivityEntity> Activities => Set<ActivityEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActivityEntity>().HasKey(a => a.ActivityId);
    }
}
