using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Security;

public class WebhookSourceIpAllowlistTests
{
    [Fact]
    public void Allowlist_Is_Disabled_By_Default_And_Allows_Everything()
    {
        var allowlist = CreateAllowlist([]);

        allowlist.IsEnabled.Should().BeFalse();
        allowlist.IsAllowed(IPAddress.Parse("203.0.113.9")).Should().BeTrue();
        allowlist.IsAllowed(null).Should().BeTrue();
    }

    [Fact]
    public void Configured_Allowlist_Fails_Closed_For_Unmatched_Sources()
    {
        var allowlist = CreateAllowlist(["203.0.113.0/24", "2001:db8::/32"]);

        allowlist.IsEnabled.Should().BeTrue();
        allowlist.IsAllowed(IPAddress.Parse("203.0.113.9")).Should().BeTrue();
        allowlist.IsAllowed(IPAddress.Parse("2001:db8::4")).Should().BeTrue();
        allowlist.IsAllowed(IPAddress.Parse("198.51.100.1")).Should().BeFalse();
    }

    [Fact]
    public void Configured_Allowlist_Rejects_Unknown_Client_Addresses()
    {
        var allowlist = CreateAllowlist(["203.0.113.0/24"]);

        allowlist.IsAllowed(null).Should().BeFalse("a configured allowlist must fail closed");
    }

    [Fact]
    public void Invalid_Entries_Are_Ignored_At_Construction()
    {
        // Startup validation rejects invalid entries; the allowlist still never matches
        // on garbage input if validation is bypassed (e.g. unit-test configurations).
        var allowlist = CreateAllowlist(["not-a-cidr"]);

        allowlist.IsEnabled.Should().BeFalse();
        allowlist.IsAllowed(IPAddress.Parse("203.0.113.9")).Should().BeTrue();
    }

    private static WebhookSourceIpAllowlist CreateAllowlist(IEnumerable<string> entries) =>
        new(Options.Create(new BillingConfiguration
        {
            Webhook = new WebhookSettings
            {
                Security = new WebhookSecuritySettings
                {
                    SourceIpAllowlist = entries.ToList()
                }
            }
        }));
}
