using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GameGuild.Identity.Authentication.UnitTests.Guest;

/// <summary>
///     Guest session lifecycle (issue #213): issuance, token namespace isolation, one-time
/// upgrade and security-event auditing — all against the real distributed-cache store.
/// </summary>
public sealed class GuestSessionServiceTests
{
    private static readonly JwtOptions TestJwtOptions = new()
    {
        Issuer = "GameGuild-Test",
        Audience = "GameGuild.Users",
        SecretKey = "test-secret-key-at-least-32-characters-long!!"
    };

    private sealed class Fixture
    {
        public Fixture(bool enabled = true, int lifetimeMinutes = 30)
        {
            Options = new GuestSessionOptions { IsEnabled = enabled, AccessTokenLifetimeMinutes = lifetimeMinutes };
            TokenService = new GuestTokenService(Options.Create(TestJwtOptions), NullLogger<GuestTokenService>.Instance);
            Cache = new MemoryDistributedCache(new MemoryDistributedCacheOptions());
            Store = new DistributedCacheGuestSessionStore(Cache, Options.Create(Options), NullLogger<DistributedCacheGuestSessionStore>.Instance);
            AuditSink = new Mock<IAuthenticationAuditEventSink>();
            Service = new GuestSessionService(
                Store,
                TokenService,
                Options.Create(Options),
                AuditSink.Object,
                NullLogger<GuestSessionService>.Instance);
        }

        public GuestSessionOptions Options { get; }

        public GuestTokenService TokenService { get; }

        public MemoryDistributedCache Cache { get; }

        public DistributedCacheGuestSessionStore Store { get; }

        public Mock<IAuthenticationAuditEventSink> AuditSink { get; }

        public GuestSessionService Service { get; }
    }

    private static void VerifyAudit(Mock<IAuthenticationAuditEventSink> sink, string actionType, Times times) =>
        sink.Verify(s => s.RecordAsync(
            It.Is<AuthenticationAuditEvent>(e => e.ActionType == actionType),
            It.IsAny<CancellationToken>()), times);

    [Fact]
    public async Task Issuance_CreatesShortLivedSession_WithAllowlistPermissions_AndAudits()
    {
        var fixture = new Fixture();

        var result = await fixture.Service.IssueAsync(new GuestSessionRequest { IpAddress = "203.0.113.7" });

        result.Success.Should().BeTrue();
        result.GuestSessionId.Should().NotBe(Guid.Empty);
        result.Permissions.Should().BeEquivalentTo(GuestPermission.Allowlist);
        result.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.ExpiresAt.Should().BeAfter(SystemClock.UtcNow).And.BeOnOrBefore(SystemClock.UtcNow.AddMinutes(30));
        VerifyAudit(fixture.AuditSink, "Authentication.GuestSessionIssued", Times.Once());
    }

    [Fact]
    public async Task Issuance_FailsClosedAndAudits_WhenGuestAccessIsDisabled()
    {
        var fixture = new Fixture(enabled: false);

        var result = await fixture.Service.IssueAsync(new GuestSessionRequest());

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be(GuestSessionFailureReason.GuestAccessDisabled);
        result.AccessToken.Should().BeEmpty();
        VerifyAudit(fixture.AuditSink, "Authentication.GuestSessionIssuanceRejected", Times.Once());
    }

    [Fact]
    public void IssuedToken_NeverCarriesUserOrTenantClaims()
    {
        var fixture = new Fixture();
        var issued = fixture.Service.IssueAsync(new GuestSessionRequest()).GetAwaiter().GetResult();

        var principal = fixture.TokenService.Validate(issued.AccessToken);

        principal.Should().NotBeNull();
        principal!.IsGuest().Should().BeTrue();
        principal.TryGetGuestId(out var guestId).Should().BeTrue();
        guestId.Should().Be(issued.GuestSessionId);
        // Identity namespace isolation: no user id, no email, no tenant.
        principal.FindFirst(ClaimNames.Subject).Should().BeNull();
        principal.FindFirst(ClaimNames.UserId).Should().BeNull();
        principal.FindFirst(ClaimNames.NameIdentifier).Should().BeNull();
        principal.FindFirst(ClaimNames.Email).Should().BeNull();
        principal.FindFirst(ClaimNames.TenantId).Should().BeNull();
        principal.FindFirst(ClaimNames.TenantIdAlt).Should().BeNull();
    }

    [Fact]
    public void IssuedToken_IsInvalidOnTheStandardUserAudience()
    {
        // Structural fail-closed: the default bearer scheme validates the user audience and
        // must reject a guest token outright (it is not merely unauthorized, it is invalid).
        var fixture = new Fixture();
        var issued = fixture.Service.IssueAsync(new GuestSessionRequest()).GetAwaiter().GetResult();

        var handler = new JwtSecurityTokenHandler();
        var parameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(TestJwtOptions.SecretKey)),
            ValidateIssuer = true,
            ValidIssuer = TestJwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = TestJwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var act = () => handler.ValidateToken(issued.AccessToken, parameters, out _);
        act.Should().Throw<Microsoft.IdentityModel.Tokens.SecurityTokenInvalidAudienceException>();
    }

    [Fact]
    public async Task ValidateAsync_ReturnsLiveSnapshot_AndNullForGarbage()
    {
        var fixture = new Fixture();
        var issued = await fixture.Service.IssueAsync(new GuestSessionRequest());

        (await fixture.Service.ValidateAsync(issued.AccessToken)).Should().NotBeNull();
        (await fixture.Service.ValidateAsync("not-a-token")).Should().BeNull();
        (await fixture.Service.ValidateAsync(null)).Should().BeNull();
    }

    [Fact]
    public async Task Upgrade_MarksSessionUpgradedOnce_AndAuditsContinuity()
    {
        var fixture = new Fixture();
        var issued = await fixture.Service.IssueAsync(new GuestSessionRequest());
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var upgrade = await fixture.Service.UpgradeAsync(issued.AccessToken, userId, sessionId);

        upgrade.Success.Should().BeTrue();
        upgrade.GuestSessionId.Should().Be(issued.GuestSessionId);
        VerifyAudit(fixture.AuditSink, "Authentication.GuestSessionUpgraded", Times.Once());

        // One-time semantics: the same guest credential cannot upgrade again, and the
        // session no longer validates.
        var replay = await fixture.Service.UpgradeAsync(issued.AccessToken, userId, Guid.NewGuid());
        replay.Success.Should().BeFalse();
        replay.FailureReason.Should().Be(GuestSessionFailureReason.GuestSessionUnavailable);
        (await fixture.Service.ValidateAsync(issued.AccessToken)).Should().BeNull();
        VerifyAudit(fixture.AuditSink, "Authentication.GuestSessionUpgradeRejected", Times.Once());
    }

    [Fact]
    public async Task Upgrade_RejectsInvalidCredentials_WithoutMappingAGuestOntoAUser()
    {
        var fixture = new Fixture();
        var upgrade = await fixture.Service.UpgradeAsync("forged-token", Guid.NewGuid(), Guid.NewGuid());

        upgrade.Success.Should().BeFalse();
        upgrade.FailureReason.Should().Be(GuestSessionFailureReason.InvalidGuestToken);
        VerifyAudit(fixture.AuditSink, "Authentication.GuestSessionUpgradeRejected", Times.Once());
    }

    [Fact]
    public async Task Upgrade_RejectsUnauthenticatedSessionTargets()
    {
        var fixture = new Fixture();
        var issued = await fixture.Service.IssueAsync(new GuestSessionRequest());

        var upgrade = await fixture.Service.UpgradeAsync(issued.AccessToken, Guid.NewGuid(), Guid.Empty);

        upgrade.Success.Should().BeFalse();
        upgrade.FailureReason.Should().Be(GuestSessionFailureReason.AuthenticatedSessionRequired);
        // No upgrade audit: the request never presented a valid continuation target.
        VerifyAudit(fixture.AuditSink, "Authentication.GuestSessionUpgraded", Times.Never());
    }

    [Fact]
    public async Task Upgrade_RejectsExpiredGuestSessions()
    {
        var fixture = new Fixture();
        // Issue with the minimum lifetime, then age the snapshot past expiry by writing it
        // directly through the store with a past expiry.
        var issued = await fixture.Service.IssueAsync(new GuestSessionRequest());
        await fixture.Store.SaveAsync(new GuestSessionSnapshot
        {
            GuestSessionId = issued.GuestSessionId,
            IssuedAt = SystemClock.UtcNow.AddMinutes(-10),
            ExpiresAt = SystemClock.UtcNow.AddMinutes(-5)
        });

        var upgrade = await fixture.Service.UpgradeAsync(issued.AccessToken, Guid.NewGuid(), Guid.NewGuid());

        upgrade.Success.Should().BeFalse();
        upgrade.FailureReason.Should().Be(GuestSessionFailureReason.GuestSessionUnavailable);
    }

    [Fact]
    public async Task Upgrade_AuditEventCarriesTheUserIdAndBothSessionIds()
    {
        var fixture = new Fixture();
        var issued = await fixture.Service.IssueAsync(new GuestSessionRequest());
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await fixture.Service.UpgradeAsync(issued.AccessToken, userId, sessionId);

        fixture.AuditSink.Verify(s => s.RecordAsync(
            It.Is<AuthenticationAuditEvent>(e =>
                e.ActionType == "Authentication.GuestSessionUpgraded" &&
                e.Success &&
                e.UserId == userId &&
                e.SessionId == sessionId &&
                e.Method == "Guest"),
            It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public void GuestAccessDisabledException_MapsTheFailClosedOutcome()
    {
        var handler = new IssueGuestSessionHandler(
            new DisabledGuestSessionService(),
            httpContextAccessor: null,
            logger: NullLogger<IssueGuestSessionHandler>.Instance);

        var act = () => handler.Handle(new IssueGuestSessionCommand(), CancellationToken.None);
        act.Should().ThrowAsync<GuestAccessDisabledException>();
    }

    private sealed class DisabledGuestSessionService : IGuestSessionService
    {
        public Task<GuestSessionResult> IssueAsync(GuestSessionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new GuestSessionResult { Success = false, FailureReason = GuestSessionFailureReason.GuestAccessDisabled });

        public Task<GuestSessionSnapshot?> ValidateAsync(string? guestToken, CancellationToken cancellationToken = default)
            => Task.FromResult<GuestSessionSnapshot?>(null);

        public Task<GuestSessionUpgradeResult> UpgradeAsync(string? guestToken, Guid userId, Guid authenticatedSessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(new GuestSessionUpgradeResult { Success = false, FailureReason = GuestSessionFailureReason.InvalidGuestToken });
    }
}
