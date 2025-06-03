using Microsoft.EntityFrameworkCore;
using cms.Modules.User.Models;
using cms.Modules.Tenant.Models;
using cms.Common.Entities;
using cms.Common.Data;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace cms.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { } // DbSets

    public DbSet<User> Users
    {
        get;
        set;
    }

    public DbSet<Credential> Credentials
    {
        get;
        set;
    }

    public DbSet<Tenant> Tenants
    {
        get;
        set;
    }

    public DbSet<TenantRole> TenantRoles
    {
        get;
        set;
    }

    public DbSet<UserTenant> UserTenants
    {
        get;
        set;
    }

    public DbSet<UserTenantRole> UserTenantRoles
    {
        get;
        set;
    }

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
            }
        ); // Configure Credential entity
        modelBuilder.Entity<Credential>(entity =>
            {
                entity.ToTable("Credentials");
                entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Value).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.Metadata).HasMaxLength(2000);

                // Configure relationship with User
                entity.HasOne(c => c.User)
                    .WithMany(u => u.Credentials)
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure optional relationship with Tenant
                entity.HasOne(c => c.Tenant)
                    .WithMany()
                    .HasForeignKey(c => c.TenantId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Create composite index for UserId and Type for efficient lookups
                entity.HasIndex(e => new
                    {
                        e.UserId, e.Type
                    }
                );
            }
        );

        // Configure Tenant entity
        modelBuilder.Entity<Tenant>(entity =>
            {
                entity.ToTable("Tenants");
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);

                // Unique constraint on tenant name (for non-deleted records)
                entity.HasIndex(e => e.Name).IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL");
            }
        );

        // Configure TenantRole entity
        modelBuilder.Entity<TenantRole>(entity =>
            {
                entity.ToTable("TenantRoles");
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Description).HasMaxLength(200);
                entity.Property(e => e.Permissions).HasMaxLength(2000);

                // Configure relationship with Tenant
                entity.HasOne(tr => tr.Tenant)
                    .WithMany(t => t.TenantRoles)
                    .HasForeignKey(tr => tr.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint on tenant-role combination (for non-deleted records)
                entity.HasIndex(e => new
                        {
                            e.TenantId, e.Name
                        }
                    ).IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL");
            }
        );

        // Configure UserTenant entity
        modelBuilder.Entity<UserTenant>(entity =>
            {
                entity.ToTable("UserTenants");

                // Configure relationship with User
                entity.HasOne(ut => ut.User)
                    .WithMany(u => u.UserTenants)
                    .HasForeignKey(ut => ut.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure relationship with Tenant
                entity.HasOne(ut => ut.Tenant)
                    .WithMany(t => t.UserTenants)
                    .HasForeignKey(ut => ut.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint on user-tenant combination (for non-deleted records)
                entity.HasIndex(e => new
                        {
                            e.UserId, e.TenantId
                        }
                    ).IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL");
            }
        );

        // Configure UserTenantRole entity
        modelBuilder.Entity<UserTenantRole>(entity =>
            {
                entity.ToTable("UserTenantRoles");

                // Configure relationship with UserTenant
                entity.HasOne(utr => utr.UserTenant)
                    .WithMany(ut => ut.UserTenantRoles)
                    .HasForeignKey(utr => utr.UserTenantId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure relationship with TenantRole
                entity.HasOne(utr => utr.TenantRole)
                    .WithMany(tr => tr.UserTenantRoles)
                    .HasForeignKey(utr => utr.TenantRoleId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint on user-tenant-role combination (for non-deleted records)
                entity.HasIndex(e => new
                        {
                            e.UserTenantId, e.TenantRoleId
                        }
                    ).IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL");
            }
        );
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

        foreach (EntityEntry entry in entries)
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
