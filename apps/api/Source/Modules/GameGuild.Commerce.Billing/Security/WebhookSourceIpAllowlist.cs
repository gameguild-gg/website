using System.Net;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Configured CIDR allowlist for webhook callback sources.
///     Empty list = allowlist disabled (all sources pass). Non-empty = fail closed:
///     only matching sources pass; null/unknown client addresses are rejected.
/// </summary>
public sealed class WebhookSourceIpAllowlist
{
    private readonly IReadOnlyList<WebhookIpNetwork> _networks;

    public WebhookSourceIpAllowlist(IOptions<BillingConfiguration> billingConfiguration)
    {
        ArgumentNullException.ThrowIfNull(billingConfiguration);

        var entries = billingConfiguration.Value.Webhook.Security.SourceIpAllowlist;
        var networks = new List<WebhookIpNetwork>(entries.Count);
        foreach (var entry in entries)
        {
            if (WebhookIpNetwork.TryParse(entry, out var network))
            {
                networks.Add(network);
            }
        }

        _networks = networks;
    }

    /// <summary>Whether any allowlist entry is configured.</summary>
    public bool IsEnabled => _networks.Count > 0;

    /// <summary>
    ///     Whether the client address passes the allowlist. Fail closed: with a configured
    ///     allowlist, a missing client address is rejected.
    /// </summary>
    public bool IsAllowed(IPAddress? remoteIpAddress)
    {
        if (!IsEnabled)
        {
            return true;
        }

        if (remoteIpAddress is null)
        {
            return false;
        }

        foreach (var network in _networks)
        {
            if (network.Contains(remoteIpAddress))
            {
                return true;
            }
        }

        return false;
    }
}
