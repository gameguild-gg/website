
namespace GameGuild.Identity.Authentication;

/// <summary>
///     Service for secure password hashing and verification.
///     Supports legacy BCrypt and versioned full-length PBKDF2 hashes, with upgrade detection.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    ///     Hashes a password with BCrypt or full-length PBKDF2 according to its UTF-8 byte length.
    /// </summary>
    /// <param name="password">The plain text password</param>
    /// <returns>Hashed password with algorithm identifier</returns>
    string HashPassword(string password);

    /// <summary>
    ///     Verifies a password against a stored hash.
    ///     Automatically handles multiple hash formats for backward compatibility.
    /// </summary>
    /// <param name="hashedPassword">The stored password hash</param>
    /// <param name="providedPassword">The password to verify</param>
    /// <returns>True if password matches</returns>
    bool VerifyPassword(string hashedPassword, string providedPassword);

    /// <summary>
    ///     Verifies a password against a stored hash and reports whether expensive
    ///     cryptographic verification actually ran. Early rejections — missing input, a
    ///     malformed stored hash, or an input beyond BCrypt's 72-byte boundary — return a
    ///     failure with <see cref="PasswordVerificationResult.PerformedCryptographicWork" />
    ///     set to false so callers can supply equivalent timing-compensation work instead of
    ///     misclassifying the short path as completed credential work.
    /// </summary>
    /// <param name="hashedPassword">The stored password hash</param>
    /// <param name="providedPassword">The password to verify</param>
    /// <returns>Verification outcome together with the credential-work classification</returns>
    PasswordVerificationResult VerifyPasswordWithWorkClassification(string hashedPassword, string providedPassword);

    /// <summary>
    ///     Performs verification-equivalent cryptographic work at the configured BCrypt work
    ///     factor. Used by timing protection to equalize paths that completed no usable
    ///     credential verification (missing, passwordless or unusable credentials).
    /// </summary>
    Task PerformDummyVerificationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if a hash is malformed, unsupported or below the configured BCrypt work factor.
    /// </summary>
    /// <param name="hashedPassword">The stored password hash</param>
    /// <returns>True if hash should be upgraded</returns>
    bool NeedsUpgrade(string hashedPassword);

    /// <summary>
    ///     Validates password strength against security requirements.
    /// </summary>
    /// <param name="password">The password to validate</param>
    /// <returns>Validation result with specific requirement failures</returns>
    PasswordStrengthResult ValidatePasswordStrength(string password);
}
