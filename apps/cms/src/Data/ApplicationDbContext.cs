using Microsoft.EntityFrameworkCore;
using cms.Modules.User.Models;
using cms.Modules.Tenant.Models;
using cms.Common.Entities;
using cms.Common.Data;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

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

    public DbSet<cms.Modules.Auth.Models.RefreshToken> RefreshTokens
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

    // Content hierarchy DbSets - Required for TPC inheritance configuration
    public DbSet<ContentLicense> ContentLicenses
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

    public DbSet<Modules.Reputation.Models.ReputationTier> ReputationTiers
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

    // Product and Program Management DbSets
    public DbSet<Modules.Product.Models.Product> Products
    {
        get;
        set;
    }

    public DbSet<Modules.Product.Models.ProductPricing> ProductPricings
    {
        get;
        set;
    }

    public DbSet<Modules.Product.Models.ProductProgram> ProductPrograms
    {
        get;
        set;
    }

    public DbSet<Modules.Product.Models.ProductSubscriptionPlan> ProductSubscriptionPlans
    {
        get;
        set;
    }

    public DbSet<Modules.Product.Models.UserProduct> UserProducts
    {
        get;
        set;
    }

    public DbSet<Modules.Product.Models.PromoCode> PromoCodes
    {
        get;
        set;
    }

    public DbSet<Modules.Product.Models.PromoCodeUse> PromoCodeUses
    {
        get;
        set;
    }

    // Program Management DbSets
    public DbSet<Modules.Program.Models.Program> Programs
    {
        get;
        set;
    }

    public DbSet<Modules.Program.Models.ProgramContent> ProgramContents
    {
        get;
        set;
    }

    public DbSet<Modules.Program.Models.ProgramUser> ProgramUsers
    {
        get;
        set;
    }

    public DbSet<Modules.Program.Models.ProgramUserRole> ProgramUserRoles
    {
        get;
        set;
    }

    public DbSet<Modules.Program.Models.ContentInteraction> ContentInteractions
    {
        get;
        set;
    }

    public DbSet<Modules.Program.Models.ActivityGrade> ActivityGrades
    {
        get;
        set;
    }

    // Certificate Management DbSets
    public DbSet<Modules.Certificate.Models.Certificate> Certificates
    {
        get;
        set;
    }

    public DbSet<Modules.Certificate.Models.UserCertificate> UserCertificates
    {
        get;
        set;
    }

    public DbSet<Modules.Certificate.Models.CertificateTag> CertificateTags
    {
        get;
        set;
    }

    public DbSet<Modules.Certificate.Models.CertificateBlockchainAnchor> CertificateBlockchainAnchors
    {
        get;
        set;
    }

    // Tag Management DbSets
    public DbSet<Modules.Tag.Models.Tag> Tags
    {
        get;
        set;
    }

    public DbSet<Modules.Tag.Models.TagRelationship> TagRelationships
    {
        get;
        set;
    }

    public DbSet<Modules.Tag.Models.TagProficiency> TagProficiencies
    {
        get;
        set;
    }

    // Subscription Management DbSets
    public DbSet<Modules.Subscription.Models.UserSubscription> UserSubscriptions
    {
        get;
        set;
    }

    // Payment Management DbSets
    public DbSet<Modules.Payment.Models.UserFinancialMethod> UserFinancialMethods
    {
        get;
        set;
    }

    public DbSet<Modules.Payment.Models.FinancialTransaction> FinancialTransactions
    {
        get;
        set;
    }

    // KYC Management DbSets
    public DbSet<Modules.Kyc.Models.UserKycVerification> UserKycVerifications
    {
        get;
        set;
    }

    // Feedback Management DbSets
    public DbSet<Modules.Feedback.Models.ProgramFeedbackSubmission> ProgramFeedbackSubmissions
    {
        get;
        set;
    }

    public DbSet<Modules.Feedback.Models.ProgramRating> ProgramRatings
    {
        get;
        set;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure ITenantable entities
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes()
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
            }        ); // Configure ResourceBase hierarchy using Table-per-Concrete (TPC) strategy
        modelBuilder.Entity<ResourceBase>(entity =>
            {
                // TPC inheritance: Configure the inheritance strategy
                entity.UseTpcMappingStrategy();
                
                // Configure common properties and relationships for all concrete types
                entity.HasOne(r => r.Owner)
                    .WithMany()
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.Metadata)
                    .WithOne()
                    .HasForeignKey<ResourceMetadata>("ResourceId")
                    .OnDelete(DeleteBehavior.Cascade);

                // Common indexes for performance - will be applied to all concrete tables
                entity.HasIndex(r => r.Visibility);
                entity.HasIndex("OwnerId"); // Shadow property
            }
        );

        // Configure UserProfile entity with TPC inheritance
        modelBuilder.Entity<cms.Modules.UserProfile.Models.UserProfile>(entity =>
            {
                entity.ToTable("UserProfiles");
                // TPC inheritance: Each concrete type gets its own complete table
                // including all inherited properties from ResourceBase
            }
        );

        // Configure ContentLicense entity with TPC inheritance
        modelBuilder.Entity<ContentLicense>(entity =>
            {
                entity.ToTable("ContentLicenses");
                // TPC inheritance: Each concrete type gets its own complete table
                // including all inherited properties from ResourceBase
                
                // Configure ContentLicense-specific properties
                entity.Property(cl => cl.Url).HasMaxLength(500);
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
            .OnDelete(DeleteBehavior.Restrict); // Configure Reputation System entities
        ConfigureReputationSystem(modelBuilder);

        // Configure Program DBML entities
        ConfigureProgramDbmlEntities(modelBuilder);
    }    /// <summary>
    /// Configure the reputation system entities and their relationships
    /// </summary>
    private void ConfigureReputationSystem(ModelBuilder modelBuilder)
    {
        // Configure UserReputation entity (global reputation only)
        modelBuilder.Entity<Modules.Reputation.Models.UserReputation>(entity =>
            {
                entity.ToTable("UserReputations");
                // TPC inheritance: UserReputation gets its own complete table
                // including all inherited properties from ResourceBase

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
        ); // Configure UserTenantReputation entity with TPC inheritance
        modelBuilder.Entity<Modules.Reputation.Models.UserTenantReputation>(entity =>
            {
                entity.ToTable("UserTenantReputations");
                // TPC inheritance: UserTenantReputation gets its own complete table
                // including all inherited properties from ResourceBase

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
        ); // Configure ReputationTier entity with TPC inheritance
        modelBuilder.Entity<Modules.Reputation.Models.ReputationTier>(entity =>
            {
                entity.ToTable("ReputationLevels");
                // TPC inheritance: ReputationTier gets its own complete table
                // including all inherited properties from ResourceBase

                entity.Property(rl => rl.Name).IsRequired().HasMaxLength(100);
                entity.Property(rl => rl.DisplayName).IsRequired().HasMaxLength(200);
                entity.Property(rl => rl.Color).HasMaxLength(50);
                entity.Property(rl => rl.Icon).HasMaxLength(100);                // In TPC inheritance, we can create composite indexes across all columns
                // since all properties are in the same table  
                // Use the TenantId foreign key directly in composite index (EF creates this automatically for ITenantable)
                entity.HasIndex("Name", "TenantId")
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL");

                // Indexes for performance
                entity.HasIndex(rl => rl.MinimumScore);
                entity.HasIndex(rl => rl.SortOrder);
            }
        ); // Configure ReputationAction entity
        modelBuilder.Entity<Modules.Reputation.Models.ReputationAction>(entity =>
            {
                entity.ToTable("ReputationActions");
                // TPC inheritance: ReputationAction gets its own complete table
                // including all inherited properties from ResourceBase

                entity.Property(ra => ra.ActionType).IsRequired().HasMaxLength(150);
                entity.Property(ra => ra.DisplayName).IsRequired().HasMaxLength(200);
                entity.Property(ra => ra.Description).HasMaxLength(2000);

                // Configure relationship with RequiredLevel
                entity.HasOne(ra => ra.RequiredLevel)
                    .WithMany()
                    .HasForeignKey(ra => ra.RequiredLevelId)
                    .OnDelete(DeleteBehavior.SetNull);                // In TPC inheritance, we can create composite indexes across all columns
                // since all properties are in the same table
                // Use the TenantId foreign key directly in composite index (EF creates this automatically for ITenantable)
                entity.HasIndex("ActionType", "TenantId")
                    .IsUnique()
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
                // TPC inheritance: UserReputationHistory gets its own complete table
                // including all inherited properties from ResourceBase

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
    /// Also handles Version incrementing for optimistic concurrency control
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
                entity.Version = 1;
            }
            else if (entry.State == EntityState.Modified)
            {
                // Don't update CreatedAt on modifications
                entry.Property(nameof(IEntity.CreatedAt)).IsModified = false;
                entity.UpdatedAt = DateTime.UtcNow;

                entity.Version++;
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

    /// <summary>
    /// Configure all Program DBML entities and their relationships
    /// </summary>
    private void ConfigureProgramDbmlEntities(ModelBuilder modelBuilder)
    {
        // Configure Product Module
        ConfigureProductModule(modelBuilder);

        // Configure Program Module
        ConfigureProgramModule(modelBuilder);

        // Configure Certificate Module
        ConfigureCertificateModule(modelBuilder);

        // Configure Tag Module
        ConfigureTagModule(modelBuilder);

        // Configure Subscription Module
        ConfigureSubscriptionModule(modelBuilder);

        // Configure Payment Module
        ConfigurePaymentModule(modelBuilder);

        // Configure KYC Module
        ConfigureKycModule(modelBuilder);

        // Configure Feedback Module
        ConfigureFeedbackModule(modelBuilder);
    }

    /// <summary>
    /// Configure Product Module entities
    /// </summary>
    private void ConfigureProductModule(ModelBuilder modelBuilder)
    {        // Configure Product entity
        modelBuilder.Entity<Modules.Product.Models.Product>(entity =>
            {
                entity.ToTable("Products");
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(2000);
                entity.Property(e => e.ShortDescription).HasMaxLength(500);
                entity.Property(e => e.ImageUrl).HasMaxLength(500);
                // Note: Metadata property configuration removed as it's inherited from ResourceBase
                entity.Property(e => e.Status).IsRequired().HasConversion<int>();
                entity.Property(e => e.Visibility).IsRequired().HasConversion<int>();

                // Configure relationship with Creator
                entity.HasOne(p => p.Creator)
                    .WithMany()
                    .HasForeignKey(p => p.CreatorId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Indexes
                entity.HasIndex(p => p.Name);
                entity.HasIndex(p => p.Status);
                entity.HasIndex(p => p.Visibility);
                entity.HasIndex(p => p.CreatorId);
            }
        );        // Configure ProductPricing entity
        modelBuilder.Entity<Modules.Product.Models.ProductPricing>(entity =>
            {
                entity.ToTable("ProductPricings");
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
                entity.Property(e => e.BasePrice).IsRequired().HasColumnType("decimal(10,2)");
                entity.Property(e => e.SalePrice).HasColumnType("decimal(10,2)");                // Configure relationship with Product
                entity.HasOne(pp => pp.Product)
                    .WithMany(p => p.ProductPricings)
                    .HasForeignKey(pp => pp.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indexes
                entity.HasIndex(pp => pp.ProductId);
                entity.HasIndex(pp => pp.Currency);
                entity.HasIndex(pp => pp.SaleStartDate);
                entity.HasIndex(pp => pp.SaleEndDate);
            }
        );

        // Configure ProductProgram entity
        modelBuilder.Entity<Modules.Product.Models.ProductProgram>(entity =>
            {
                entity.ToTable("ProductPrograms");

                // Configure relationship with Product
                entity.HasOne(pp => pp.Product)
                    .WithMany(p => p.ProductPrograms)
                    .HasForeignKey(pp => pp.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure relationship with Program
                entity.HasOne(pp => pp.Program)
                    .WithMany()
                    .HasForeignKey(pp => pp.ProgramId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint
                entity.HasIndex(pp => new
                        {
                            pp.ProductId, pp.ProgramId
                        }
                    )
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL");
            }
        );        // Configure ProductSubscriptionPlan entity
        modelBuilder.Entity<Modules.Product.Models.ProductSubscriptionPlan>(entity =>
            {
                entity.ToTable("ProductSubscriptionPlans");
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Description).HasMaxLength(1000);

                // Configure relationship with Product
                entity.HasOne(psp => psp.Product)
                    .WithMany()
                    .HasForeignKey(psp => psp.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indexes
                entity.HasIndex(psp => psp.ProductId);
                entity.HasIndex(psp => psp.Name);
            }
        );        // Configure UserProduct entity
        modelBuilder.Entity<Modules.Product.Models.UserProduct>(entity =>
            {
                entity.ToTable("UserProducts");
                entity.Property(e => e.AcquisitionType).IsRequired().HasConversion<int>();
                entity.Property(e => e.AccessStatus).IsRequired().HasConversion<int>();
                entity.Property(e => e.PricePaid).HasColumnType("decimal(10,2)");
                entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);

                // Configure relationship with User
                entity.HasOne(up => up.User)
                    .WithMany()
                    .HasForeignKey(up => up.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Configure relationship with Product
                entity.HasOne(up => up.Product)
                    .WithMany()
                    .HasForeignKey(up => up.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Configure relationship with Subscription
                entity.HasOne(up => up.Subscription)
                    .WithMany()
                    .HasForeignKey(up => up.SubscriptionId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Configure relationship with GiftedByUser
                entity.HasOne(up => up.GiftedByUser)
                    .WithMany()
                    .HasForeignKey(up => up.GiftedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Indexes
                entity.HasIndex(up => new { up.UserId, up.ProductId }).IsUnique();
                entity.HasIndex(up => up.AcquisitionType);
                entity.HasIndex(up => up.AccessStatus);
                entity.HasIndex(up => up.AccessStartDate);
                entity.HasIndex(up => up.AccessEndDate);
            }
        );        // Configure PromoCode entity
        modelBuilder.Entity<Modules.Product.Models.PromoCode>(entity =>
            {
                entity.ToTable("PromoCodes");
                entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Type).IsRequired().HasConversion<int>();
                entity.Property(e => e.DiscountPercentage).HasColumnType("decimal(5,2)");
                entity.Property(e => e.DiscountAmount).HasColumnType("decimal(10,2)");
                entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
                entity.Property(e => e.MinimumOrderAmount).HasColumnType("decimal(10,2)");

                // Configure relationship with CreatedByUser
                entity.HasOne(pc => pc.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(pc => pc.CreatedBy)
                    .OnDelete(DeleteBehavior.Restrict);

                // Configure relationship with Product (optional)
                entity.HasOne(pc => pc.Product)
                    .WithMany()
                    .HasForeignKey(pc => pc.ProductId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Indexes
                entity.HasIndex(pc => pc.Code).IsUnique().HasFilter("\"DeletedAt\" IS NULL");
                entity.HasIndex(pc => pc.IsActive);
                entity.HasIndex(pc => pc.ValidFrom);
                entity.HasIndex(pc => pc.ValidUntil);
            }
        );        // Configure PromoCodeUse entity
        modelBuilder.Entity<Modules.Product.Models.PromoCodeUse>(entity =>
            {
                entity.ToTable("PromoCodeUses");
                entity.Property(e => e.DiscountApplied).IsRequired().HasColumnType("decimal(10,2)");

                // Configure relationship with PromoCode
                entity.HasOne(pcu => pcu.PromoCode)
                    .WithMany(pc => pc.PromoCodeUses)
                    .HasForeignKey(pcu => pcu.PromoCodeId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure relationship with User
                entity.HasOne(pcu => pcu.User)
                    .WithMany()
                    .HasForeignKey(pcu => pcu.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure relationship with FinancialTransaction
                entity.HasOne(pcu => pcu.FinancialTransaction)
                    .WithMany()
                    .HasForeignKey(pcu => pcu.FinancialTransactionId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Indexes
                entity.HasIndex(pcu => pcu.PromoCodeId);
                entity.HasIndex(pcu => pcu.UserId);
                entity.HasIndex(pcu => pcu.CreatedAt); // Used instead of UsedAt
            }
        );
    }

    /// <summary>
    /// Configure Program Module entities
    /// </summary>
    private void ConfigureProgramModule(ModelBuilder modelBuilder)
    {        // Configure Program entity
        modelBuilder.Entity<Modules.Program.Models.Program>(entity =>
            {
                entity.ToTable("programs");
                entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Description).HasMaxLength(2000);
                entity.Property(e => e.Thumbnail).HasMaxLength(500);
                entity.Property(e => e.Visibility).IsRequired().HasConversion<int>();
                entity.Property(e => e.Status).IsRequired().HasConversion<int>();
                entity.Property(e => e.Slug).IsRequired().HasMaxLength(255);

                // Configure relationship with Owner
                entity.HasOne(p => p.Owner)
                    .WithMany()
                    .HasForeignKey("OwnerId")
                    .OnDelete(DeleteBehavior.Restrict);

                // Indexes
                entity.HasIndex(p => p.Title);
                entity.HasIndex(p => p.Status);
                entity.HasIndex(p => p.Visibility);
                entity.HasIndex(p => p.Slug).IsUnique();
            }
        );        // Configure ProgramContent entity
        modelBuilder.Entity<Modules.Program.Models.ProgramContent>(entity =>
            {
                entity.ToTable("program_contents");
                entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Description).HasMaxLength(2000);
                entity.Property(e => e.Type).IsRequired().HasConversion<int>();
                entity.Property(e => e.Body).HasColumnType("jsonb");
                entity.Property(e => e.MaxPoints).HasColumnType("decimal(5,2)");
                entity.Property(e => e.Visibility).IsRequired().HasConversion<int>();

                // Configure relationship with Program
                entity.HasOne(pc => pc.Program)
                    .WithMany(p => p.ProgramContents)
                    .HasForeignKey(pc => pc.ProgramId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure relationship with Parent (self-referencing)
                entity.HasOne(pc => pc.Parent)
                    .WithMany(pc => pc.Children)
                    .HasForeignKey(pc => pc.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Indexes
                entity.HasIndex(pc => pc.ProgramId);
                entity.HasIndex(pc => pc.ParentId);
                entity.HasIndex(pc => pc.SortOrder);
                entity.HasIndex(pc => pc.Type);
                entity.HasIndex(pc => pc.Visibility);
            }
        );        // Configure ProgramUser entity
        modelBuilder.Entity<Modules.Program.Models.ProgramUser>(entity =>
            {
                entity.ToTable("program_users");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CompletionPercentage).HasColumnType("decimal(5,2)").HasDefaultValue(0);
                entity.Property(e => e.FinalGrade).HasColumnType("decimal(5,2)");

                // Configure relationship with Program
                entity.HasOne(pu => pu.Program)
                    .WithMany(p => p.ProgramUsers)
                    .HasForeignKey(pu => pu.ProgramId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure relationship with User
                entity.HasOne(pu => pu.User)
                    .WithMany()
                    .HasForeignKey(pu => pu.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint
                entity.HasIndex(pu => new
                        {
                            pu.ProgramId, pu.UserId
                        }
                    )
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL");

                // Indexes
                entity.HasIndex(pu => pu.IsActive);
                entity.HasIndex(pu => pu.JoinedAt);
                entity.HasIndex(pu => pu.CompletedAt);
            }
        );        // Configure ProgramUserRole entity
        modelBuilder.Entity<Modules.Program.Models.ProgramUserRole>(entity =>
            {
                entity.ToTable("program_user_roles");
                entity.Property(e => e.Role).IsRequired().HasConversion<int>();

                // Configure relationship with Program
                entity.HasOne(pur => pur.Program)
                    .WithMany()
                    .HasForeignKey(pur => pur.ProgramId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure relationship with ProgramUser
                entity.HasOne(pur => pur.ProgramUser)
                    .WithMany(pu => pu.ProgramUserRoles)
                    .HasForeignKey(pur => pur.ProgramUserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint
                entity.HasIndex(pur => new
                        {
                            pur.ProgramId, pur.ProgramUserId, pur.Role
                        }
                    )
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL");

                // Indexes
                entity.HasIndex(pur => pur.Role);
                entity.HasIndex(pur => pur.ActiveFrom);
                entity.HasIndex(pur => pur.ActiveUntil);
            }
        );        // Configure ContentInteraction entity
        modelBuilder.Entity<Modules.Program.Models.ContentInteraction>(entity =>
            {
                entity.ToTable("content_interactions");
                entity.Property(e => e.Status).IsRequired().HasConversion<int>();
                entity.Property(e => e.SubmissionData).HasColumnType("jsonb");
                entity.Property(e => e.CompletionPercentage).HasColumnType("decimal(5,2)");

                // Configure relationship with ProgramUser
                entity.HasOne(ci => ci.ProgramUser)
                    .WithMany(pu => pu.ContentInteractions)
                    .HasForeignKey(ci => ci.ProgramUserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure relationship with Content
                entity.HasOne(ci => ci.Content)
                    .WithMany()
                    .HasForeignKey(ci => ci.ContentId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indexes
                entity.HasIndex(ci => ci.ProgramUserId);
                entity.HasIndex(ci => ci.ContentId);
                entity.HasIndex(ci => ci.Status);
                entity.HasIndex(ci => ci.LastAccessedAt);
                entity.HasIndex(ci => ci.CompletedAt);
            }
        );        // Configure ActivityGrade entity
        modelBuilder.Entity<Modules.Program.Models.ActivityGrade>(entity =>
            {
                entity.ToTable("activity_grades");
                entity.Property(e => e.Grade).IsRequired().HasColumnType("decimal(5,2)");
                entity.Property(e => e.Feedback).HasMaxLength(2000);
                entity.Property(e => e.GradingDetails).HasColumnType("jsonb");

                // Configure relationship with ContentInteraction
                entity.HasOne(ag => ag.ContentInteraction)
                    .WithMany(ci => ci.ActivityGrades)
                    .HasForeignKey(ag => ag.ContentInteractionId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure relationship with GraderProgramUser
                entity.HasOne(ag => ag.GraderProgramUser)
                    .WithMany()
                    .HasForeignKey(ag => ag.GraderProgramUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Unique constraint - one grade per content interaction
                entity.HasIndex(ag => ag.ContentInteractionId)
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL");

                // Indexes
                entity.HasIndex(ag => ag.Grade);
                entity.HasIndex(ag => ag.GradedAt);
            }
        );
    }

    /// <summary>
    /// Configure Certificate Module entities
    /// </summary>
    private void ConfigureCertificateModule(ModelBuilder modelBuilder)
    {        // Configure Certificate entity
        modelBuilder.Entity<Modules.Certificate.Models.Certificate>(entity =>
            {
                entity.ToTable("certificates");
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Description).HasMaxLength(2000);
                entity.Property(e => e.Type).IsRequired().HasConversion<int>();
                entity.Property(e => e.CompletionPercentage).HasColumnType("decimal(5,2)");
                entity.Property(e => e.MinimumGrade).HasColumnType("decimal(5,2)");
                entity.Property(e => e.MinimumRating).HasColumnType("decimal(2,1)");
                entity.Property(e => e.VerificationMethod).IsRequired().HasConversion<int>();
                entity.Property(e => e.CertificateTemplate).HasMaxLength(500);

                // Configure relationship with Program
                entity.HasOne(c => c.Program)
                    .WithMany()
                    .HasForeignKey(c => c.ProgramId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Configure relationship with Product
                entity.HasOne(c => c.Product)
                    .WithMany()
                    .HasForeignKey(c => c.ProductId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Indexes
                entity.HasIndex(c => c.Name);
                entity.HasIndex(c => c.Type);
                entity.HasIndex(c => c.IsActive);
            }
        );        // Configure UserCertificate entity
        modelBuilder.Entity<Modules.Certificate.Models.UserCertificate>(entity =>
            {
                entity.ToTable("user_certificates");
                entity.Property(e => e.VerificationCode).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Status).IsRequired().HasConversion<int>();
                entity.Property(e => e.FinalGrade).HasColumnType("decimal(5,2)");
                entity.Property(e => e.Metadata).HasMaxLength(1000);
                entity.Property(e => e.RevocationReason).HasMaxLength(500);

                // Configure relationship with Certificate
                entity.HasOne(uc => uc.Certificate)
                    .WithMany()
                    .HasForeignKey(uc => uc.CertificateId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure relationship with User
                entity.HasOne(uc => uc.User)
                    .WithMany()
                    .HasForeignKey(uc => uc.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure optional relationship with Program
                entity.HasOne(uc => uc.Program)
                    .WithMany()
                    .HasForeignKey(uc => uc.ProgramId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Configure optional relationship with Product
                entity.HasOne(uc => uc.Product)
                    .WithMany()
                    .HasForeignKey(uc => uc.ProductId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Configure optional relationship with ProgramUser
                entity.HasOne(uc => uc.ProgramUser)
                    .WithMany(pu => pu.UserCertificates)
                    .HasForeignKey(uc => uc.ProgramUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Indexes
                entity.HasIndex(uc => uc.CertificateId);
                entity.HasIndex(uc => uc.UserId);
                entity.HasIndex(uc => uc.Status);
                entity.HasIndex(uc => uc.IssuedAt);
                entity.HasIndex(uc => uc.ExpiresAt);
                entity.HasIndex(uc => uc.VerificationCode).IsUnique();
            }
        );        // Configure CertificateTag entity
        modelBuilder.Entity<Modules.Certificate.Models.CertificateTag>(entity =>
            {
                entity.ToTable("certificate_tags");
                entity.Property(e => e.RelationshipType).IsRequired().HasConversion<int>();

                // Configure relationship with Certificate
                entity.HasOne(ct => ct.Certificate)
                    .WithMany()
                    .HasForeignKey(ct => ct.CertificateId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure relationship with Tag
                entity.HasOne(ct => ct.Tag)
                    .WithMany()
                    .HasForeignKey(ct => ct.TagId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint
                entity.HasIndex(ct => new
                        {
                            ct.CertificateId, ct.TagId
                        }
                    )
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL");

                // Indexes
                entity.HasIndex(ct => ct.RelationshipType);
            }
        );        // Configure CertificateBlockchainAnchor entity
        modelBuilder.Entity<Modules.Certificate.Models.CertificateBlockchainAnchor>(entity =>
            {
                entity.ToTable("certificate_blockchain_anchors");
                entity.Property(e => e.BlockchainNetwork).IsRequired().HasMaxLength(100);
                entity.Property(e => e.TransactionHash).IsRequired().HasMaxLength(200);
                entity.Property(e => e.BlockHash).HasMaxLength(200);
                entity.Property(e => e.ContractAddress).HasMaxLength(500);
                entity.Property(e => e.TokenId).HasMaxLength(100);
                entity.Property(e => e.Status).HasMaxLength(50);

                // Configure relationship with Certificate
                entity.HasOne(cba => cba.Certificate)
                    .WithMany(uc => uc.BlockchainAnchors)
                    .HasForeignKey(cba => cba.CertificateId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indexes
                entity.HasIndex(cba => cba.CertificateId);
                entity.HasIndex(cba => cba.TransactionHash).IsUnique();
                entity.HasIndex(cba => cba.BlockchainNetwork);
                entity.HasIndex(cba => cba.AnchoredAt);
                entity.HasIndex(cba => cba.Status);
            }
        );
    }

    /// <summary>
    /// Configure Tag Module entities
    /// </summary>
    private void ConfigureTagModule(ModelBuilder modelBuilder)
    {        // Configure Tag entity
        modelBuilder.Entity<Modules.Tag.Models.Tag>(entity =>
            {
                entity.ToTable("tags");
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Type).IsRequired().HasConversion<int>();
                entity.Property(e => e.Color).HasMaxLength(7); // Hex color code
                entity.Property(e => e.Icon).HasMaxLength(100);

                // Indexes
                entity.HasIndex(t => t.Name);
                entity.HasIndex(t => t.Type);
                entity.HasIndex(t => t.IsActive);
            }
        );

        // Configure TagRelationship entity
        modelBuilder.Entity<Modules.Tag.Models.TagRelationship>(entity =>
            {
                entity.ToTable("tag_relationships");
                entity.Property(e => e.Type).IsRequired().HasConversion<int>();
                entity.Property(e => e.Weight).HasColumnType("decimal(3,2)");
                entity.Property(e => e.Metadata).HasMaxLength(500);

                // Configure relationship with Source
                entity.HasOne(tr => tr.Source)
                    .WithMany(t => t.SourceRelationships)
                    .HasForeignKey(tr => tr.SourceId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure relationship with Target
                entity.HasOne(tr => tr.Target)
                    .WithMany(t => t.TargetRelationships)
                    .HasForeignKey(tr => tr.TargetId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint - prevent duplicate relationships
                entity.HasIndex(tr => new
                        {
                            tr.SourceId, tr.TargetId, tr.Type
                        }
                    )
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL");

                // Check constraint to prevent self-referencing
                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_TagRelationships_NoSelfReference",
                    "\"SourceId\" != \"TargetId\""
                ));                // Indexes
                entity.HasIndex(tr => tr.Type);
            }
        );// Configure TagProficiency entity
        modelBuilder.Entity<Modules.Tag.Models.TagProficiency>(entity =>
        {
            entity.ToTable("tag_proficiencies");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Type).IsRequired().HasConversion<int>();
            entity.Property(e => e.ProficiencyLevel).IsRequired().HasConversion<int>();
            entity.Property(e => e.Color).HasMaxLength(7);
            entity.Property(e => e.Icon).HasMaxLength(100);
            entity.Property(e => e.IsActive).IsRequired();

            // Indexes for performance
            entity.HasIndex(tp => tp.Name);
            entity.HasIndex(tp => tp.Type);
            entity.HasIndex(tp => tp.ProficiencyLevel);
            entity.HasIndex(tp => tp.IsActive);
        });
    }

    /// <summary>
    /// Configure Subscription Module entities
    /// </summary>
    private void ConfigureSubscriptionModule(ModelBuilder modelBuilder)
    {        // Configure UserSubscription entity
        modelBuilder.Entity<Modules.Subscription.Models.UserSubscription>(entity =>
        {
            entity.ToTable("user_subscriptions");
            entity.Property(e => e.Status).IsRequired().HasConversion<int>();
            entity.Property(e => e.ExternalSubscriptionId).HasMaxLength(255);

            // Configure relationship with User
            entity.HasOne(us => us.User)
                .WithMany()
                .HasForeignKey(us => us.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure relationship with SubscriptionPlan
            entity.HasOne(us => us.SubscriptionPlan)
                .WithMany()
                .HasForeignKey(us => us.SubscriptionPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            entity.HasIndex(us => us.UserId);
            entity.HasIndex(us => us.Status);
            entity.HasIndex(us => us.SubscriptionPlanId);
            entity.HasIndex(us => us.CurrentPeriodStart);
            entity.HasIndex(us => us.CurrentPeriodEnd);
            entity.HasIndex(us => us.NextBillingAt);
        });
    }

    /// <summary>
    /// Configure Payment Module entities
    /// </summary>
    private void ConfigurePaymentModule(ModelBuilder modelBuilder)
    {        // Configure UserFinancialMethod entity
        modelBuilder.Entity<Modules.Payment.Models.UserFinancialMethod>(entity =>
        {
            entity.ToTable("user_financial_methods");
            entity.Property(e => e.Type).IsRequired().HasConversion<int>();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ExternalId).HasMaxLength(255);
            entity.Property(e => e.LastFour).HasMaxLength(10);
            entity.Property(e => e.ExpiryMonth).HasMaxLength(2);
            entity.Property(e => e.ExpiryYear).HasMaxLength(4);
            entity.Property(e => e.Brand).HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasConversion<int>();

            // Configure relationship with User
            entity.HasOne(ufm => ufm.User)
                .WithMany()
                .HasForeignKey(ufm => ufm.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(ufm => ufm.UserId);
            entity.HasIndex(ufm => ufm.Type);
            entity.HasIndex(ufm => ufm.Status);
            entity.HasIndex(ufm => ufm.IsDefault);
        });        // Configure FinancialTransaction entity
        modelBuilder.Entity<Modules.Payment.Models.FinancialTransaction>(entity =>
        {
            entity.ToTable("financial_transactions");
            entity.Property(e => e.Type).IsRequired().HasConversion<int>();
            entity.Property(e => e.Amount).IsRequired().HasColumnType("decimal(10,2)");
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
            entity.Property(e => e.Status).IsRequired().HasConversion<int>();
            entity.Property(e => e.ExternalTransactionId).HasMaxLength(255);
            entity.Property(e => e.PlatformFee).HasColumnType("decimal(10,2)");
            entity.Property(e => e.ProcessorFee).HasColumnType("decimal(10,2)");
            entity.Property(e => e.NetAmount).HasColumnType("decimal(10,2)");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Metadata).HasMaxLength(1000);
            entity.Property(e => e.ErrorMessage).HasMaxLength(500);

            // Configure relationship with FromUser
            entity.HasOne(ft => ft.FromUser)
                .WithMany()
                .HasForeignKey(ft => ft.FromUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure relationship with ToUser
            entity.HasOne(ft => ft.ToUser)
                .WithMany()
                .HasForeignKey(ft => ft.ToUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure relationship with PaymentMethod
            entity.HasOne(ft => ft.PaymentMethod)
                .WithMany()
                .HasForeignKey(ft => ft.PaymentMethodId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure relationship with PromoCode
            entity.HasOne(ft => ft.PromoCode)
                .WithMany()
                .HasForeignKey(ft => ft.PromoCodeId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes
            entity.HasIndex(ft => ft.FromUserId);
            entity.HasIndex(ft => ft.ToUserId);
            entity.HasIndex(ft => ft.Type);
            entity.HasIndex(ft => ft.Status);
            entity.HasIndex(ft => ft.CreatedAt);
            entity.HasIndex(ft => ft.ExternalTransactionId);
        });
    }

    /// <summary>
    /// Configure KYC Module entities
    /// </summary>
    private void ConfigureKycModule(ModelBuilder modelBuilder)
    {        // Configure UserKycVerification entity
        modelBuilder.Entity<Modules.Kyc.Models.UserKycVerification>(entity =>
        {
            entity.ToTable("user_kyc_verifications");
            entity.Property(e => e.Provider).IsRequired().HasConversion<int>();
            entity.Property(e => e.Status).IsRequired().HasConversion<int>();
            entity.Property(e => e.ExternalVerificationId).HasMaxLength(255);
            entity.Property(e => e.VerificationLevel).HasMaxLength(50);
            entity.Property(e => e.DocumentTypes).HasMaxLength(500);
            entity.Property(e => e.DocumentCountry).HasMaxLength(2);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.ProviderData).HasColumnType("jsonb");

            // Configure relationship with User
            entity.HasOne(ukv => ukv.User)
                .WithMany()
                .HasForeignKey(ukv => ukv.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(ukv => ukv.UserId);
            entity.HasIndex(ukv => ukv.Provider);
            entity.HasIndex(ukv => ukv.Status);
            entity.HasIndex(ukv => ukv.SubmittedAt);
            entity.HasIndex(ukv => ukv.ExternalVerificationId);
        });
    }

    /// <summary>
    /// Configure Feedback Module entities
    /// </summary>
    private void ConfigureFeedbackModule(ModelBuilder modelBuilder)
    {        // Configure ProgramFeedbackSubmission entity
        modelBuilder.Entity<Modules.Feedback.Models.ProgramFeedbackSubmission>(entity =>
        {
            entity.ToTable("program_feedback_submissions");
            entity.Property(e => e.FeedbackData).IsRequired().HasColumnType("jsonb");
            entity.Property(e => e.OverallRating).HasColumnType("decimal(2,1)");

            // Configure relationship with Program
            entity.HasOne(pfs => pfs.Program)
                .WithMany()
                .HasForeignKey(pfs => pfs.ProgramId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure relationship with User
            entity.HasOne(pfs => pfs.User)
                .WithMany()
                .HasForeignKey(pfs => pfs.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure optional relationship with Product
            entity.HasOne(pfs => pfs.Product)
                .WithMany()
                .HasForeignKey(pfs => pfs.ProductId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure relationship with ProgramUser
            entity.HasOne(pfs => pfs.ProgramUser)
                .WithMany()
                .HasForeignKey(pfs => pfs.ProgramUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(pfs => pfs.ProgramId);
            entity.HasIndex(pfs => pfs.UserId);
            entity.HasIndex(pfs => pfs.ProgramUserId);
            entity.HasIndex(pfs => pfs.SubmittedAt);
        });

        // Configure ProgramRating entity
        modelBuilder.Entity<Modules.Feedback.Models.ProgramRating>(entity =>
            {
                entity.ToTable("ProgramRatings");
                entity.Property(e => e.Rating).IsRequired().HasColumnType("decimal(3,2)");
                entity.Property(e => e.Review).HasMaxLength(2000);

                // Configure relationship with Program
                entity.HasOne(pr => pr.Program)
                    .WithMany()
                    .HasForeignKey(pr => pr.ProgramId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure relationship with User
                entity.HasOne(pr => pr.User)
                    .WithMany()
                    .HasForeignKey(pr => pr.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint - one rating per user per program
                entity.HasIndex(pr => new
                        {
                            pr.ProgramId, pr.UserId
                        }
                    )
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL");

                // Indexes
                entity.HasIndex(pr => pr.Rating);
                entity.HasIndex(pr => pr.CreatedAt);
            }
        );
    }
}
