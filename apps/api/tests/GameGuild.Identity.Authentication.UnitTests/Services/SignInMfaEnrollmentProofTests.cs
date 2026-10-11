using GameGuild.Identity.Authentication;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class SignInMfaEnrollmentProofTests
{
    [Fact]
    public void CompletedEnrollmentCanCreateOnlyATotpProofWithItsServerBinding()
    {
        var challenge = Challenge();
        var verifiedAt = challenge.CreatedAt.AddSeconds(10);
        var proof = SignInMfaProof.FromVerifiedChallenge(challenge, MfaMethod.Totp, verifiedAt);
        Assert.Equal(challenge.Id, proof.ChallengeId);
        Assert.Equal(challenge.SubjectId, proof.SubjectId);
        Assert.Equal(challenge.TenantId, proof.TenantId);
        Assert.Equal(challenge.CreatedAt, proof.FirstFactorVerifiedAt);
        Assert.Equal(verifiedAt, proof.VerifiedAt);
        Assert.Equal(MfaMethod.Totp, proof.Method);
        Assert.Throws<AuthenticationRequiredException>(() => SignInMfaProof.FromVerifiedChallenge(challenge, MfaMethod.BackupCode, verifiedAt));
        Assert.Throws<AuthenticationRequiredException>(() => SignInMfaProof.FromVerifiedChallenge(challenge, MfaMethod.WebAuthn, verifiedAt));
    }

    [Theory]
    [InlineData("missing-configuration")]
    [InlineData("empty-configuration")]
    [InlineData("missing-fingerprint")]
    [InlineData("invalid-fingerprint")]
    [InlineData("missing-initialization")]
    [InlineData("initialization-before-first-factor")]
    [InlineData("initialization-after-proof")]
    [InlineData("expired")]
    public void IncompleteOrStaleEnrollmentEvidenceCannotBecomeAnMfaProof(string fault)
    {
        var challenge = Challenge();
        var verifiedAt = challenge.CreatedAt.AddSeconds(10);
        switch (fault)
        {
            case "missing-configuration": challenge.EnrollmentConfigurationId = null; break;
            case "empty-configuration": challenge.EnrollmentConfigurationId = Guid.Empty; break;
            case "missing-fingerprint": challenge.EnrollmentSecretFingerprint = null; break;
            case "invalid-fingerprint": challenge.EnrollmentSecretFingerprint = "invalid"; break;
            case "missing-initialization": challenge.EnrollmentInitializedAt = null; break;
            case "initialization-before-first-factor": challenge.EnrollmentInitializedAt = challenge.CreatedAt.AddSeconds(-1); break;
            case "initialization-after-proof": challenge.EnrollmentInitializedAt = verifiedAt.AddSeconds(1); break;
            case "expired": verifiedAt = challenge.ExpiresAt; break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        Assert.Throws<AuthenticationRequiredException>(() => SignInMfaProof.FromVerifiedChallenge(challenge, MfaMethod.Totp, verifiedAt));
    }

    private static SignInMfaChallenge Challenge()
    {
        var challenge = SignInMfaChallengeTests.CreateChallenge();
        challenge.Purpose = SignInMfaPurpose.EnrollFactor;
        challenge.EnrollmentConfigurationId = Guid.NewGuid();
        challenge.EnrollmentSecretFingerprint = new string('a', 64);
        challenge.EnrollmentInitializedAt = challenge.CreatedAt;
        return challenge;
    }
}
