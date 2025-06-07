using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using cms.Common.Entities;

namespace cms.Modules.Reputation.Models;

/// <summary>
/// Configurable reputation tier definition stored in the database
/// Allows dynamic configuration of reputation thresholds and permissions
/// </summary>
[Table("ReputationLevels")]
[Index(nameof(MinimumScore))]
[Index(nameof(SortOrder))]
public class ReputationTier : ResourceBase, ITenantable
{
    /// <summary>
    /// Unique name/identifier for this reputation tier
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string Name
    {
        get;
        set;
    }

    /// <summary>
    /// Display name for this reputation tier
    /// </summary>
    [Required]
    [MaxLength(200)]
    public required string DisplayName
    {
        get;
        set;
    }

    /// <summary>
    /// Minimum score required to achieve this tier
    /// </summary>
    public int MinimumScore
    {
        get;
        set;
    }

    /// <summary>
    /// Maximum score for this tier (null means no upper limit)
    /// </summary>
    public int? MaximumScore
    {
        get;
        set;
    }

    /// <summary>
    /// Color or visual identifier for this tier (hex color, CSS class, etc.)
    /// </summary>
    [MaxLength(50)]
    public string? Color
    {
        get;
        set;
    }

    /// <summary>
    /// Icon or badge identifier for this tier
    /// </summary>
    [MaxLength(100)]
    public string? Icon
    {
        get;
        set;
    }

    /// <summary>
    /// Order/priority of this tier (lower numbers = higher priority)
    /// </summary>
    public int SortOrder
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this tier is currently active
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    } = true;

    /// <summary>
    /// Users who have achieved this reputation tier
    /// </summary>
    public ICollection<UserReputation> UserReputations
    {
        get;
        set;
    } = new List<UserReputation>();

    /// <summary>
    /// Tenant this reputation tier belongs to (null for global tiers)
    /// </summary>
    [ForeignKey(nameof(TenantId))]
    public new Modules.Tenant.Models.Tenant? Tenant
    {
        get;
        set;
    }

    public Guid? TenantId
    {
        get;
        set;
    }

    /// <summary>
    /// Indicates whether this reputation tier is accessible across all tenants
    /// </summary>
    public new bool IsGlobal => TenantId == null;
}
