using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Products;

/// <summary>Entity tracking when and how promo codes are used</summary>
[Table("promo_code_uses")]
[Index(nameof(PromoCodeId))]
[Index(nameof(UserId))]
[Index(nameof(PromoCodeId), nameof(UserId))]
[Index(nameof(UsedAt))]
[Index(nameof(IdempotencyKey), IsUnique = true)]
public class PromoCodeUse : EntityBase
{
    /// <summary>Default constructor</summary>
    public PromoCodeUse() { }

    /// <summary>Constructor for partial initialization</summary>
    /// <param name="partial">Partial promo code use data</param>
    public PromoCodeUse(object partial) : base(partial) { }

    /// <summary>Foreign key to the PromoCode entity</summary>
    [Required]
    public Guid PromoCodeId { get; set; }

    /// <summary>Navigation property to the PromoCode entity</summary>
    [ForeignKey(nameof(PromoCodeId))]
    public virtual PromoCode PromoCode { get; set; } = null!;

    /// <summary>Foreign key to the User entity</summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>Navigation property to the User entity</summary>
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    /// <summary>When the usage was recorded</summary>
    public DateTime UsedAt { get; set; } = SystemClock.UtcNow;

    /// <summary>The actual discount amount that was applied</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal DiscountApplied { get; set; }

    /// <summary>Currency of the applied discount (ISO 4217)</summary>
    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    /// <summary>Optional order the code was applied to</summary>
    public Guid? OrderId { get; set; }

    /// <summary>Optional order line item the code was applied to</summary>
    public Guid? OrderLineItemId { get; set; }

    /// <summary>
    /// Idempotency key supplied by the redemption flow. A repeated submission of the same
    /// key returns the previously recorded usage instead of counting a second use
    /// (unique index enforces replay protection at the database level).
    /// </summary>
    [MaxLength(200)]
    public string? IdempotencyKey { get; set; }
}
