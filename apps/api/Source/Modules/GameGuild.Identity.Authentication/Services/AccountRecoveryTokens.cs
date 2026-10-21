using System.Security.Cryptography;
using System.Text;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Shared token utilities for delegated account recovery (issue #209): opaque
///     generation, SHA-256 hex digests and constant-time comparison. Mirrors
///     <see cref="MfaRecoveryTokens"/> so every recovery secret is stored as a digest only.
/// </summary>
internal static class AccountRecoveryTokens
{
    internal const int InviteTokenBytes = 32;
    internal const int ApprovalCodeBytes = 32;
    internal const int ResetTokenBytes = 32;

    internal static string Generate(int byteCount) =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(byteCount)).ToLowerInvariant();

    internal static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    /// <summary>Constant-time comparison of two hex digests (see BackupCodeMfaService).</summary>
    internal static bool FixedTimeHexEquals(string left, string right)
    {
        if (left is not { Length: 64 } || right is not { Length: 64 })
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(left), Convert.FromHexString(right));
    }
}
