using System.Globalization;
using System.Text.Json;
using Nethereum.Util;

namespace GameGuild.Identity.Authentication;

/// <summary>Protocol-v1 ABI and strict Ethereum DATA/QUANTITY decoding.</summary>
internal static class EvmCertificateProtocol
{
    // Selectors match the Solidity0.8.37 compiler artifact for the reviewed registry.
    internal const string ProtocolVersionCall = "0xaa3aa460";
    internal const string IssuerIdCall = "0x4f572d75";
    internal const string GetAnchorCall = "0x7feb51d9";
    internal const string AnchorCall = "0xeecdf927";
    internal const string RevokeCall = "0xb75c7dc6";
    internal const string ZeroAddress = "0x0000000000000000000000000000000000000000";

    internal static string AnchorEventTopic { get; } = "0x" + Sha3Keccack.Current.CalculateHash("CertificateAnchored(bytes32,bytes32,address,uint256)");
    internal static string RevokeEventTopic { get; } = "0x" + Sha3Keccack.Current.CalculateHash("CertificateRevoked(bytes32,bytes32,address,uint256)");

    internal static string NormalizeHashArgument(string value, string argumentName)
    {
        var data = value ?? string.Empty;
        if (!data.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            data = "0x" + data;
        }
        if (!TryData(data, 32, out var normalized) || normalized.AsSpan(2).IndexOfAnyExcept('0') < 0)
        {
            throw new ArgumentException("A nonzero 32-byte hexadecimal digest is required.", argumentName);
        }

        return normalized;
    }

    internal static string Data(JsonElement element, int? byteCount = null)
    {
        if (element.ValueKind != JsonValueKind.String || !TryData(element.GetString(), byteCount, out var data))
        {
            throw Malformed();
        }

        return data;
    }

    internal static string PropertyData(JsonElement value, string name, int byteCount)
    {
        RequireObject(value);
        if (!value.TryGetProperty(name, out var property))
        {
            throw Malformed();
        }
        return Data(property, byteCount);
    }

    internal static ulong Quantity(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw Malformed();
        }
        var value = element.GetString();
        if (value is null || value.Length < 3 || !value.StartsWith("0x", StringComparison.Ordinal) ||
            (value.Length > 3 && value[2] == '0') ||
            !ulong.TryParse(value.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var result))
        {
            throw Malformed();
        }

        return result;
    }

    internal static ulong PropertyQuantity(JsonElement value, string name)
    {
        RequireObject(value);
        if (!value.TryGetProperty(name, out var property))
        {
            throw Malformed();
        }
        return Quantity(property);
    }

    internal static string EncodeQuantity(ulong value) => "0x" + value.ToString("x", CultureInfo.InvariantCulture);

    internal static EvmCertificateBlockReference Block(JsonElement value) =>
        new(PropertyQuantity(value, "number"), PropertyData(value, "hash", 32));

    internal static ulong UInt64Word(string word)
    {
        if (word.Length != 64 || word.AsSpan(0, 48).IndexOfAnyExcept('0') >= 0 ||
            !ulong.TryParse(word.AsSpan(48), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            throw Malformed();
        }

        return value;
    }

    internal static string AddressWord(string word)
    {
        if (word.Length != 64 || word.AsSpan(0, 24).IndexOfAnyExcept('0') >= 0 || !word.All(Uri.IsHexDigit))
        {
            throw Malformed();
        }
        return "0x" + word[24..].ToLowerInvariant();
    }

    internal static EvmCertificateRegistryState State(JsonElement value)
    {
        var data = Data(value, 128)[2..];
        return new(AddressWord(data[..64]), AddressWord(data[64..128]), UInt64Word(data[128..192]), UInt64Word(data[192..256]));
    }

    internal static void RequireObject(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw Malformed();
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                throw Malformed();
            }
        }
    }

    private static bool TryData(string? value, int? byteCount, out string normalized)
    {
        normalized = string.Empty;
        if (value is null || value.Length < 2 || !value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            value.Length % 2 != 0 || (byteCount.HasValue && value.Length != byteCount.Value * 2 + 2) ||
            !value.Skip(2).All(Uri.IsHexDigit))
        {
            return false;
        }
        normalized = value.ToLowerInvariant();
        return true;
    }

    internal static EvmCertificateEvidenceException Malformed() => new(EvmCertificateEvidenceFailure.MalformedRpcData);
}
