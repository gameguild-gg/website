using System.Security.Cryptography;
using System.Text;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Service for securely hashing refresh tokens before database storage.
///     Uses SHA-256 to create a one-way hash of the token.
/// </summary>
/// <remarks>
///     <para>
///         Refresh tokens are sensitive credentials that should never be stored in plaintext.
///         This service provides a consistent way to hash tokens before storage and verify
///         tokens during validation.
///     </para>
///     <para>
///         Unlike passwords, refresh tokens don't need slow hashing algorithms like BCrypt
///         because they are already high-entropy random strings. SHA-256 is sufficient.
///     </para>
/// </remarks>
public interface IRefreshTokenHasher
{
    /// <summary>
    ///     Hashes a refresh token for secure storage.
    /// </summary>
    /// <param name="token">The plaintext refresh token</param>
    /// <returns>The hashed token suitable for database storage</returns>
    string HashToken(string token);

    /// <summary>
    ///     Verifies a plaintext token against a stored hash.
    /// </summary>
    /// <param name="token">The plaintext refresh token to verify</param>
    /// <param name="hashedToken">The stored hash from the database</param>
    /// <returns>True if the token matches the hash</returns>
    bool VerifyToken(string token, string hashedToken);
}

/// <summary>
///     SHA-256 based implementation of refresh token hashing.
/// </summary>
public sealed class RefreshTokenHasher : IRefreshTokenHasher
{
    /// <summary>
    ///     Hashes a refresh token using SHA-256.
    /// </summary>
    public string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token cannot be empty", nameof(token));
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    ///     Verifies a plaintext token against a stored SHA-256 hash.
    /// </summary>
    public bool VerifyToken(string token, string hashedToken)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(hashedToken))
        {
            return false;
        }

        var computedHash = HashToken(token);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHash),
            Encoding.UTF8.GetBytes(hashedToken));
    }
}
