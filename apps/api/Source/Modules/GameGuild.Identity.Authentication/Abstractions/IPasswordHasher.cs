
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

    /// <summary>Opaque providers remain conservative; work-aware implementations override this operation.</summary>
    PasswordVerificationResult VerifyPasswordWithWorkClassification(string hashedPassword, string providedPassword) =>
        new PasswordVerificationResult(VerifyPassword(hashedPassword, providedPassword), false);

    /// <summary>Native providers perform verification-equivalent work at their configured factor.</summary>
    Task PerformDummyVerificationAsync() => PerformDummyVerificationAsync(CancellationToken.None);

    /// <summary>Native providers perform cancellable verification-equivalent work at their configured factor.</summary>
    Task PerformDummyVerificationAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("This password provider does not expose dummy credential work.");

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
