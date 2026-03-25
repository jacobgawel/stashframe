using JSG.API.Stashframe.Core.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace JSG.API.Stashframe.Core.Database;

public class StashframeContext(DbContextOptions<StashframeContext> options) : DbContext(options)
{
    public DbSet<Media> Media { get; set; }
    public DbSet<ShareLink> ShareLinks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Apply all IEntityTypeConfiguration implementations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StashframeContext).Assembly);
    }
}
