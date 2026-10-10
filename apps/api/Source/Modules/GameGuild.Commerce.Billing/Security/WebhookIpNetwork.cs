using System.Net;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     A parsed CIDR network (IPv4 or IPv6) used by the webhook source allowlist.
/// </summary>
public readonly record struct WebhookIpNetwork
{
    public IPAddress Address { get; }
    public int PrefixLength { get; }

    private WebhookIpNetwork(IPAddress address, int prefixLength)
    {
        Address = address;
        PrefixLength = prefixLength;
    }

    /// <summary>
    ///     Parses a CIDR string ("203.0.113.0/24", "2001:db8::/32") into a network.
    /// </summary>
    public static bool TryParse(string? cidr, out WebhookIpNetwork network)
    {
        network = default;

        if (string.IsNullOrWhiteSpace(cidr))
        {
            return false;
        }

        var separator = cidr.LastIndexOf('/');
        if (separator <= 0 || separator == cidr.Length - 1)
        {
            return false;
        }

        var addressPart = cidr[..separator].Trim();
        var prefixPart = cidr[(separator + 1)..].Trim();

        if (!IPAddress.TryParse(addressPart, out var address) ||
            !int.TryParse(prefixPart, out var prefixLength))
        {
            return false;
        }

        var normalized = UnmapIpv4(address);
        var byteCount = normalized.GetAddressBytes().Length;
        var maxPrefix = byteCount * 8;
        if (prefixLength < 0 || prefixLength > maxPrefix)
        {
            return false;
        }

        network = new WebhookIpNetwork(normalized, prefixLength);
        return true;
    }

    /// <summary>
    ///     Whether <paramref name="candidate"/> falls inside this network. IPv4-mapped IPv6
    ///     addresses are normalized so an allowlist entry in either family matches both forms.
    /// </summary>
    public bool Contains(IPAddress candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var networkBytes = Address.GetAddressBytes();
        var candidateBytes = UnmapIpv4(candidate).GetAddressBytes();

        if (networkBytes.Length != candidateBytes.Length)
        {
            return false;
        }

        var fullBytes = PrefixLength / 8;
        for (var i = 0; i < fullBytes; i++)
        {
            if (networkBytes[i] != candidateBytes[i])
            {
                return false;
            }
        }

        var remainderBits = PrefixLength % 8;
        if (remainderBits == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainderBits));
        return (networkBytes[fullBytes] & mask) == (candidateBytes[fullBytes] & mask);
    }

    private static IPAddress UnmapIpv4(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            var bytes = address.GetAddressBytes();
            return new IPAddress(bytes[^4..]);
        }

        return address;
    }
}
