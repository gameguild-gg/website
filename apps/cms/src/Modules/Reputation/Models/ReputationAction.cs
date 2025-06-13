using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GameGuild.Common.Entities;

namespace GameGuild.Modules.Reputation.Models;

/// <summary>
/// Defines actions that can affect user reputation
/// Allows configurable reputation changes for different activities
/// </summary>
[Table("ReputationActions")]
[Index(nameof(ActionType), IsUnique = true)]
[Index(nameof(Points))]
[Index(nameof(IsActive))]
public class ReputationAction : ResourceBase
{
    /// <summary>
    /// Unique identifier for this action type
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string ActionType
    {
        get;
        set;
    }

    /// <summary>
    /// Display name for this action
    /// </summary>
    [Required]
    [MaxLength(200)]
    public required string DisplayName
    {
        get;
        set;
    }

    /// <summary>
    /// Description of what this action represents
    /// </summary>
    public new string? Description
    {
        get;
        set;
    }

    /// <summary>
    /// Points gained/lost when this action is performed
    /// </summary>
    public int Points
    {
        get;
        set;
    }

    /// <summary>
    /// Maximum number of times this action can award points per day
    /// (null for no limit)
    /// </summary>
    public int? DailyLimit
    {
        get;
        set;
    }

    /// <summary>
    /// Maximum number of times this action can award points total
    /// (null for no limit)
    /// </summary>
    public int? TotalLimit
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this action is currently active
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    } = true;

    /// <summary>
    /// Minimum reputation tier required to perform this action
    /// (null for no requirement)
    /// </summary>
    [ForeignKey(nameof(RequiredLevelId))]
    public ReputationTier? RequiredLevel
    {
        get;
        set;
    }

    public Guid? RequiredLevelId
    {
        get;
        set;
    }

    /// <summary>
    /// History of when this action was performed
    /// </summary>
    public ICollection<UserReputationHistory> ReputationHistory
    {
        get;
        set;
    } = new List<UserReputationHistory>();
}

public class ReputationActionConfiguration : IEntityTypeConfiguration<ReputationAction>
{
    public void Configure(EntityTypeBuilder<ReputationAction> builder)
    {
        // Configure relationship with RequiredLevel (can't be done with annotations)
        builder.HasOne(ra => ra.RequiredLevel)
            .WithMany()
            .HasForeignKey(ra => ra.RequiredLevelId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure shadow property for TenantId (since ReputationAction implements ITenantable)
        builder.Property<Guid?>("TenantId");

        // Filtered unique constraint (can't be done with annotations)
        builder.HasIndex("ActionType", "TenantId")
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");
    }
}
