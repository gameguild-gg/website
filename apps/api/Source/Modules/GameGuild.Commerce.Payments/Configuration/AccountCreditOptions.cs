using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Account credits options (issue #410 v1): daily expiry worker gating and the
///     referral program reward configuration (single active program, config-driven in v1).
/// </summary>
public sealed class AccountCreditOptions
{
    public const string SectionName = "AccountCredits";

    /// <summary>Enables the daily credit/gift expiry worker (default true; expiry only ever drains already-due balances).</summary>
    public bool WorkerEnabled { get; set; } = true;

    /// <summary>Worker interval in hours; clamped to at least 1 hour.</summary>
    public int WorkerIntervalHours { get; set; } = 24;

    /// <summary>Upper bound of entities each expiry pass may flip (bounded work per tick).</summary>
    public int ExpiryBatchSize { get; set; } = 500;

    /// <summary>Credit granted to the referrer when a referral converts (first paid invoice).</summary>
    public decimal ReferrerRewardAmount { get; set; } = 10m;

    /// <summary>Credit granted to the referee when a referral converts (first paid invoice).</summary>
    public decimal RefereeRewardAmount { get; set; } = 5m;

    /// <summary>ISO 4217 currency referral rewards are granted in.</summary>
    public string ReferralRewardCurrency { get; set; } = "USD";

    /// <summary>Maximum rewarded referrals per referrer; null means unlimited.</summary>
    public int? MaxReferralsPerUser { get; set; } = 25;

    /// <summary>Referee must have a paid invoice before rewards are issued (v1 default: fail-closed true).</summary>
    public bool RequirePayment { get; set; } = true;

    /// <summary>Base URL used to build trackable referral share links.</summary>
    public string ReferralLinkBaseUrl { get; set; } = "https://app.gameguild.gg/signup";

    /// <summary>Days after which an unrewarded referral expires (0 = never).</summary>
    public int ReferralExpiryDays { get; set; } = 90;
}

/// <summary>Validates <see cref="AccountCreditOptions" />.</summary>
public sealed class AccountCreditOptionsValidator : IValidateOptions<AccountCreditOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AccountCreditOptions options)
    {
        var failures = new List<string>();

        if (options.WorkerIntervalHours < 1)
        {
            failures.Add("AccountCredits:WorkerIntervalHours must be at least 1 hour.");
        }

        if (options.ExpiryBatchSize is < 1 or > 1000)
        {
            failures.Add("AccountCredits:ExpiryBatchSize must be between 1 and 1000.");
        }

        if (options.ReferrerRewardAmount < 0 || options.RefereeRewardAmount < 0)
        {
            failures.Add("AccountCredits:ReferrerRewardAmount and RefereeRewardAmount cannot be negative.");
        }

        if (options.ReferrerRewardAmount <= 0 && options.RefereeRewardAmount <= 0)
        {
            failures.Add("At least one referral reward amount must be positive.");
        }

        if (string.IsNullOrWhiteSpace(options.ReferralRewardCurrency)
            || options.ReferralRewardCurrency.Length != 3
            || !Iso4217CurrencyCatalog.Exists(options.ReferralRewardCurrency))
        {
            failures.Add("AccountCredits:ReferralRewardCurrency must be a known ISO 4217 code.");
        }

        if (options.MaxReferralsPerUser is < 1)
        {
            failures.Add("AccountCredits:MaxReferralsPerUser must be at least 1 when set.");
        }

        if (options.ReferralExpiryDays < 0)
        {
            failures.Add("AccountCredits:ReferralExpiryDays cannot be negative.");
        }

        if (!Uri.TryCreate(options.ReferralLinkBaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != "https")
        {
            failures.Add("AccountCredits:ReferralLinkBaseUrl must be an absolute https URL.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
