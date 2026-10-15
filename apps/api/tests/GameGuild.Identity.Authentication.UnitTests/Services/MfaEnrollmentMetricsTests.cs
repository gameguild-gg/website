using System.Diagnostics.Metrics;
using FluentAssertions;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
///     MFA enrollment/verification metrics tests (issue #244): counters are observable through a
///     meter listener with per-method and per-outcome dimensions, and never throw.
/// </summary>
public sealed class MfaEnrollmentMetricsTests
{
    [Fact]
    public void EnrollmentCounter_RecordsMethodAndOutcomeDimensions()
    {
        var measurements = new List<(string Method, string Outcome, long Value)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Meter.Name == MfaEnrollmentMetrics.MeterName && instrument.Name == "authentication.mfa.enrollments")
            {
                current.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            string? method = null;
            string? outcome = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "method") { method = tag.Value?.ToString(); }
                else if (tag.Key == "outcome") { outcome = tag.Value?.ToString(); }
            }
            measurements.Add((method ?? "?", outcome ?? "?", value));
        });
        listener.Start();

        MfaEnrollmentMetrics.RecordEnrollment(MfaMethod.Totp, "enrolled");
        MfaEnrollmentMetrics.RecordEnrollment(MfaMethod.Sms, "verify-failed");
        MfaEnrollmentMetrics.RecordEnrollment(MfaMethod.Push, "device-registered");

        measurements.Should().Contain(m => m.Method == "Totp" && m.Outcome == "enrolled" && m.Value == 1);
        measurements.Should().Contain(m => m.Method == "Sms" && m.Outcome == "verify-failed" && m.Value == 1);
        measurements.Should().Contain(m => m.Method == "Push" && m.Outcome == "device-registered" && m.Value == 1);
    }

    [Fact]
    public void VerificationCounter_RecordsOutcomeDimensions()
    {
        var measurements = new List<(string Method, string Outcome, long Value)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Meter.Name == MfaEnrollmentMetrics.MeterName && instrument.Name == "authentication.mfa.verifications")
            {
                current.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            string? method = null;
            string? outcome = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "method") { method = tag.Value?.ToString(); }
                else if (tag.Key == "outcome") { outcome = tag.Value?.ToString(); }
            }
            measurements.Add((method ?? "?", outcome ?? "?", value));
        });
        listener.Start();

        MfaEnrollmentMetrics.RecordVerification(MfaMethod.Push, succeeded: true);
        MfaEnrollmentMetrics.RecordVerification(MfaMethod.Totp, succeeded: false);
        MfaEnrollmentMetrics.RecordVerification(MfaMethod.Sms, succeeded: false, outcome: "timeout");

        measurements.Should().Contain(m => m.Method == "Push" && m.Outcome == "succeeded" && m.Value == 1);
        measurements.Should().Contain(m => m.Method == "Totp" && m.Outcome == "failed" && m.Value == 1);
        measurements.Should().Contain(m => m.Method == "Sms" && m.Outcome == "timeout" && m.Value == 1);
    }

    [Fact]
    public void Recording_NeverThrows()
    {
        var act = () => MfaEnrollmentMetrics.RecordEnrollment(MfaMethod.Totp, "enrolled");
        act.Should().NotThrow();

        var actVerify = () => MfaEnrollmentMetrics.RecordVerification(MfaMethod.Push, succeeded: true);
        actVerify.Should().NotThrow();
    }
}
