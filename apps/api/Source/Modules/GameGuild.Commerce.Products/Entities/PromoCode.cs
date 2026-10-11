using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Products;

/// <summary>Entity representing promotional codes for discounts</summary>
[Table("promo_codes")]
[Index(nameof(Code), IsUnique = true)]
[Index(nameof(Type))]
[Index(nameof(IsActive))]
[Index(nameof(ValidFrom))]
[Index(nameof(ValidUntil))]
[Index(nameof(CampaignId))]
[Index(nameof(IsActive), nameof(ValidUntil))]
public class PromoCode : EntityBase
{
    /// <summary>Default constructor</summary>
    public PromoCode() { }

    /// <summary>Constructor for partial initialization</summary>
    /// <param name="partial">Partial promo code data</param>
    public PromoCode(object partial) : base(partial) { }

    /// <summary>The promotional code that users enter (stored uppercase; always compare via <see cref="PromoCodeNormalizer.Normalize"/>)</summary>
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name for the promotional code</summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Description of what the promo code does</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>Type of discount this promo code provides</summary>
    public PromoCodeType Type { get; set; }

    /// <summary>Discount percentage (for PercentageOff type)</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? DiscountPercentage { get; set; }

    /// <summary>Fixed discount amount (for FixedAmountOff; target price for SetPrice)</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? DiscountAmount { get; set; }

    /// <summary>Maximum discount amount that caps percentage discounts (null = no cap)</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? MaxDiscountAmount { get; set; }

    /// <summary>Currency code for fixed amount discounts</summary>
    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    /// <summary>Minimum order amount required to use this promo code</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? MinimumOrderAmount { get; set; }

    /// <summary>Maximum number of times this code can be used (null = unlimited)</summary>
    public int? MaxUses { get; set; }

    /// <summary>Maximum uses per user (null = unlimited per user)</summary>
    public int? MaxUsesPerUser { get; set; }

    /// <summary>Number of recorded uses; incremented atomically when usage is recorded</summary>
    public int CurrentUses { get; set; } = 0;

    /// <summary>Trial extension in days granted by FreeTrialExtension codes</summary>
    public int? FreeTrialExtensionDays { get; set; }

    /// <summary>Number of billing periods granted by FreeMonths codes (also the discount duration for other types)</summary>
    public int? DurationMonths { get; set; }

    /// <summary>Whether the discount also applies to renewals</summary>
    public bool ApplyToRenewal { get; set; } = false;

    /// <summary>When this promo code becomes valid</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>When this promo code expires</summary>
    public DateTime? ValidUntil { get; set; }

    /// <summary>Whether this promo code is currently active</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Whether this promo code cannot be combined with others</summary>
    public bool IsExclusive { get; set; } = false;

    /// <summary>Priority for stacking (higher = applied first)</summary>
    public int StackingPriority { get; set; } = 0;

    /// <summary>Campaign identifier used to group codes for performance tracking</summary>
    [MaxLength(100)]
    public string? CampaignId { get; set; }

    /// <summary>Specific product this code applies to (null = applies to all products)</summary>
    public Guid? ProductId { get; set; }

    /// <summary>Navigation property to the specific product (if applicable)</summary>
    [ForeignKey(nameof(ProductId))]
    public virtual Product? Product { get; set; }

    /// <summary>Foreign key to the user who created this promo code</summary>
    [Required]
    public Guid CreatedBy { get; set; }

    /// <summary>Navigation property to the user who created this promo code</summary>
    [ForeignKey(nameof(CreatedBy))]
    public virtual User CreatedByUser { get; set; } = null!;

    /// <summary>Navigation property to promo code usage records</summary>
    public virtual ICollection<PromoCodeUse> PromoCodeUses { get; set; } = new List<PromoCodeUse>();

    /// <summary>Check if the promo code is currently valid</summary>
    public bool IsCurrentlyValid()
    {
        if (!IsActive) return false;

        var now = SystemClock.UtcNow;
        if (ValidFrom.HasValue && ValidFrom.Value > now)
            return false;

        if (ValidUntil.HasValue && ValidUntil.Value <= now)
            return false;

        return true;
    }

    /// <summary>
    /// Calculate the discount amount for a given order amount. Supports all five
    /// discount types with a monetary outcome on the current charge:
    /// <list type="bullet">
    /// <item><see cref="PromoCodeType.PercentageOff"/>: percentage of the amount, capped by <see cref="MaxDiscountAmount"/> when set.</item>
    /// <item><see cref="PromoCodeType.FixedAmountOff"/>: fixed amount, never exceeding the order amount.</item>
    /// <item><see cref="PromoCodeType.FreeTrialExtension"/>: full current charge waived; <see cref="FreeTrialExtensionDays"/> trial days are granted at checkout.</item>
    /// <item><see cref="PromoCodeType.FreeMonths"/>: full current charge waived; <see cref="DurationMonths"/> billing periods are skipped.</item>
    /// <item><see cref="PromoCodeType.SetPrice"/>: discount is the difference between the order amount and the set price.</item>
    /// </list>
    /// </summary>
    public decimal CalculateDiscount(decimal orderAmount)
    {
        if (!IsCurrentlyValid()) return 0;

        if (MinimumOrderAmount.HasValue && orderAmount < MinimumOrderAmount.Value) return 0;

        return Type switch
        {
            PromoCodeType.PercentageOff => CapPercentageDiscount(orderAmount * (DiscountPercentage ?? 0) / 100, orderAmount),
            PromoCodeType.FixedAmountOff => Math.Min(DiscountAmount ?? 0, orderAmount),
            PromoCodeType.FreeTrialExtension => FreeTrialExtensionDays is > 0 ? orderAmount : 0,
            PromoCodeType.FreeMonths => DurationMonths is > 0 ? orderAmount : 0,
            PromoCodeType.SetPrice => DiscountAmount.HasValue
                ? Math.Max(0, Math.Min(orderAmount - DiscountAmount.Value, orderAmount))
                : 0,
            _ => 0
        };
    }

    /// <summary>Apply the <see cref="MaxDiscountAmount"/> cap to a percentage discount, never exceeding the order amount.</summary>
    private decimal CapPercentageDiscount(decimal rawDiscount, decimal orderAmount)
    {
        var capped = MaxDiscountAmount.HasValue ? Math.Min(rawDiscount, MaxDiscountAmount.Value) : rawDiscount;
        return Math.Min(capped, orderAmount);
    }

    /// <summary>Check if this promo code is exclusive (cannot be stacked)</summary>
    public bool GetIsExclusive()
    {
        return IsExclusive;
    }
}
