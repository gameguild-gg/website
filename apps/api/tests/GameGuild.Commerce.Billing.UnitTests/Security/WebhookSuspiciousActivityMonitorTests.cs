using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Security;

public class WebhookSuspiciousActivityMonitorTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Monitor_Fails_Open_When_Disabled()
    {
        var monitor = CreateMonitor(enabled: false);

        for (var i = 0; i < 50; i++)
        {
            monitor.RegisterFailure("203.0.113.9", Now.AddSeconds(i)).Should().BeFalse();
        }

        monitor.IsBlocked("203.0.113.9", Now.AddSeconds(60)).Should().BeFalse();
        monitor.GetBlockedSources(Now.AddSeconds(60)).Should().BeEmpty();
    }

    [Fact]
    public void RegisterFailure_Blocks_Source_When_Threshold_Is_Crossed()
    {
        var monitor = CreateMonitor(enabled: true, failureThreshold: 3, windowSeconds: 60, blockDurationSeconds: 120);

        monitor.RegisterFailure("198.51.100.7", Now).Should().BeFalse();
        monitor.RegisterFailure("198.51.100.7", Now.AddSeconds(1)).Should().BeFalse();
        monitor.RegisterFailure("198.51.100.7", Now.AddSeconds(2)).Should().BeTrue("the third failure crosses the threshold");

        monitor.IsBlocked("198.51.100.7", Now.AddSeconds(3)).Should().BeTrue();

        var blocked = monitor.GetBlockedSources(Now.AddSeconds(3));
        blocked.Should().HaveCount(1);
        blocked[0].SourceKey.Should().Be("198.51.100.7");
        blocked[0].FailureCount.Should().Be(3);
        blocked[0].BlockedUntilUtc.Should().Be(Now.AddSeconds(2 + 120));
    }

    [Fact]
    public void Block_Expires_After_The_Configured_Duration()
    {
        var monitor = CreateMonitor(enabled: true, failureThreshold: 1, windowSeconds: 60, blockDurationSeconds: 30);

        monitor.RegisterFailure("198.51.100.7", Now).Should().BeTrue();
        monitor.IsBlocked("198.51.100.7", Now.AddSeconds(31)).Should().BeFalse();
        monitor.GetBlockedSources(Now.AddSeconds(31)).Should().BeEmpty();
    }

    [Fact]
    public void Failures_Outside_The_Sliding_Window_Do_Not_Count()
    {
        var monitor = CreateMonitor(enabled: true, failureThreshold: 2, windowSeconds: 10, blockDurationSeconds: 30);

        monitor.RegisterFailure("198.51.100.7", Now).Should().BeFalse();
        monitor.RegisterFailure("198.51.100.7", Now.AddSeconds(11)).Should().BeFalse("the first failure left the window");
        monitor.IsBlocked("198.51.100.7", Now.AddSeconds(12)).Should().BeFalse();
    }

    [Fact]
    public void Prune_Removes_Expired_Blocks_And_Stale_Windows()
    {
        var monitor = CreateMonitor(enabled: true, failureThreshold: 1, windowSeconds: 10, blockDurationSeconds: 30);

        monitor.RegisterFailure("198.51.100.7", Now).Should().BeTrue();
        monitor.RegisterFailure("203.0.113.9", Now).Should().BeTrue();
        monitor.Prune(Now.AddSeconds(31));

        monitor.IsBlocked("198.51.100.7", Now.AddSeconds(32)).Should().BeFalse();
        monitor.GetBlockedSources(Now.AddSeconds(32)).Should().BeEmpty();
    }

    [Fact]
    public void Blocked_Sources_Are_Isolated_Per_Source_Key()
    {
        var monitor = CreateMonitor(enabled: true, failureThreshold: 1, windowSeconds: 60, blockDurationSeconds: 60);

        monitor.RegisterFailure("198.51.100.7", Now).Should().BeTrue();
        monitor.IsBlocked("203.0.113.9", Now).Should().BeFalse();
    }

    private static WebhookSuspiciousActivityMonitor CreateMonitor(
        bool enabled,
        int failureThreshold = 20,
        int windowSeconds = 300,
        int blockDurationSeconds = 900) =>
        new(
            Options.Create(new BillingConfiguration
            {
                Webhook = new WebhookSettings
                {
                    Security = new WebhookSecuritySettings
                    {
                        SuspiciousActivity = new WebhookSuspiciousActivitySettings
                        {
                            Enabled = enabled,
                            FailureThreshold = failureThreshold,
                            WindowSeconds = windowSeconds,
                            BlockDurationSeconds = blockDurationSeconds
                        }
                    }
                }
            }),
            NullLogger<WebhookSuspiciousActivityMonitor>.Instance);
}
