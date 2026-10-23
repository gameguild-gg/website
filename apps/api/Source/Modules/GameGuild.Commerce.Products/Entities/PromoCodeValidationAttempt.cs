using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Outcome of a promo code validation attempt (issue #418 conversion tracking).
/// The numerator of the campaign conversion metric is recorded usage
/// (<see cref="PromoCodeUse"/>); this entity is the denominator — every code
/// string submitted to the <c>:validate</c> endpoint, whether it succeeded or not.
/// </summary>
public enum PromoCodeValidationOutcome
{
    /// <summary>The code is valid for the submitted context</summary>
    Valid = 0,

    /// <summary>No promo code matches the submitted string</summary>
    NotFound = 1,

    /// <summary>The code exists but is inactive</summary>
    Inactive = 2,

    /// <summary>The code is outside its validity window</summary>
    Expired = 3,

    /// <summary>The code is scoped to a different product</summary>
    ProductNotEligible = 4,

    /// <summary>The code is scoped to a different subscription plan</summary>
    PlanNotEligible = 5,

    /// <summary>The submitted order amount is below the code minimum</summary>
    MinimumOrderAmountNotMet = 6,

    /// <summary>The code reached its total usage limit</summary>
    MaxUsesReached = 7,

    /// <summary>The user exhausted their per-user limit for the code</summary>
    MaxUsesPerUserReached = 8,

    /// <summary>Validation failed for another reason (empty input, calculation failure, ...)</summary>
    Invalid = 9
}

/// <summary>
/// One recorded promo code validation attempt (code, user, timestamp, outcome). Written by the
/// <c>promo-codes:validate</c> endpoint so campaign performance can compute a
/// validations-to-applications conversion rate (issue #418). Attempts for unknown code strings
/// are kept (with a null <see cref="PromoCodeId"/>) so code-farming patterns remain observable.
/// </summary>
[Table("promo_code_validation_attempts")]
[Index(nameof(PromoCodeId))]
[Index(nameof(Code))]
[Index(nameof(AttemptedAt))]
[Index(nameof(UserId))]
public class PromoCodeValidationAttempt : EntityBase
{
    /// <summary>Default constructor</summary>
    public PromoCodeValidationAttempt() { }

    /// <summary>Constructor for partial initialization</summary>
    /// <param name="partial">Partial attempt data</param>
    public PromoCodeValidationAttempt(object partial) : base(partial) { }

    /// <summary>The code string as submitted (normalized: trimmed, uppercased)</summary>
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Matching promo code, when the submitted string resolved to one
    /// (null = the code was not found).
    /// </summary>
    public Guid? PromoCodeId { get; set; }

    /// <summary>Authenticated user who submitted the validation (null = anonymous)</summary>
    public Guid? UserId { get; set; }

    /// <summary>Outcome of the validation attempt</summary>
    public PromoCodeValidationOutcome Outcome { get; set; } = PromoCodeValidationOutcome.Invalid;

    /// <summary>Human-readable failure message (null on success)</summary>
    [MaxLength(300)]
    public string? ErrorMessage { get; set; }

    /// <summary>When the attempt was recorded</summary>
    public DateTime AttemptedAt { get; set; } = SystemClock.UtcNow;
}
