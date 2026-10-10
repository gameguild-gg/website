namespace GameGuild.Identity.Authentication;

/// <summary>Server-created evidence after verified proof and atomic challenge consumption; never request data.</summary>
public sealed class SignInMfaProof
{
    internal SignInMfaProof(Guid challengeId, Guid subjectId, Guid tenantId, int tokenVersion, string policyFingerprint,
        SignInFirstFactor firstFactor, DateTimeOffset firstFactorVerifiedAt, DateTimeOffset verifiedAt, MfaMethod method)
    {
        ChallengeId = challengeId;
        SubjectId = subjectId;
        TenantId = tenantId;
        TokenVersion = tokenVersion;
        PolicyFingerprint = policyFingerprint;
        FirstFactor = firstFactor;
        FirstFactorVerifiedAt = firstFactorVerifiedAt;
        VerifiedAt = verifiedAt;
        Method = method;
        if (challengeId == Guid.Empty || subjectId == Guid.Empty || tenantId == Guid.Empty || tokenVersion < 1 ||
            !SignInMfaChallengeToken.IsDigest(policyFingerprint) || !Enum.IsDefined(firstFactor) ||
            method is not (MfaMethod.Totp or MfaMethod.BackupCode or MfaMethod.WebAuthn) ||
            firstFactorVerifiedAt == default || firstFactorVerifiedAt.Offset != TimeSpan.Zero || verifiedAt.Offset != TimeSpan.Zero ||
            verifiedAt < firstFactorVerifiedAt || verifiedAt - firstFactorVerifiedAt > TimeSpan.FromMinutes(5))
        {
            throw new AuthenticationRequiredException("MFA verification evidence is invalid.");
        }
    }

    public Guid ChallengeId { get; }
    public Guid SubjectId { get; }
    public Guid TenantId { get; }
    public int TokenVersion { get; }
    public string PolicyFingerprint { get; }
    public SignInFirstFactor FirstFactor { get; }
    public DateTimeOffset FirstFactorVerifiedAt { get; }
    public DateTimeOffset VerifiedAt { get; }
    public MfaMethod Method { get; }

    internal static SignInMfaProof FromVerifiedChallenge(SignInMfaChallenge challenge, MfaMethod method, DateTimeOffset verifiedAt)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        var enrolledProof = challenge.Purpose == SignInMfaPurpose.EnrollFactor && method == MfaMethod.Totp &&
            challenge.EnrollmentConfigurationId is { } configurationId && configurationId != Guid.Empty &&
            SignInMfaChallengeToken.IsDigest(challenge.EnrollmentSecretFingerprint ?? string.Empty) &&
            challenge.EnrollmentInitializedAt is { } initialized && initialized >= challenge.CreatedAt && initialized <= verifiedAt;
        if (challenge.Purpose != SignInMfaPurpose.VerifyFactor && !enrolledProof || verifiedAt >= challenge.ExpiresAt)
        {
            throw new AuthenticationRequiredException("MFA verification evidence is invalid.");
        }
        return new(challenge.Id, challenge.SubjectId, challenge.TenantId, challenge.SubjectTokenVersion,
            challenge.PolicyFingerprint, challenge.FirstFactor, challenge.CreatedAt, verifiedAt, method);
    }

    internal void RequireBinding(Guid subjectId, Guid? tenantId, int tokenVersion, DateTimeOffset authenticatedAt, DateTimeOffset now)
    {
        if (SubjectId != subjectId || TenantId != tenantId || TokenVersion != tokenVersion ||
            authenticatedAt.Offset != TimeSpan.Zero || authenticatedAt.ToUnixTimeSeconds() != FirstFactorVerifiedAt.ToUnixTimeSeconds() ||
            VerifiedAt > now || FirstFactorVerifiedAt > now)
        {
            throw new AuthenticationRequiredException("MFA verification evidence is no longer valid.");
        }
    }
}
