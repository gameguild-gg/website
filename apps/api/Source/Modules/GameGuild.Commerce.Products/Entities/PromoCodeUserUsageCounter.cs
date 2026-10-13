using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Per-user usage counter for a promo code. The counter is incremented with a guarded
/// conditional update so that <c>MaxUsesPerUser</c> binds atomically under concurrency.
/// Composite key (PromoCodeId, UserId) is configured in <c>ProductsModule</c>.
/// </summary>
[Table("promo_code_user_usage_counters")]
public class PromoCodeUserUsageCounter
{
    /// <summary>Foreign key to the PromoCode entity (part of the composite primary key)</summary>
    [Required]
    public Guid PromoCodeId { get; set; }

    /// <summary>Foreign key to the User entity (part of the composite primary key)</summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>Number of recorded uses by this user for this code</summary>
    public int UseCount { get; set; }

    /// <summary>When the counter was created</summary>
    public DateTime CreatedAt { get; set; } = SystemClock.UtcNow;

    /// <summary>When the counter was last incremented</summary>
    public DateTime UpdatedAt { get; set; } = SystemClock.UtcNow;
}
