namespace GameGuild;

/// <summary>
///     Explicit rounding strategies accepted by <see cref="Money"/> when a decimal amount
///     carries more precision than the currency's minor-unit exponent allows.
/// </summary>
public enum MoneyRoundingMode
{
    /// <summary>
    ///     Midpoints round away from zero (0.005 USD -> 0.01). Historical default of the
    ///     platform and the behavior used when no mode is specified — preserves billing
    ///     semantics that predate per-currency exponents.
    /// </summary>
    AwayFromZero = 0,

    /// <summary>Banker's rounding: midpoints round to the nearest even digit (0.005 USD -> 0.00).</summary>
    ToEven = 1,

    /// <summary>Discards excess precision toward zero (0.009 USD -> 0.00). Never increases a charge.</summary>
    Truncate = 2,

    /// <summary>Always rounds surplus precision up (0.001 USD -> 0.01). Use for conservative settlement.</summary>
    Ceiling = 3
}
