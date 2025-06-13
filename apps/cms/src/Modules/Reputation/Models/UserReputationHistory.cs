using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GameGuild.Common.Entities;

namespace GameGuild.Modules.Reputation.Models;

/// <summary>
/// Tracks the history of reputation changes for any reputation entity
/// Provides audit trail and analytics for reputation system
/// Supports polymorphic relationships with UserReputation, UserTenantReputation, and future reputation entities
/// </summary>
[Table("UserReputationHistory")]
[Index(nameof(UserId), nameof(OccurredAt))]
[Index(nameof(TenantPermissionId), nameof(OccurredAt))]
[Index(nameof(ReputationActionId))]
[Index(nameof(OccurredAt))]
[Index(nameof(PointsChange))]
public class UserReputationHistory : ResourceBase
{
    /// <summary>
    /// The user whose reputation changed (for direct user reputation tracking)
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public Modules.User.Models.User? User
    {
        get;
        set;
    }

    public Guid? UserId
    {
        get;
        set;
    }

    /// <summary>
    /// The user-tenant whose reputation changed (for tenant-specific reputation tracking)
    /// </summary>
    [ForeignKey(nameof(TenantPermissionId))]
    public Modules.Tenant.Models.TenantPermission? TenantPermission
    {
        get;
        set;
    }

    public Guid? TenantPermissionId
    {
        get;
        set;
    }

    /// <summary>
    /// Polymorphic reference to the reputation entity that changed
    /// This can point to UserReputation, UserTenantReputation, or any future IReputation implementation
    /// Note: This is a computed property for convenience - the actual relationship is handled
    /// through UserId (for UserReputation) or UserTenantId (for UserTenantReputation)
    /// </summary>
    [NotMapped]
    public IReputation? Reputation
    {
        get;
        set;
    }

    /// <summary>
    /// The action that caused this reputation change
    /// </summary>
    [ForeignKey(nameof(ReputationActionId))]
    public ReputationAction? ReputationAction
    {
        get;
        set;
    }

    public Guid? ReputationActionId
    {
        get;
        set;
    }

    /// <summary>
    /// Points gained or lost in this change
    /// </summary>
    public int PointsChange
    {
        get;
        set;
    }

    /// <summary>
    /// User's reputation score before this change
    /// </summary>
    public int PreviousScore
    {
        get;
        set;
    }

    /// <summary>
    /// User's reputation score after this change
    /// </summary>
    public int NewScore
    {
        get;
        set;
    }

    /// <summary>
    /// Previous reputation tier (if different)
    /// </summary>
    [ForeignKey(nameof(PreviousLevelId))]
    public ReputationTier? PreviousLevel
    {
        get;
        set;
    }

    public Guid? PreviousLevelId
    {
        get;
        set;
    }

    /// <summary>
    /// New reputation tier (if changed)
    /// </summary>
    [ForeignKey(nameof(NewLevelId))]
    public ReputationTier? NewLevel
    {
        get;
        set;
    }

    public Guid? NewLevelId
    {
        get;
        set;
    }

    /// <summary>
    /// Optional reason for this reputation change
    /// </summary>
    [MaxLength(500)]
    public string? Reason
    {
        get;
        set;
    }

    /// <summary>
    /// User who triggered this change (null for system actions)
    /// </summary>
    [ForeignKey(nameof(TriggeredByUserId))]
    public Modules.User.Models.User? TriggeredByUser
    {
        get;
        set;
    }

    public Guid? TriggeredByUserId
    {
        get;
        set;
    }

    /// <summary>
    /// Related resource that triggered this change (polymorphic relationship)
    /// Entity Framework will create a shadow RelatedResourceId foreign key property automatically
    /// </summary>
    public ResourceBase? RelatedResource
    {
        get;
        set;
    }

    /// <summary>
    /// When this reputation change occurred
    /// </summary>
    public DateTime OccurredAt
    {
        get;
        set;
    } = DateTime.UtcNow;
}

public class UserReputationHistoryConfiguration : IEntityTypeConfiguration<UserReputationHistory>
{
    public void Configure(EntityTypeBuilder<UserReputationHistory> builder)
    {
        // Check constraint for polymorphic relationship (can't be done with annotations)
        builder.ToTable(
            "UserReputationHistory",
            t => t.HasCheckConstraint(
                "CK_UserReputationHistory_UserOrUserTenant",
                "(\"UserId\" IS NOT NULL AND \"UserTenantId\" IS NULL) OR (\"UserId\" IS NULL AND \"UserTenantId\" IS NOT NULL)"
            )
        );

        // Configure optional relationship with User (can't be done with annotations)
        builder.HasOne(urh => urh.User)
            .WithMany()
            .HasForeignKey(urh => urh.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure optional relationship with UserTenant (can't be done with annotations)
        builder.HasOne(urh => urh.TenantPermission)
            .WithMany()
            .HasForeignKey(urh => urh.TenantPermissionId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure relationship with ReputationAction (can't be done with annotations)
        builder.HasOne(urh => urh.ReputationAction)
            .WithMany(ra => ra.ReputationHistory)
            .HasForeignKey(urh => urh.ReputationActionId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure relationship with TriggeredByUser (can't be done with annotations)
        builder.HasOne(urh => urh.TriggeredByUser)
            .WithMany()
            .HasForeignKey(urh => urh.TriggeredByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure relationship with PreviousLevel (can't be done with annotations)
        builder.HasOne(urh => urh.PreviousLevel)
            .WithMany()
            .HasForeignKey(urh => urh.PreviousLevelId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure relationship with NewLevel (can't be done with annotations)
        builder.HasOne(urh => urh.NewLevel)
            .WithMany()
            .HasForeignKey(urh => urh.NewLevelId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure polymorphic relationship with RelatedResource (can't be done with annotations)
        builder.HasOne(urh => urh.RelatedResource)
            .WithMany()
            .HasForeignKey("RelatedResourceId")
            .OnDelete(DeleteBehavior.SetNull);
    }
}
