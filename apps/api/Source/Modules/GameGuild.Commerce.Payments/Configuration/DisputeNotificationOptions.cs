using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Dispute notification tunables (issue #419): who is alerted when a dispute is created and
///     when evidence deadlines approach. Safe defaults: no recipients are configured, so no email
///     leaves the system until operators opt in; the pending-dispute surface stays authoritative.
/// </summary>
public sealed class DisputeNotificationOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Payments:Disputes";

    /// <summary>
    ///     Email addresses alerted when a new dispute is recorded (gateway-webhook reconciled or
    ///     locally created). Empty by default: no team alert is sent.
    /// </summary>
    [MaxLength(64)]
    public IReadOnlyList<string> TeamAlertEmails { get; set; } = [];

    /// <summary>
    ///     Enables the daily dispute-deadline reminder worker. Default false: reminders only run
    ///     when an operator opts in.
    /// </summary>
    public bool DeadlineReminderEnabled { get; set; }

    /// <summary>
    ///     Reminders are sent for open disputes whose evidence deadline falls within this many
    ///     days (issue #419 spec: 7).
    /// </summary>
    [Range(1, 60)]
    public int ReminderDaysBeforeDeadline { get; set; } = 7;
}

/// <summary>Validates <see cref="DisputeNotificationOptions"/> bound from configuration.</summary>
public sealed class DisputeNotificationOptionsValidator : IValidateOptions<DisputeNotificationOptions>
{
    public ValidateOptionsResult Validate(string? name, DisputeNotificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        foreach (var email in options.TeamAlertEmails)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
            {
                failures.Add($"TeamAlertEmails contains an invalid address: '{email}'.");
            }
        }

        if (options.ReminderDaysBeforeDeadline is < 1 or > 60)
        {
            failures.Add("ReminderDaysBeforeDeadline must be between 1 and 60.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
