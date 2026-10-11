using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Password hashing service using BCrypt, with versioned PBKDF2 for inputs beyond BCrypt's byte limit.
///     Provides password hashing, verification, strength validation, and rehashing detection.
/// </summary>
public sealed class PasswordHasher(ILogger<PasswordHasher> logger, IConfiguration configuration) : IPasswordHasher, IPasswordVerificationWork
{
    private static readonly Regex BcryptHashPattern = new(
        @"\A\$2[abxy]?\$(0[4-9]|1[0-6])\$[./A-Za-z0-9]{53}\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private int GetBCryptWorkFactor() => ResolveBCryptWorkFactor(configuration);

    // Resolve on every operation, including dummy work, so configuration reloads cannot drift.
    internal static int ResolveBCryptWorkFactor(IConfiguration? configuration)
    {
        var workFactor = configuration?.GetValue<int?>("PresentationLayer:Authentication:PasswordPolicy:BCryptWorkFactor")
            ?? configuration?.GetValue<int?>("Authentication:PasswordPolicy:BCryptWorkFactor")
            ?? configuration?.GetValue<int?>("PasswordPolicy:BCryptWorkFactor")
            ?? 12;
        if (workFactor is < 10 or > 16)
        {
            throw new InvalidOperationException("BCrypt work factor must be between 10 and 16.");
        }
        return workFactor;
    }

    // Prefer the shared presentation options; retain both prior locations for configuration compatibility.
    private int MinPasswordLength => GetPolicyInteger("MinPasswordLength", 8);

    private int MaxPasswordLength => GetPolicyInteger("MaxPasswordLength", 128);

    private bool RequireUppercase => GetPolicyBoolean("RequireUppercase", true);

    private bool RequireLowercase => GetPolicyBoolean("RequireLowercase", true);

    private bool RequireDigit => GetPolicyBoolean("RequireDigit", true);

    private bool RequireSpecialChar => GetPolicyBoolean("RequireSpecialChar", true);

    private int GetPolicyInteger(string name, int defaultValue) =>
        configuration.GetValue<int?>($"PresentationLayer:Authentication:PasswordPolicy:{name}") ??
        configuration.GetValue<int?>($"Authentication:PasswordPolicy:{name}") ??
        configuration.GetValue($"PasswordPolicy:{name}", defaultValue);

    private bool GetPolicyBoolean(string name, bool defaultValue) =>
        configuration.GetValue<bool?>($"PresentationLayer:Authentication:PasswordPolicy:{name}") ??
        configuration.GetValue<bool?>($"Authentication:PasswordPolicy:{name}") ??
        configuration.GetValue($"PasswordPolicy:{name}", defaultValue);

    /// <summary>
    ///     Hashes a password using BCrypt algorithm.
    /// </summary>
    public Task<string> HashPasswordAsync(string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = HashPassword(password);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(result);
    }

    /// <summary>
    ///     Verifies a password against its hash.
    /// </summary>
    public Task<bool> VerifyPasswordAsync(string passwordHash, string providedPassword, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = VerifyPassword(passwordHash, providedPassword);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(result);
    }

    internal const string DummyCredentialMaterial = "dummy";

    /// <summary>Uses the same reload-aware work factor as real BCrypt verification.</summary>
    public Task PerformDummyVerificationAsync() => PerformDummyVerificationAsync(CancellationToken.None);

    /// <summary>Uses the same reload-aware work factor as real BCrypt verification, with caller cancellation.</summary>
    public async Task PerformDummyVerificationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var workFactor = GetBCryptWorkFactor();
        await Task.Run(() => BCrypt.Net.BCrypt.HashPassword(DummyCredentialMaterial, workFactor), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogDebug("Dummy credential work completed (BCrypt work factor: {WorkFactor})", workFactor);
    }

    /// <summary>
    ///     Validates password strength against policy requirements.
    /// </summary>
    public Task<PasswordStrengthResult> ValidatePasswordStrengthAsync(string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = ValidatePasswordStrength(password);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(result);
    }

    /// <summary>
    ///     Checks if a password hash needs to be rehashed (e.g., due to increased work factor).
    /// </summary>
    public Task<bool> NeedsRehashAsync(string passwordHash, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = NeedsUpgrade(passwordHash);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(result);
    }

    #region Private Helper Methods

    /// <summary>
    ///     Calculates password strength score (0-100).
    /// </summary>
    private int CalculatePasswordStrength(string password)
    {
        var score = 0;

        // Length score (max 25 points)
        score += Math.Min(password.Length * 2, 25);

        // Character variety score (max 40 points)
        if (Regex.IsMatch(password, @"[a-z]"))
        {
            score += 10;
        }

        if (Regex.IsMatch(password, @"[A-Z]"))
        {
            score += 10;
        }

        if (Regex.IsMatch(password, @"[0-9]"))
        {
            score += 10;
        }

        if (Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>/?]"))
        {
            score += 10;
        }

        // Complexity bonus (max 35 points)
        var uniqueChars = password.Distinct().Count();
        score += Math.Min(uniqueChars * 2, 20);

        // Entropy bonus
        if (password.Length >= 12 && uniqueChars >= 10) { score += 15; }

        // Penalty for repeated characters
        if (Regex.IsMatch(password, @"(.)\1{2,}")) { score -= 10; }

        // Penalty for sequential characters
        if (ContainsSequentialCharacters(password)) { score -= 10; }

        return Math.Max(0, Math.Min(100, score));
    }

    /// <summary>
    ///     Checks if password contains sequential characters (abc, 123, etc.).
    /// </summary>
    private bool ContainsSequentialCharacters(string password)
    {
        for (var i = 0; i < password.Length - 2; i++)
        {
            var char1 = password[i];
            var char2 = password[i + 1];
            var char3 = password[i + 2];

            // Check ascending sequence
            if (char2 == char1 + 1 && char3 == char2 + 1) { return true; }

            // Check descending sequence
            if (char2 == char1 - 1 && char3 == char2 - 1) { return true; }
        }

        return false;
    }

    #endregion

    #region Interface Implementation (Synchronous Methods)

    /// <summary>
    ///     Hashes a password using BCrypt algorithm.
    /// </summary>
    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password)) { throw new ArgumentException("Password cannot be empty", nameof(password)); }

        var workFactor = GetBCryptWorkFactor();
        if (Encoding.UTF8.GetByteCount(password) > 72)
        {
            return LongPasswordHash.Create(password);
        }

        logger.LogDebug("Hashing password with BCrypt (work factor: {WorkFactor})", workFactor);
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor);
        logger.LogDebug("Password hashed successfully");
        return passwordHash;
    }

    /// <summary>
    ///     Verifies a password against its hash.
    /// </summary>
    public bool VerifyPassword(string hashedPassword, string providedPassword) =>
        VerifyPasswordWithWork(hashedPassword, providedPassword).IsValid;

    public PasswordVerificationResult VerifyPasswordWithWorkClassification(string hashedPassword, string providedPassword) =>
        VerifyPasswordWithWork(hashedPassword, providedPassword);

    /// <summary>Reports only credential work that actually completed, never merely account existence.</summary>
    public PasswordVerificationResult VerifyPasswordWithWork(string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword) || string.IsNullOrWhiteSpace(providedPassword))
        {
            return default;
        }

        if (hashedPassword.StartsWith(LongPasswordHash.Prefix, StringComparison.Ordinal))
        {
            return LongPasswordHash.VerifyWithWork(hashedPassword, providedPassword);
        }

        // Legacy BCrypt cannot establish bytes after 72. Reject before costly work, then compensate upstream.
        if (Encoding.UTF8.GetByteCount(providedPassword) > 72 || !BcryptHashPattern.IsMatch(hashedPassword))
        {
            return default;
        }

        try
        {
            logger.LogDebug("Verifying password");
            var isValid = BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword);
            logger.LogDebug("Password verification result: {IsValid}", isValid);
            return new PasswordVerificationResult(isValid, true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error verifying password");
            return default;
        }
    }

    /// <summary>History conservatively rejects every long input matching a truncated legacy hash.</summary>
    internal static bool MatchesLongLegacyHashForHistory(string hashedPassword, string providedPassword)
    {
        if (Encoding.UTF8.GetByteCount(providedPassword) <= 72 || !BcryptHashPattern.IsMatch(hashedPassword))
        {
            return false;
        }
        try
        {
            return BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    ///     Checks if a password hash needs rehashing (e.g., due to increased work factor).
    /// </summary>
    public bool NeedsUpgrade(string hashedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword)) { return false; }

        if (hashedPassword.StartsWith(LongPasswordHash.Prefix, StringComparison.Ordinal))
        {
            return !LongPasswordHash.IsValid(hashedPassword);
        }

        var match = BcryptHashPattern.Match(hashedPassword);
        if (!match.Success)
        {
            logger.LogWarning("Invalid or unsupported BCrypt hash format");
            return true;
        }

        var currentWorkFactor = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var workFactor = GetBCryptWorkFactor();

        var needsRehash = currentWorkFactor < workFactor;

        if (needsRehash) { logger.LogInformation("Password hash needs rehashing: Current work factor {Current}, Required {Required}", currentWorkFactor, workFactor); }

        return needsRehash;
    }

    /// <summary>
    ///     Validates password strength against policy requirements.
    /// </summary>
    public PasswordStrengthResult ValidatePasswordStrength(string password)
    {
        var result = new PasswordStrengthResult { IsValid = true, ValidationFailures = new List<string>() };

        if (string.IsNullOrWhiteSpace(password))
        {
            result.IsValid = false;
            result.ValidationFailures.Add("Password is required");
            return result;
        }

        if (password.Length < MinPasswordLength)
        {
            result.IsValid = false;
            result.ValidationFailures.Add($"Password must be at least {MinPasswordLength} characters long");
        }

        if (password.Length > MaxPasswordLength)
        {
            result.IsValid = false;
            result.ValidationFailures.Add($"Password must not exceed {MaxPasswordLength} characters");
        }

        if (RequireUppercase && !Regex.IsMatch(password, @"[A-Z]"))
        {
            result.IsValid = false;
            result.ValidationFailures.Add("Password must contain at least one uppercase letter");
        }

        if (RequireLowercase && !Regex.IsMatch(password, @"[a-z]"))
        {
            result.IsValid = false;
            result.ValidationFailures.Add("Password must contain at least one lowercase letter");
        }

        if (RequireDigit && !Regex.IsMatch(password, @"[0-9]"))
        {
            result.IsValid = false;
            result.ValidationFailures.Add("Password must contain at least one digit");
        }

        if (RequireSpecialChar && !Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>/?]"))
        {
            result.IsValid = false;
            result.ValidationFailures.Add("Password must contain at least one special character");
        }

        var commonPasswords = new[] { "password", "12345678", "qwerty", "abc123", "password1", "Password1", "Password123", "Welcome1", "Admin123" };

        if (commonPasswords.Contains(password, StringComparer.OrdinalIgnoreCase))
        {
            result.IsValid = false;
            result.ValidationFailures.Add("Password is too common and easily guessable");
        }

        var strengthScore = CalculatePasswordStrength(password);
        var strengthLevel = strengthScore switch
        {
            >= 80 => "Strong",
            >= 60 => "Good",
            >= 40 => "Fair",
            >= 20 => "Weak",
            _ => "Very Weak"
        };

        result.StrengthScore = strengthScore;
        result.StrengthLevel = strengthLevel;

        logger.LogDebug("Password strength validation completed");

        return result;
    }

    #endregion
}
