using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using FluentAssertions;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Configuration;

/// <summary>
///     Startup validation for the webhook security hardening settings (CIDR allowlist,
///     suspicious-activity thresholds) and the Google Pay verification keys.
/// </summary>
public class WebhookSecuritySettingsValidationTests
{
    [Fact]
    public void Configuration_Rejects_Invalid_Cidr_Allowlist_Entries()
    {
        var failures = Validate(new BillingConfiguration
        {
            Webhook = new WebhookSettings
            {
                Security = new WebhookSecuritySettings { SourceIpAllowlist = ["not-a-cidr"] }
            }
        });

        failures.Should().Contain(f => f.ErrorMessage!.Contains("not-a-cidr"));
    }

    [Fact]
    public void Configuration_Accepts_Valid_Ipv4_And_Ipv6_Cidr_Allowlist_Entries()
    {
        var failures = Validate(new BillingConfiguration
        {
            Webhook = new WebhookSettings
            {
                Security = new WebhookSecuritySettings { SourceIpAllowlist = ["203.0.113.0/24", "2001:db8::/32"] }
            }
        });

        failures.Should().NotContain(f => f.ErrorMessage!.Contains("SourceIpAllowlist"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Configuration_Rejects_NonPositive_Suspicious_Activity_Thresholds_When_Enabled(int threshold)
    {
        var failures = Validate(new BillingConfiguration
        {
            Webhook = new WebhookSettings
            {
                Security = new WebhookSecuritySettings
                {
                    SuspiciousActivity = new WebhookSuspiciousActivitySettings
                    {
                        Enabled = true,
                        FailureThreshold = threshold
                    }
                }
            }
        });

        failures.Should().Contain(f => f.ErrorMessage!.Contains("FailureThreshold"));
    }

    [Fact]
    public void Configuration_Skips_Threshold_Validation_When_Suspicious_Activity_Is_Disabled()
    {
        var failures = Validate(new BillingConfiguration
        {
            Webhook = new WebhookSettings
            {
                Security = new WebhookSecuritySettings
                {
                    SuspiciousActivity = new WebhookSuspiciousActivitySettings
                    {
                        Enabled = false,
                        FailureThreshold = 0
                    }
                }
            }
        });

        failures.Should().NotContain(f => f.ErrorMessage!.Contains("FailureThreshold"));
    }

    [Fact]
    public void Configuration_Rejects_NonPositive_Block_Durations_When_Enabled()
    {
        var failures = Validate(new BillingConfiguration
        {
            Webhook = new WebhookSettings
            {
                Security = new WebhookSecuritySettings
                {
                    SuspiciousActivity = new WebhookSuspiciousActivitySettings
                    {
                        Enabled = true,
                        BlockDurationSeconds = 0
                    }
                }
            }
        });

        failures.Should().Contain(f => f.ErrorMessage!.Contains("BlockDurationSeconds"));
    }

    [Fact]
    public void Configuration_Rejects_Invalid_Google_Pay_Verification_Keys()
    {
        var failures = Validate(new BillingConfiguration
        {
            GooglePay = new GooglePaySettings
            {
                ProjectId = "gameguild-project",
                VerificationKeys = ["definitely-not-a-key"]
            }
        });

        failures.Should().Contain(f => f.ErrorMessage!.Contains("VerificationKeys"));
    }

    [Fact]
    public void Configuration_Accepts_A_Valid_Google_Pay_Verification_Key()
    {
        using var key = RSA.Create(2048);
        var failures = Validate(new BillingConfiguration
        {
            GooglePay = new GooglePaySettings
            {
                ProjectId = "gameguild-project",
                VerificationKeys = [key.ExportSubjectPublicKeyInfoPem()]
            }
        });

        failures.Should().NotContain(f => f.ErrorMessage!.Contains("VerificationKeys"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(901)]
    public void Configuration_Rejects_Out_Of_Range_Google_Pay_Timestamp_Tolerance(int tolerance)
    {
        var failures = Validate(new BillingConfiguration
        {
            GooglePay = new GooglePaySettings { TimestampToleranceSeconds = tolerance }
        });

        failures.Should().Contain(f => f.ErrorMessage!.Contains("TimestampToleranceSeconds"));
    }

    [Fact]
    public void Defaults_Are_Disabled_And_Fail_Safe()
    {
        var settings = new WebhookSecuritySettings();

        settings.SourceIpAllowlist.Should().BeEmpty("the allowlist must be disabled by default");
        settings.SuspiciousActivity.Enabled.Should().BeFalse("auto-blocking must be disabled by default (fail open)");
        settings.SuspiciousActivity.FailureThreshold.Should().BePositive();
        settings.SuspiciousActivity.WindowSeconds.Should().BePositive();
        settings.SuspiciousActivity.BlockDurationSeconds.Should().BePositive();
    }

    private static IEnumerable<ValidationResult> Validate(BillingConfiguration configuration) =>
        configuration.Validate(new ValidationContext(configuration));
}
