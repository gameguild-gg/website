using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using GameGuild.Configuration.ApplicationLayer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class SignInMfaProofTests
{
    [Theory]
    [InlineData("subject")]
    [InlineData("tenant")]
    [InlineData("version")]
    [InlineData("auth-time")]
    [InlineData("future")]
    public async Task MismatchedOrFutureProofCannotMintMfaClaims(string fault)
    {
        var proof = Proof();
        var service = Jwt();
        await Assert.ThrowsAsync<AuthenticationRequiredException>(() => service.GenerateMfaAccessTokenAsync(
            fault == "subject" ? Guid.NewGuid() : proof.SubjectId, "proof@example.test", ["Member"],
            fault == "tenant" ? Guid.NewGuid() : proof.TenantId, fault == "version" ? proof.TokenVersion + 1 : proof.TokenVersion,
            fault == "auth-time" ? proof.FirstFactorVerifiedAt.AddSeconds(1) : proof.FirstFactorVerifiedAt,
            Guid.NewGuid(), fault == "future" ? Proof(verifiedAt: DateTimeOffset.UtcNow.AddMinutes(1),
                subjectId: proof.SubjectId, tenantId: proof.TenantId, authenticatedAt: proof.FirstFactorVerifiedAt) : proof));
    }

    [Theory]
    [InlineData("empty-challenge")]
    [InlineData("empty-subject")]
    [InlineData("empty-tenant")]
    [InlineData("invalid-version")]
    [InlineData("invalid-policy")]
    [InlineData("invalid-factor")]
    [InlineData("invalid-method")]
    [InlineData("before-first-factor")]
    [InlineData("outside-challenge-window")]
    public void InvalidDurableProofCannotBeRestored(string fault)
    {
        var proof = Proof();
        var stored = SessionMfaEvidence.Create(Guid.NewGuid(), proof);
        switch (fault)
        {
            case "empty-challenge": stored.ChallengeId = Guid.Empty; break;
            case "empty-subject": stored.SubjectId = Guid.Empty; break;
            case "empty-tenant": stored.TenantId = Guid.Empty; break;
            case "invalid-version": stored.TokenVersion = 0; break;
            case "invalid-policy": stored.PolicyFingerprint = "invalid"; break;
            case "invalid-factor": stored.FirstFactor = (SignInFirstFactor)int.MaxValue; break;
            case "invalid-method": stored.Method = MfaMethod.Sms; break;
            case "before-first-factor": stored.VerifiedAt = stored.FirstFactorVerifiedAt.AddSeconds(-1); break;
            case "outside-challenge-window": stored.VerifiedAt = stored.FirstFactorVerifiedAt.AddMinutes(6); break;
            default: throw new ArgumentOutOfRangeException(nameof(fault), fault, "Unknown durable MFA proof fault.");
        }
        Assert.Throws<AuthenticationRequiredException>(stored.ToProof);
    }

    [Theory]
    [InlineData(MfaMethod.Totp)]
    [InlineData(MfaMethod.BackupCode)]
    public async Task VerifiedProofEmitsMfaClaimsWithOriginalTimes(MfaMethod method)
    {
        var proof = Proof(method: method);
        var service = Jwt();
        var sessionId = Guid.NewGuid();
        var access = await service.GenerateMfaAccessTokenAsync(proof.SubjectId, "proof@example.test", ["Member"],
            proof.TenantId, proof.TokenVersion, proof.FirstFactorVerifiedAt, sessionId, proof);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(access);
        Assert.Contains(jwt.Claims, claim => claim.Type == "amr" && claim.Value == "mfa");
        Assert.Contains(jwt.Claims, claim => claim.Type == "amr" && claim.Value == "pwd");
        Assert.Equal(method == MfaMethod.Totp, jwt.Claims.Any(claim => claim.Type == "amr" && claim.Value == "otp"));
        Assert.Equal("true", Assert.Single(jwt.Claims, claim => claim.Type == "mfa_verified").Value);
        Assert.Equal(proof.VerifiedAt.ToUnixTimeSeconds().ToString(), Assert.Single(jwt.Claims, claim => claim.Type == "mfa_time").Value);
        Assert.Equal(proof.FirstFactorVerifiedAt.ToUnixTimeSeconds().ToString(), Assert.Single(jwt.Claims, claim => claim.Type == "auth_time").Value);
        Assert.Equal(sessionId.ToString(), Assert.Single(jwt.Claims, claim => claim.Type == "session_id").Value);
    }

    [Fact]
    public async Task OrdinaryCredentialsCarryNoMfaEvidence()
    {
        var access = await Jwt().GenerateAccessTokenAsync(Guid.NewGuid(), "ordinary@example.test", ["Member"], Guid.NewGuid(), 1);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(access);
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type is "mfa_verified" or "mfa_time" or "amr");
    }

    private static SignInMfaProof Proof(MfaMethod method = MfaMethod.BackupCode, DateTimeOffset? verifiedAt = null,
        Guid? subjectId = null, Guid? tenantId = null, DateTimeOffset? authenticatedAt = null) =>
        new(Guid.NewGuid(), subjectId ?? Guid.NewGuid(), tenantId ?? Guid.NewGuid(), 7, new string('a', 64),
            SignInFirstFactor.Password, authenticatedAt ?? DateTimeOffset.UtcNow.AddMinutes(-2),
            verifiedAt ?? DateTimeOffset.UtcNow.AddMinutes(-1), method);

    private static JwtTokenService Jwt() => new(Mock.Of<ILogger<JwtTokenService>>(), Mock.Of<IRefreshTokenRepository>(),
        Mock.Of<IRefreshTokenHasher>(), Mock.Of<IHttpContextAccessor>(), Options.Create(new JwtOptions
        {
            SecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)), Issuer = "SyntheticIssuer",
            Audience = "SyntheticAudience", AccessTokenExpirationMinutes = 15, RefreshTokenExpirationDays = 7
        }));
}
