using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Options for the billing wallet balance alerting (issue #406), bound from the
/// <c>Wallets</c> configuration section.
/// </summary>
public class WalletsOptions
{
    public const string SectionName = "Wallets";

    /// <summary>
    ///     Whether the low-balance monitor runs. Default true — balance alerts are the safe
    ///     default for prepaid wallet visibility.
    /// </summary>
    public bool BalanceAlertsEnabled { get; set; } = true;

    /// <summary>
    ///     Hours between low-balance monitoring passes. Default 24 (daily).
    /// </summary>
    public int BalanceAlertIntervalHours { get; set; } = 24;

    /// <summary>
    ///     Balance threshold below which a wallet owner is notified. Applied per wallet in the
    ///     wallet's own currency. Default 10.
    /// </summary>
    public decimal LowBalanceThreshold { get; set; } = 10m;

    /// <summary>
    ///     Whether completed provider invoice payments (billing webhooks) credit the paying
    ///     user's billing wallet (issue #406 integration with the billing stack). Default true;
    ///     credits are idempotent per provider event identifier.
    /// </summary>
    public bool WebhookCreditEnabled { get; set; } = true;
}

/// <summary>Validates <see cref="WalletsOptions" /> (issue #406).</summary>
public sealed class WalletsOptionsValidator : IValidateOptions<WalletsOptions>
{
    public ValidateOptionsResult Validate(string? name, WalletsOptions options)
    {
        if (options.BalanceAlertIntervalHours < 1)
        {
            return ValidateOptionsResult.Fail("Wallets:BalanceAlertIntervalHours must be at least 1 hour.");
        }

        if (options.LowBalanceThreshold < 0)
        {
            return ValidateOptionsResult.Fail("Wallets:LowBalanceThreshold cannot be negative.");
        }

        return ValidateOptionsResult.Success;
    }
}
