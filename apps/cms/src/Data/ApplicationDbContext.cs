using Microsoft.EntityFrameworkCore;
using cms.Modules.User.Models;
using cms.Common.Entities;
using cms.Common.Data;

namespace cms.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    // DbSets
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure base entity properties for all entities
        modelBuilder.ConfigureBaseEntities();
        
        // Configure soft delete global query filters
        modelBuilder.ConfigureSoftDelete();
        
        // Configure User entity (specific configurations only)
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasIndex(e => e.Email).IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL"); // Unique constraint only for non-deleted records
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
        });
    }

    /// <summary>
    /// Automatically update timestamps when saving changes
    /// </summary>
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    /// <summary>
    /// Automatically update timestamps when saving changes asynchronously
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Updates CreatedAt and UpdatedAt timestamps for entities that inherit from BaseEntity
    /// </summary>
    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is IEntity && (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            var entity = (IEntity)entry.Entity;
            
            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt = DateTime.UtcNow;
                entity.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                // Don't update CreatedAt on modifications
                entry.Property(nameof(IEntity.CreatedAt)).IsModified = false;
                entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Include soft-deleted entities in queries
    /// </summary>
    /// <returns>DbContext with soft-deleted entities included</returns>
    public ApplicationDbContext IncludeDeleted()
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        return this;
    }
}
