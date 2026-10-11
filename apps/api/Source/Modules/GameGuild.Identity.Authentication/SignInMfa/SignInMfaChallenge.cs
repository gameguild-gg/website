namespace GameGuild.Identity.Authentication;

public enum SignInMfaPurpose { VerifyFactor = 1, EnrollFactor = 2 }
public enum SignInFirstFactor { Password = 1, Federated = 2, Wallet = 3, MagicLink = 4, Passkey = 5, EmailCode = 6 }

/// <summary>Persisted first-factor evidence. No password, MFA code or raw bearer is retained.</summary>
public sealed class SignInMfaChallenge
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
    public Guid TenantId { get; set; }
    public int SubjectTokenVersion { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string PolicyFingerprint { get; set; } = string.Empty;
    public SignInMfaPurpose Purpose { get; set; }
    public SignInFirstFactor FirstFactor { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public MfaMethod? VerificationMethod { get; set; }
    public Guid? EnrollmentConfigurationId { get; set; }
    public string? EnrollmentSecretFingerprint { get; set; }
    public DateTimeOffset? EnrollmentInitializedAt { get; set; }

    public void ValidateForPersistence()
    {
        if (Id == Guid.Empty || SubjectId == Guid.Empty || TenantId == Guid.Empty || SubjectTokenVersion < 1 ||
            !SignInMfaChallengeToken.IsDigest(TokenHash) || !SignInMfaChallengeToken.IsDigest(PolicyFingerprint) ||
            !Enum.IsDefined(Purpose) || !Enum.IsDefined(FirstFactor) || CreatedAt == default ||
            CreatedAt.Offset != TimeSpan.Zero || ExpiresAt.Offset != TimeSpan.Zero ||
            ExpiresAt <= CreatedAt || ExpiresAt - CreatedAt > TimeSpan.FromMinutes(5) ||
            ConsumedAt is not null || RevokedAt is not null || VerificationMethod is not null ||
            EnrollmentConfigurationId is not null || EnrollmentSecretFingerprint is not null || EnrollmentInitializedAt is not null)
        {
            throw new ArgumentException("Invalid first-factor challenge state.");
        }
    }
}

/// <summary>The completion flow derives this binding from current server-owned subject and policy state.</summary>
public sealed record SignInMfaChallengeBinding(
    Guid SubjectId, Guid TenantId, int SubjectTokenVersion, string PolicyFingerprint, SignInMfaPurpose Purpose)
{
    public void Validate()
    {
        if (SubjectId == Guid.Empty || TenantId == Guid.Empty || SubjectTokenVersion < 1 ||
            !SignInMfaChallengeToken.IsDigest(PolicyFingerprint) || !Enum.IsDefined(Purpose))
        {
            throw new ArgumentException("Invalid current subject binding.");
        }
    }
}
