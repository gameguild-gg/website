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
    public DbSet<ResourceBase> Resources
    {
        get;
        set;
    }

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
    }

    public DbSet<ResourcePermission> ResourcePermissions
    {
        get;
        set;
    }

    public DbSet<ContentTypePermission> ContentTypePermissions
    {
        get;
        set;
    }

    public DbSet<Modules.Reputation.Models.UserReputation> UserReputations
    {
        get;
        set;
    }

    public DbSet<Modules.Reputation.Models.UserTenantReputation> UserTenantReputations
    {
        get;
        set;
    }

    public DbSet<Modules.Reputation.Models.ReputationLevel> ReputationLevels
    {
        get;
        set;
    }

    public DbSet<Modules.Reputation.Models.ReputationAction> ReputationActions
    {
        get;
        set;
    }

    public DbSet<Modules.Reputation.Models.UserReputationHistory> UserReputationHistory
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
        ); // Configure ResourcePermission entity
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
                entity.HasIndex(e => e.Permissions);
            }
        );

        // Configure ContentTypePermission entity (Both global and tenant-specific content type permissions)
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
                entity.HasIndex(e => new
                        {
                            e.UserId, e.ContentTypeName
                        }
                    ).IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL AND \"TenantId\" IS NULL"); // Unique for global permissions
                entity.HasIndex(e => e.ContentTypeName);
                entity.HasIndex(e => e.Permissions);
                entity.HasIndex("TenantId"); // Shadow property from ITenantable
            }
        ); // Configure ResourceLocalization entity
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
        ); // Configure ResourceBase hierarchy using Table-per-Type (TPT) strategy
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
            }
        );

        // Configure UserProfile entity with TPT inheritance
        modelBuilder.Entity<cms.Modules.UserProfile.Models.UserProfile>(entity =>
            {
                entity.ToTable("UserProfiles");
                // TPT inheritance: UserProfile gets its own table.
                // Do NOT configure any keys or inherited properties here.
                // All key configuration must be on ResourceBase only.
            }
        );

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
            .OnDelete(DeleteBehavior.Cascade); // Explicitly configure ResourcePermission.GrantedByUser <-> User.GrantedPermissions (GrantedByUserId)
        modelBuilder.Entity<ResourcePermission>()
            .HasOne(rp => rp.GrantedByUser)
            .WithMany(u => u.GrantedPermissions)
            .HasForeignKey(rp => rp.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure Reputation System entities
        ConfigureReputationSystem(modelBuilder);
    }

    /// <summary>
    /// Configure the reputation system entities and their relationships
    /// </summary>
    private void ConfigureReputationSystem(ModelBuilder modelBuilder)
    {
        // Configure UserReputation entity (global reputation only)
        modelBuilder.Entity<Modules.Reputation.Models.UserReputation>(entity =>
            {
                entity.ToTable("UserReputations");

                // Configure relationship with User
                entity.HasOne(ur => ur.User)
                    .WithMany()
                    .HasForeignKey(ur => ur.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure relationship with CurrentLevel
                entity.HasOne(ur => ur.CurrentLevel)
                    .WithMany()
                    .HasForeignKey(ur => ur.CurrentLevelId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Indexes for performance
                entity.HasIndex(ur => ur.UserId).IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL"); // One global reputation per user
                entity.HasIndex(ur => ur.Score);
                entity.HasIndex(ur => ur.CurrentLevelId);
            }
        );

        // Configure UserTenantReputation entity with TPT inheritance
        modelBuilder.Entity<Modules.Reputation.Models.UserTenantReputation>(entity =>
            {
                entity.ToTable("UserTenantReputations");

                // Configure relationship with UserTenant
                entity.HasOne(utr => utr.UserTenant)
                    .WithMany()
                    .HasForeignKey(utr => utr.UserTenantId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure relationship with CurrentLevel
                entity.HasOne(utr => utr.CurrentLevel)
                    .WithMany()
                    .HasForeignKey(utr => utr.CurrentLevelId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Indexes for performance
                entity.HasIndex(utr => utr.UserTenantId).IsUnique(); // One reputation per UserTenant
                entity.HasIndex(utr => utr.Score);
                entity.HasIndex(utr => utr.CurrentLevelId);
            }
        );

        // Configure ReputationLevel entity with TPT inheritance
        modelBuilder.Entity<Modules.Reputation.Models.ReputationLevel>(entity =>
            {
                entity.ToTable("ReputationLevels");

                entity.Property(rl => rl.Name).IsRequired().HasMaxLength(100);
                entity.Property(rl => rl.DisplayName).IsRequired().HasMaxLength(200);
                entity.Property(rl => rl.Color).HasMaxLength(50);
                entity.Property(rl => rl.Icon).HasMaxLength(100);

                // Unique constraint on name per tenant using string-based property names
                entity.HasIndex("Name", "TenantId").IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL");

                // Indexes for performance
                entity.HasIndex(rl => rl.MinimumScore);
                entity.HasIndex(rl => rl.SortOrder);
            }
        );


        // Configure ReputationAction entity with TPT inheritance
        modelBuilder.Entity<Modules.Reputation.Models.ReputationAction>(entity =>
            {
                entity.ToTable("ReputationActions");

                entity.Property(ra => ra.ActionType).IsRequired().HasMaxLength(150);
                entity.Property(ra => ra.DisplayName).IsRequired().HasMaxLength(200);

                // Configure relationship with RequiredLevel
                entity.HasOne(ra => ra.RequiredLevel)
                    .WithMany()
                    .HasForeignKey(ra => ra.RequiredLevelId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Unique constraint on action type per tenant using string-based property names
                entity.HasIndex("ActionType", "TenantId").IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL");

                // Indexes for performance
                entity.HasIndex(ra => ra.ActionType);
                entity.HasIndex(ra => ra.Points);
                entity.HasIndex(ra => ra.IsActive);
            }
        ); // Configure UserReputationHistory entity with polymorphic relationships
        modelBuilder.Entity<Modules.Reputation.Models.UserReputationHistory>(entity =>
            {
                entity.ToTable(
                    "UserReputationHistory",
                    t =>
                        t.HasCheckConstraint(
                            "CK_UserReputationHistory_UserOrUserTenant",
                            "(\"UserId\" IS NOT NULL AND \"UserTenantId\" IS NULL) OR (\"UserId\" IS NULL AND \"UserTenantId\" IS NOT NULL)"
                        )
                );

                entity.Property(urh => urh.Reason).HasMaxLength(500);

                // Configure optional relationship with User (for direct user reputation)
                entity.HasOne(urh => urh.User)
                    .WithMany()
                    .HasForeignKey(urh => urh.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Configure optional relationship with UserTenant (for tenant-specific reputation)
                entity.HasOne(urh => urh.UserTenant)
                    .WithMany()
                    .HasForeignKey(urh => urh.UserTenantId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Configure relationship with ReputationAction
                entity.HasOne(urh => urh.ReputationAction)
                    .WithMany(ra => ra.ReputationHistory)
                    .HasForeignKey(urh => urh.ReputationActionId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Configure relationship with TriggeredByUser
                entity.HasOne(urh => urh.TriggeredByUser)
                    .WithMany()
                    .HasForeignKey(urh => urh.TriggeredByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Configure relationship with PreviousLevel
                entity.HasOne(urh => urh.PreviousLevel)
                    .WithMany()
                    .HasForeignKey(urh => urh.PreviousLevelId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Configure relationship with NewLevel
                entity.HasOne(urh => urh.NewLevel)
                    .WithMany()
                    .HasForeignKey(urh => urh.NewLevelId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Configure polymorphic relationship with RelatedResource
                // This allows reputation to be associated with any ResourceBase entity
                entity.HasOne(urh => urh.RelatedResource)
                    .WithMany()
                    .HasForeignKey("RelatedResourceId") // Shadow property created by EF
                    .OnDelete(DeleteBehavior.SetNull);

                // Indexes for performance and querying
                entity.HasIndex(urh => urh.UserId);
                entity.HasIndex(urh => urh.UserTenantId);
                entity.HasIndex(urh => urh.OccurredAt);
                entity.HasIndex(urh => urh.PointsChange);
                entity.HasIndex("RelatedResourceId"); // Shadow property
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
