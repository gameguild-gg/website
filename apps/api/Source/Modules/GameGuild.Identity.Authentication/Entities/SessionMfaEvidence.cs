namespace GameGuild.Identity.Authentication;

/// <summary>Durable session proof metadata, with no code, secret or raw challenge bearer.</summary>
public sealed class SessionMfaEvidence
{
    public Guid SessionId { get; set; }
    public Guid ChallengeId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid TenantId { get; set; }
    public int TokenVersion { get; set; }
    public string PolicyFingerprint { get; set; } = string.Empty;
    public SignInFirstFactor FirstFactor { get; set; }
    public DateTimeOffset FirstFactorVerifiedAt { get; set; }
    public DateTimeOffset VerifiedAt { get; set; }
    public MfaMethod Method { get; set; }

    internal SignInMfaProof ToProof() => new(ChallengeId, SubjectId, TenantId, TokenVersion, PolicyFingerprint,
        FirstFactor, FirstFactorVerifiedAt, VerifiedAt, Method);

    internal static SessionMfaEvidence Create(Guid sessionId, SignInMfaProof proof) => new()
    {
        SessionId = sessionId, ChallengeId = proof.ChallengeId, SubjectId = proof.SubjectId,
        TenantId = proof.TenantId, TokenVersion = proof.TokenVersion, PolicyFingerprint = proof.PolicyFingerprint,
        FirstFactor = proof.FirstFactor, FirstFactorVerifiedAt = proof.FirstFactorVerifiedAt,
        VerifiedAt = proof.VerifiedAt, Method = proof.Method
    };
}
