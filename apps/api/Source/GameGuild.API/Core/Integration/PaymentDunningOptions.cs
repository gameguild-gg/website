using System.Globalization;
using GameGuild.Notifications;

namespace GameGuild.API.Integration;

/// <summary>
///     Configurable dunning escalation ladder (issue #403), bound from <c>Payments:Dunning</c>.
///     The default ladder reproduces the two legacy hardcoded dunning emails exactly:
///     a first-step reminder on payment failure and an urgent final notice when renewal fails.
/// </summary>
public sealed class PaymentDunningOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Payments:Dunning";

    /// <summary>
    ///     Escalation ladder, ordered by increasing <see cref="DunningStepOptions.DayOffset" />.
    ///     Each step binds a dunning stage to a day offset, template key, title, message and priority.
    /// </summary>
    public IList<DunningStepOptions> EscalationLadder { get; set; } = new List<DunningStepOptions>
    {
        new()
        {
            Stage = DunningStages.PaymentFailed,
            DayOffset = 0,
            Template = "PaymentFailedReminder",
            Title = "Action required: your payment failed",
            Message = "We were unable to process your most recent subscription payment on {date}. " +
                      "Reason: {reason}. Please update your payment method to avoid service interruption.",
            Priority = "High"
        },
        new()
        {
            Stage = DunningStages.FinalNotice,
            DayOffset = 3,
            Template = "FinalNotice",
            Title = "Final notice: your subscription could not be renewed",
            Message = "Your subscription renewal on {date} failed. Reason: {reason}. " +
                      "If we cannot collect payment, your subscription will be suspended. " +
                      "Please update your payment method now to keep your service active.",
            Priority = "Urgent"
        }
    };
}

/// <summary>One rung of the dunning escalation ladder.</summary>
public sealed class DunningStepOptions
{
    /// <summary>Dunning stage that triggers this step (<see cref="DunningStages" />).</summary>
    public string Stage { get; set; } = DunningStages.PaymentFailed;

    /// <summary>Days after the failed payment when this step is scheduled to fire.</summary>
    public int DayOffset { get; set; }

    /// <summary>Stable template key identifying the message template for this step.</summary>
    public string Template { get; set; } = "PaymentFailedReminder";

    /// <summary>Notification title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     Notification body. Supports the placeholders <c>{date}</c> (failure date, yyyy-MM-dd)
    ///     and <c>{reason}</c> (provider failure reason).
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Notification priority: Normal, High or Urgent.</summary>
    public string Priority { get; set; } = "High";
}

/// <summary>Well-known dunning stages used by the escalation ladder.</summary>
public static class DunningStages
{
    /// <summary>A subscription payment failed; first recovery reminder.</summary>
    public const string PaymentFailed = "PaymentFailed";

    /// <summary>Automatic renewal failed after retries; urgent final notice.</summary>
    public const string FinalNotice = "FinalNotice";
}

/// <summary>Resolves the applicable escalation-ladder step for a dunning event.</summary>
public static class PaymentDunningLadder
{
    /// <summary>
    ///     Selects the step of <paramref name="stage" /> whose day offset has been reached by
    ///     <paramref name="asOfUtc" /> since <paramref name="failedAtUtc" /> (the highest reached rung).
    ///     Falls back to the built-in default for the stage when the configured ladder has no
    ///     step for it (fail-closed to the legacy behavior); when the event arrives before the
    ///     first scheduled rung, the first rung is used.
    /// </summary>
    public static DunningStepOptions Resolve(
        IEnumerable<DunningStepOptions> ladder,
        string stage,
        DateTime failedAtUtc,
        DateTime asOfUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);

        var daysSinceFailure = Math.Max(0.0, (asOfUtc - failedAtUtc).TotalDays);
        var stageSteps = ladder
            .Where(step => string.Equals(step.Stage, stage, StringComparison.OrdinalIgnoreCase))
            .OrderBy(step => step.DayOffset)
            .ToList();

        if (stageSteps.Count == 0)
        {
            return DefaultStep(stage);
        }

        return stageSteps.LastOrDefault(step => step.DayOffset <= daysSinceFailure + ToleranceDays)
               ?? stageSteps[0];
    }

    /// <summary>Parses a ladder priority name, defaulting to <paramref name="fallback" />.</summary>
    public static NotificationPriority ParsePriority(string? priority, NotificationPriority fallback)
        => Enum.TryParse<NotificationPriority>(priority, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;

    /// <summary>Small tolerance so a step scheduled "today" fires on the event day itself.</summary>
    private const double ToleranceDays = 0.5;

    /// <summary>Interpolates <c>{date}</c> and <c>{reason}</c> placeholders in a template string.</summary>
    public static string Interpolate(string template, DateTime failedAtUtc, string reason)
        => template
            .Replace("{date}", failedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{reason}", reason, StringComparison.OrdinalIgnoreCase);

    private static DunningStepOptions DefaultStep(string stage)
        => string.Equals(stage, DunningStages.FinalNotice, StringComparison.OrdinalIgnoreCase)
            ? new DunningStepOptions
            {
                Stage = DunningStages.FinalNotice,
                DayOffset = 3,
                Template = "FinalNotice",
                Title = "Final notice: your subscription could not be renewed",
                Message = "Your subscription renewal on {date} failed. Reason: {reason}. " +
                          "If we cannot collect payment, your subscription will be suspended. " +
                          "Please update your payment method now to keep your service active.",
                Priority = "Urgent"
            }
            : new DunningStepOptions
            {
                Stage = DunningStages.PaymentFailed,
                DayOffset = 0,
                Template = "PaymentFailedReminder",
                Title = "Action required: your payment failed",
                Message = "We were unable to process your most recent subscription payment on {date}. " +
                          "Reason: {reason}. Please update your payment method to avoid service interruption.",
                Priority = "High"
            };
}
