using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Refund-approval workflow tunables (issue #419). Safe defaults: no manual-approval
///     threshold is configured, so refund requests execute immediately; when a threshold is
///     configured, requests at or above it wait in the pending-approval queue until an
///     administrator holding <c>payments:refunds:approve</c> decides.
/// </summary>
public sealed class RefundApprovalOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Payments:Refunds";

    /// <summary>
    ///     Amount (in major currency units) at or above which a refund request requires manual
    ///     approval. Null (default) disables manual approval — every request auto-approves.
    ///     Amounts strictly below the threshold always auto-approve.
    /// </summary>
    [Range(0.01, 1_000_000)]
    public decimal? ManualApprovalThresholdAmount { get; set; }

    /// <summary>
    ///     Email addresses notified when a refund enters the pending-approval queue. Empty by
    ///     default: approval notifications are visible through the pending-approvals endpoint and
    ///     no email is sent until recipients are configured.
    /// </summary>
    [MaxLength(64)]
    public IReadOnlyList<string> ApproverNotificationEmails { get; set; } = [];
}

/// <summary>Validates <see cref="RefundApprovalOptions"/> bound from configuration.</summary>
public sealed class RefundApprovalOptionsValidator : IValidateOptions<RefundApprovalOptions>
{
    public ValidateOptionsResult Validate(string? name, RefundApprovalOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.ManualApprovalThresholdAmount is { } threshold && threshold <= 0)
        {
            failures.Add("ManualApprovalThresholdAmount must be positive when configured.");
        }

        foreach (var email in options.ApproverNotificationEmails)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
            {
                failures.Add($"ApproverNotificationEmails contains an invalid address: '{email}'.");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
