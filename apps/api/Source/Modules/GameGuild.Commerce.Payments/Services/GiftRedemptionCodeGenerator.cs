using System.Security.Cryptography;
using System.Text;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Secure gift redemption code generator (issue #410 v1): 32-character URL-safe
///     codes from 24 random bytes (192 bits of entropy). Only the SHA-256 hash is stored;
/// <see cref="HashCode" /> lets lookups by code run without ever persisting the secret.
/// </summary>
public static class GiftRedemptionCodeGenerator
{
    private const int RandomByteCount = 24;

    /// <summary>Generates a new URL-safe redemption code.</summary>
    public static string GenerateCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(RandomByteCount);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>SHA-256 hash of a redemption code (hex, 64 characters).</summary>
    public static string HashCode(string code)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(hash);
    }

    /// <summary>Non-secret support prefix (first characters of the code).</summary>
    public static string PrefixOf(string code)
        => code.Length <= 6 ? code : code[..6];
}
