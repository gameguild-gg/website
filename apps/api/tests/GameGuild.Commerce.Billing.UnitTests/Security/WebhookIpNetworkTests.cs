using System.Net;
using FluentAssertions;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Security;

public class WebhookIpNetworkTests
{
    [Theory]
    [InlineData("203.0.113.0/24", true)]
    [InlineData("203.0.113.7/32", true)]
    [InlineData("0.0.0.0/0", true)]
    [InlineData("2001:db8::/32", true)]
    [InlineData("::1/128", true)]
    [InlineData("203.0.113.0", false)]
    [InlineData("/24", false)]
    [InlineData("203.0.113.0/", false)]
    [InlineData("not-an-ip/24", false)]
    [InlineData("203.0.113.0/33", false)]
    [InlineData("2001:db8::/129", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void TryParse_Should_Validate_Cidr_Entries(string candidate, bool expected)
    {
        WebhookIpNetwork.TryParse(candidate, out var network).Should().Be(expected);

        if (!expected)
        {
            network.Should().Be(default(WebhookIpNetwork));
        }
    }

    [Theory]
    [InlineData("203.0.113.0/24", "203.0.113.42", true)]
    [InlineData("203.0.113.0/24", "203.0.114.42", false)]
    [InlineData("203.0.113.7/32", "203.0.113.7", true)]
    [InlineData("203.0.113.7/32", "203.0.113.8", false)]
    [InlineData("0.0.0.0/0", "198.51.100.9", true)]
    [InlineData("2001:db8::/32", "2001:db8:1::1", true)]
    [InlineData("2001:db8::/32", "2001:db9::1", false)]
    [InlineData("203.0.113.128/25", "203.0.113.200", true)]
    [InlineData("203.0.113.128/25", "203.0.113.100", false)]
    public void Contains_Should_Match_Addresses_Inside_The_Network(string cidr, string candidate, bool expected)
    {
        WebhookIpNetwork.TryParse(cidr, out var network).Should().BeTrue(
            "the test data must be valid CIDR entries");

        network.Contains(IPAddress.Parse(candidate)).Should().Be(expected);
    }

    [Fact]
    public void Contains_Should_Match_Ipv4_Mapped_Ipv6_Forms()
    {
        WebhookIpNetwork.TryParse("203.0.113.0/24", out var network).Should().BeTrue();

        // ::ffff:203.0.113.9 is the IPv4-mapped IPv6 form of 203.0.113.9.
        network.Contains(IPAddress.Parse("::ffff:203.0.113.9")).Should().BeTrue();
    }

    [Fact]
    public void Contains_Should_Reject_Cross_Family_Addresses()
    {
        WebhookIpNetwork.TryParse("203.0.113.0/24", out var network).Should().BeTrue();

        network.Contains(IPAddress.Parse("2001:db8::1")).Should().BeFalse();
    }
}
