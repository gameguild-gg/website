using System.Diagnostics.Metrics;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     MFA enrollment and verification metrics (issue #244): OpenTelemetry counters broken down
///     per method (Totp/Sms/Push/BackupCode) and outcome. Mirrors
///     <see cref="RefreshTokenLifecycleMetrics"/>: counters are fire-and-forget; instrumentation
///     must never throw into the MFA flows.
/// </summary>
public static class MfaEnrollmentMetrics
{
    public static string MeterName => "GameGuild.Identity.Authentication.Mfa";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Enrollments = Meter.CreateCounter<long>(
        "authentication.mfa.enrollments", "{enrollment}", "MFA enrollment lifecycle transitions per method and outcome.");

    private static readonly Counter<long> Verifications = Meter.CreateCounter<long>(
        "authentication.mfa.verifications", "{verification}", "MFA verification attempts per method and outcome.");

    /// <summary>Records an enrollment lifecycle transition (started/enrolled/disabled/failed).</summary>
    public static void RecordEnrollment(MfaMethod method, string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        try
        {
            Enrollments.Add(1,
                new KeyValuePair<string, object?>("method", method.ToString()),
                new KeyValuePair<string, object?>("outcome", outcome));
        }
        catch (Exception)
        {
            // Instrumentation must never break the security flow.
        }
    }

    /// <summary>Records a verification attempt outcome (succeeded/failed/timed-out).</summary>
    public static void RecordVerification(MfaMethod method, bool succeeded, string? outcome = null)
    {
        try
        {
            Verifications.Add(1,
                new KeyValuePair<string, object?>("method", method.ToString()),
                new KeyValuePair<string, object?>("outcome", outcome ?? (succeeded ? "succeeded" : "failed")));
        }
        catch (Exception)
        {
            // Instrumentation must never break the security flow.
        }
    }
}
