using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GameGuild.Common.Entities;

namespace GameGuild.Modules.Reputation.Models;

/// <summary>
/// Tracks a user's reputation score and tier within a specific tenant context
/// Supports tenant-specific reputation that is separate from global user reputation
/// </summary>
[Table("UserTenantReputations")]
[Index(nameof(TenantPermissionId), IsUnique = true)]
[Index(nameof(Score))]
[Index(nameof(CurrentLevelId))]
public class UserTenantReputation : ResourceBase, IReputation
{
    /// <summary>
    /// The user-tenant relationship this reputation belongs to
    /// </summary>
    [Required]
    [ForeignKey(nameof(TenantPermissionId))]
    public required Modules.Tenant.Models.TenantPermission TenantPermission
    {
        get;
        set;
    }

    [Required]
    public Guid TenantPermissionId
    {
        get;
        set;
    }

    /// <summary>
    /// Current reputation score
    /// </summary>
    public int Score
    {
        get;
        set;
    } = 0;

    /// <summary>
    /// Current reputation tier (linked to configurable tier)
    /// </summary>
    [ForeignKey(nameof(CurrentLevelId))]
    public ReputationTier? CurrentLevel
    {
        get;
        set;
    }

    public Guid? CurrentLevelId
    {
        get;
        set;
    }

    /// <summary>
    /// When the reputation was last updated
    /// </summary>
    public DateTime LastUpdated
    {
        get;
        set;
    } = DateTime.UtcNow;

    /// <summary>
    /// When the user's reputation tier was last recalculated
    /// </summary>
    public DateTime? LastLevelCalculation
    {
        get;
        set;
    }

    /// <summary>
    /// Number of positive reputation changes
    /// </summary>
    public int PositiveChanges
    {
        get;
        set;
    } = 0;

    /// <summary>
    /// Number of negative reputation changes
    /// </summary>
    public int NegativeChanges
    {
        get;
        set;
    } = 0;

    /// <summary>
    /// History of reputation changes for this user-tenant
    /// </summary>
    public ICollection<UserReputationHistory> History
    {
        get;
        set;
    } = new List<UserReputationHistory>();
}

public class UserTenantReputationConfiguration : IEntityTypeConfiguration<UserTenantReputation>
{
    public void Configure(EntityTypeBuilder<UserTenantReputation> builder)
    {
        // Configure relationship with UserTenant (can't be done with annotations)
        builder.HasOne(utr => utr.TenantPermission)
            .WithMany()
            .HasForeignKey(utr => utr.TenantPermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure relationship with CurrentLevel (can't be done with annotations)
        builder.HasOne(utr => utr.CurrentLevel)
            .WithMany()
            .HasForeignKey(utr => utr.CurrentLevelId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
