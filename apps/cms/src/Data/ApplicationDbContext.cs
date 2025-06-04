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

    // Resource hierarchy DbSet - Required for proper inheritance configuration
    public DbSet<ResourceBase> Resources { get; set; }

    // Resource and Localization DbSets
    public DbSet<Language> Languages
    {
        get;
        set;
    }

    public DbSet<ResourceMetadata> ResourceMetadata
    {
        get;
        set;
    }    public DbSet<ResourcePermission> ResourcePermissions
    {
        get;
        set;    }    public DbSet<ContentTypePermission> ContentTypePermissions
    {
        get;
        set;
    }

    public DbSet<ResourceRole> ResourceRoles
    {
        get;
        set;
    }

    public DbSet<ResourceLocalization> ResourceLocalizations
    {
        get;
        set;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure ITenantable entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(ITenantable).IsAssignableFrom(t.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType)
                .HasOne(typeof(Modules.Tenant.Models.Tenant).Name)
                .WithMany()
                .HasForeignKey("TenantId")
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        }

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
        ); // Configure Language entity
        modelBuilder.Entity<Language>(entity =>
            {
                entity.ToTable("Languages");
                entity.Property(e => e.Name).IsRequired().HasMaxLength(64);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(64);

                // Unique constraint on language code (for non-deleted records)
                entity.HasIndex(e => e.Code).IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL");

                // Index on name for searching
                entity.HasIndex(e => e.Name);
            }
        );

        // Configure ResourceMetadata entity
        modelBuilder.Entity<ResourceMetadata>(entity =>
            {
                entity.ToTable("ResourceMetadata");
                entity.Property(e => e.ResourceType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.AdditionalData).HasColumnType("jsonb");
                entity.Property(e => e.Tags).HasMaxLength(500);
                entity.Property(e => e.SeoMetadata).HasColumnType("jsonb");

                // Index on resource type for filtering
                entity.HasIndex(e => e.ResourceType);
            }
        );        // Configure ResourcePermission entity
        modelBuilder.Entity<ResourcePermission>(entity =>
            {
                entity.ToTable("ResourcePermissions");
                entity.Property(e => e.ResourceType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Permissions).IsRequired()
                    .HasConversion<int>(); // Store enum as int

                // Configure relationships - EF will create shadow foreign key properties
                entity.HasOne(rp => rp.User)
                    .WithMany()
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(rp => rp.GrantedByUser)
                    .WithMany()
                    .HasForeignKey(rp => rp.GrantedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(rp => rp.ResourceRole)
                    .WithMany(rr => rr.ResourcePermissions)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(rp => rp.ResourceMetadata)
                    .WithMany(rm => rm.ResourcePermissions)
                    .OnDelete(DeleteBehavior.SetNull);

                // Configure polymorphic relationship with ResourceBase
                entity.HasOne(rp => rp.Resource)
                    .WithMany()
                    .HasForeignKey("ResourceId")
                    .OnDelete(DeleteBehavior.Cascade);

                // Indexes for performance
                entity.HasIndex("ResourceId");
                entity.HasIndex(e => e.ResourceType);
                entity.HasIndex("UserId"); // Shadow property
                entity.HasIndex("ResourceRoleId"); // Shadow property
                entity.HasIndex(e => e.Permissions);
            }        );        // Configure ContentTypePermission entity (Both global and tenant-specific content type permissions)
        modelBuilder.Entity<ContentTypePermission>(entity =>
        {
            entity.ToTable("ContentTypePermissions");
            entity.Property(e => e.ContentTypeName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Permissions).IsRequired()
                .HasConversion<int>(); // Store enum as int

            // Configure relationships
            entity.HasOne(ctp => ctp.User)
                .WithMany(u => u.ContentTypePermissions)
                .HasForeignKey(ctp => ctp.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ctp => ctp.AssignedByUser)
                .WithMany()
                .HasForeignKey(ctp => ctp.AssignedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Tenant relationship is configured automatically by ITenantable            // Indexes for performance
            entity.HasIndex(e => new { e.UserId, e.ContentTypeName }).IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL AND \"TenantId\" IS NULL"); // Unique for global permissions
            entity.HasIndex(e => e.ContentTypeName);
            entity.HasIndex(e => e.Permissions);
            entity.HasIndex("TenantId"); // Shadow property from ITenantable
        });

        // Configure ResourceRole entity
        modelBuilder.Entity<ResourceRole>(entity =>
            {
                entity.ToTable("ResourceRoles");
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Description).HasMaxLength(200);
                entity.Property(e => e.DefaultPermission).IsRequired()
                    .HasConversion<int>(); // Store enum as int

                // Unique constraint on role name (for non-deleted records)
                entity.HasIndex(e => e.Name).IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL");
            }
        );        // Configure ResourceLocalization entity
        modelBuilder.Entity<ResourceLocalization>(entity =>
            {
                entity.ToTable("ResourceLocalizations");
                entity.Property(e => e.ResourceType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.FieldName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.Status).IsRequired()
                    .HasConversion<int>(); // Store enum as int

                // Configure relationship with Language
                entity.HasOne(rl => rl.Language)
                    .WithMany(l => l.ResourceLocalizations)
                    .OnDelete(DeleteBehavior.Cascade);

                // Shadow property for ResourceId
                entity.Property<Guid>("ResourceId").IsRequired();

                // Indexes for performance
                entity.HasIndex(
                        new[]
                        {
                            "ResourceId", nameof(ResourceLocalization.ResourceType)
                        }
                    )
                    .HasFilter("\"DeletedAt\" IS NULL");

                // Create a unique index on ResourceId, ResourceType, LanguageId, and FieldName
                entity.HasIndex(
                        new[]
                        {
                            "ResourceId", nameof(ResourceLocalization.ResourceType), "LanguageId", nameof(ResourceLocalization.FieldName)
                        }
                    )
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL");

                entity.HasIndex(e => e.Status);
            }
        );        // Configure ResourceBase hierarchy using Table-per-Type (TPT) strategy
        modelBuilder.Entity<ResourceBase>(entity =>
        {
            entity.ToTable("Resources");
            
            // Configure relationships
            entity.HasOne(r => r.Owner)
                .WithMany()
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Metadata)
                .WithOne()
                .HasForeignKey<ResourceMetadata>("ResourceId")
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for performance
            entity.HasIndex(r => r.Visibility);
            entity.HasIndex("OwnerId"); // Shadow property
        });

        // Configure UserProfile entity with TPT inheritance
        modelBuilder.Entity<cms.Modules.UserProfile.Models.UserProfile>(entity =>
        {
            entity.ToTable("UserProfiles");
            // TPT inheritance: UserProfile gets its own table.
            // Do NOT configure any keys or inherited properties here.
            // All key configuration must be on ResourceBase only.
        });

        // Configure User <-> ResourcePermission (GrantedByUser)
        modelBuilder.Entity<User>()
            .HasMany(u => u.GrantedPermissions)
            .WithOne(rp => rp.GrantedByUser)
            .HasForeignKey(rp => rp.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Explicitly configure ResourcePermission.User <-> User.GrantedResourcePermissions (UserId)
        modelBuilder.Entity<ResourcePermission>()
            .HasOne(rp => rp.User)
            .WithMany(u => u.GrantedResourcePermissions)
            .HasForeignKey("UserId")
            .OnDelete(DeleteBehavior.Cascade);

        // Explicitly configure ResourcePermission.GrantedByUser <-> User.GrantedPermissions (GrantedByUserId)
        modelBuilder.Entity<ResourcePermission>()
            .HasOne(rp => rp.GrantedByUser)
            .WithMany(u => u.GrantedPermissions)
            .HasForeignKey(rp => rp.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
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
