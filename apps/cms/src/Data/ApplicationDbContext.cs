using Microsoft.EntityFrameworkCore;
using cms.Modules.User.Models;
using cms.Modules.Tenant.Models;
using cms.Common.Entities;
using cms.Common.Data;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;

namespace cms.Data;

// NOTE: do not add fluent api configurations here, they should be in the same file of the entity. On the entity, use notations for simple configurations, and fluent API for complex ones.
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    // DbSets
    public DbSet<User> Users { get; set; }
    public DbSet<Credential> Credentials { get; set; }
    public DbSet<cms.Modules.Auth.Models.RefreshToken> RefreshTokens { get; set; }
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<cms.Modules.Tenant.Models.TenantPermission> TenantPermissions { get; set; }

    // Resource hierarchy DbSet - Required for proper inheritance configuration
    public DbSet<ResourceBase> Resources { get; set; }

    // Resource and Localization DbSets
    public DbSet<Language> Languages { get; set; }

    // Content hierarchy DbSets - Required for TPC inheritance configuration
    public DbSet<ContentLicense> ContentLicenses { get; set; }
    public DbSet<ResourceMetadata> ResourceMetadata { get; set; }
    public DbSet<ContentTypePermission> ContentTypePermissions { get; set; }
    public DbSet<ResourceLocalization> ResourceLocalizations { get; set; }

    // Reputation Management DbSets
    public DbSet<Modules.Reputation.Models.UserReputation> UserReputations { get; set; }
    public DbSet<Modules.Reputation.Models.UserTenantReputation> UserTenantReputations { get; set; }
    public DbSet<Modules.Reputation.Models.ReputationTier> ReputationTiers { get; set; }
    public DbSet<Modules.Reputation.Models.ReputationAction> ReputationActions { get; set; }
    public DbSet<Modules.Reputation.Models.UserReputationHistory> UserReputationHistory { get; set; }

    // Product Management DbSets
    public DbSet<Modules.Product.Models.Product> Products { get; set; }
    public DbSet<Modules.Product.Models.ProductPricing> ProductPricings { get; set; }
    public DbSet<Modules.Product.Models.ProductProgram> ProductPrograms { get; set; }
    public DbSet<Modules.Product.Models.ProductSubscriptionPlan> ProductSubscriptionPlans { get; set; }
    public DbSet<Modules.Product.Models.UserProduct> UserProducts { get; set; }
    public DbSet<Modules.Product.Models.PromoCode> PromoCodes { get; set; }
    public DbSet<Modules.Product.Models.PromoCodeUse> PromoCodeUses { get; set; }

    // Program Management DbSets
    public DbSet<Modules.Program.Models.Program> Programs { get; set; }
    public DbSet<Modules.Program.Models.ProgramContent> ProgramContents { get; set; }
    public DbSet<Modules.Program.Models.ProgramUser> ProgramUsers { get; set; }
    public DbSet<Modules.Program.Models.ContentInteraction> ContentInteractions { get; set; }
    public DbSet<Modules.Program.Models.ActivityGrade> ActivityGrades { get; set; }

    // Certificate Management DbSets
    public DbSet<Modules.Certificate.Models.Certificate> Certificates { get; set; }
    public DbSet<Modules.Certificate.Models.UserCertificate> UserCertificates { get; set; }
    public DbSet<Modules.Certificate.Models.CertificateTag> CertificateTags { get; set; }
    public DbSet<Modules.Certificate.Models.CertificateBlockchainAnchor> CertificateBlockchainAnchors { get; set; }

    // Tag Management DbSets
    public DbSet<Modules.Tag.Models.Tag> Tags { get; set; }
    public DbSet<Modules.Tag.Models.TagRelationship> TagRelationships { get; set; }
    public DbSet<Modules.Tag.Models.TagProficiency> TagProficiencies { get; set; }

    // Subscription Management DbSets
    public DbSet<Modules.Subscription.Models.UserSubscription> UserSubscriptions { get; set; }

    // Payment Management DbSets
    public DbSet<Modules.Payment.Models.UserFinancialMethod> UserFinancialMethods { get; set; }
    public DbSet<Modules.Payment.Models.FinancialTransaction> FinancialTransactions { get; set; }

    // KYC Management DbSets
    public DbSet<Modules.Kyc.Models.UserKycVerification> UserKycVerifications { get; set; }

    // Feedback Management DbSets
    public DbSet<Modules.Feedback.Models.ProgramFeedbackSubmission> ProgramFeedbackSubmissions { get; set; }
    public DbSet<Modules.Feedback.Models.ProgramRating> ProgramRatings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from the assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // NOTE: do not add fluent api configurations here, they should be in the same file of the entity. On the entity, use notations for simple configurations, and fluent API for complex ones.

        // Configure ContentTypePermission relationships explicitly to avoid ambiguity
        modelBuilder.Entity<ContentTypePermission>()
            .HasOne(ctp => ctp.User)
            .WithMany()
            .HasForeignKey(ctp => ctp.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure TenantPermission relationships explicitly to avoid ambiguity
        modelBuilder.Entity<TenantPermission>()
            .HasOne(tp => tp.User)
            .WithMany()
            .HasForeignKey(tp => tp.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure ITenantable entities (this logic needs to stay in OnModelCreating)
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

        // Configure inheritance strategies for content hierarchy
        ConfigureInheritanceStrategies(modelBuilder);
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
    /// Configure inheritance strategies for the content and resource hierarchies
    /// </summary>
    private void ConfigureInheritanceStrategies(ModelBuilder modelBuilder)
    {
        // Configure Table-Per-Concrete-Type (TPC) for ResourceBase inheritance
        // Each concrete entity that inherits from ResourceBase gets its own complete table
        // with all inherited properties included
        modelBuilder.Entity<ResourceBase>().UseTpcMappingStrategy();

        // Additional inheritance configurations can be added here as needed
    }
}
