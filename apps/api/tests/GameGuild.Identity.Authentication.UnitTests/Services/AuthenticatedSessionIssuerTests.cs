using System.IdentityModel.Tokens.Jwt;
using GameGuild.CQRS;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class AuthenticatedSessionIssuerTests
{
    [Fact]
    public async Task OwnedTenantRolesAndPersistedRootBindingPrecedeAccessTokenIssuance()
    {
        var fixture = new IssuanceFixture();
        var result = await fixture.IssueAsync();
        Assert.True(result.Success);
        Assert.Equal(fixture.TenantId, result.TenantId);
        Assert.Equal(fixture.Token.SessionId, result.SessionId);
        Assert.NotEqual(Guid.Empty, result.SessionId);
        Assert.Equal(fixture.Hasher.HashToken(result.RefreshToken), fixture.Token.Token);
        Assert.NotEqual(result.RefreshToken, fixture.Token.Token);
        Assert.Equal(fixture.Token.ExpiresAt, result.RefreshTokenExpiresAt);
        Assert.Equal(fixture.AccessExpiresAt, result.AccessTokenExpiresAt);
        Assert.InRange(result.ExpiresIn, 895, 900);
        fixture.Jwt.Verify(service => service.GenerateAccessTokenAsync(fixture.User.Id, fixture.User.Email,
            It.Is<string[]>(roles => roles.Contains("Member") && roles.Contains("User") && !roles.Contains("SystemAdmin")),
            fixture.TenantId, fixture.User.TokenVersion, new DateTimeOffset(fixture.Token.CreatedAt),
            result.SessionId, CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData("inactive-member")]
    [InlineData("inactive-tenant")]
    [InlineData("pending-invite")]
    [InlineData("cancelled-invite")]
    [InlineData("empty-tenant")]
    [InlineData("foreign-tenant")]
    [InlineData("no-membership")]
    public async Task UnownedOrUnavailableTenantCannotCreateCredentials(string mode)
    {
        var fixture = new IssuanceFixture();
        var member = fixture.Membership;
        var memberships = mode switch
        {
            "inactive-member" => new[] { member with { IsActive = false } },
            "inactive-tenant" => new[] { member with { TenantIsActive = false } },
            "pending-invite" => new[] { member with { InviteStatus = "Pending" } },
            "cancelled-invite" => new[] { member with { InviteStatus = "Cancelled" } },
            "empty-tenant" => new[] { member with { TenantId = Guid.Empty } },
            "no-membership" => [],
            _ => new[] { member }
        };
        fixture.SetMemberships(memberships);
        await Assert.ThrowsAsync<AuthenticationRequiredException>(() => fixture.IssueAsync(mode == "foreign-tenant" ? Guid.NewGuid() : null));
        fixture.Jwt.VerifyNoOtherCalls();
        fixture.Sessions.VerifyNoOtherCalls();
        fixture.Repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong-owner")]
    [InlineData("revoked")]
    [InlineData("expired")]
    [InlineData("already-bound")]
    [InlineData("non-root")]
    [InlineData("replaced")]
    [InlineData("hash-mismatch")]
    public async Task InvalidPersistedRefreshRootCannotCreateSession(string mode)
    {
        var fixture = new IssuanceFixture();
        switch (mode)
        {
            case "missing": fixture.Repository.Setup(service => service.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((RefreshToken?)null); break;
            case "wrong-owner": fixture.Token.UserId = Guid.NewGuid(); break;
            case "revoked": fixture.Token.IsRevoked = true; break;
            case "expired": fixture.Token.ExpiresAt = SystemClock.UtcNow.AddSeconds(-1); break;
            case "already-bound": fixture.Token.SessionId = Guid.NewGuid(); break;
            case "non-root": fixture.Token.ParentTokenId = Guid.NewGuid(); break;
            case "replaced": fixture.Token.ReplacedByToken = "old-replacement-hash"; break;
            case "hash-mismatch": fixture.Token.Token = fixture.Hasher.HashToken("different-test-token"); break;
            default: throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown persisted-root fault.");
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.IssueAsync());
        fixture.Sessions.VerifyNoOtherCalls();
        fixture.VerifyNoAccessIssued();
    }

    [Theory]
    [InlineData("unbound-token")]
    [InlineData("wrong-session-owner")]
    [InlineData("inactive-session")]
    [InlineData("wrong-session-hash")]
    public async Task MissingOrInvalidSessionBindingCannotReturnAccessCredentials(string mode)
    {
        var fixture = new IssuanceFixture { SessionFault = mode };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.IssueAsync());
        fixture.VerifyNoAccessIssued();
    }

    [Fact]
    public async Task AbsoluteSessionCapIsStoredAndReportedForBothCredentials()
    {
        var fixture = new IssuanceFixture { SessionCap = SystemClock.UtcNow.AddSeconds(90) };
        var result = await fixture.IssueAsync();
        Assert.Equal(fixture.SessionCap, fixture.Token.ExpiresAt);
        Assert.Equal(fixture.Token.ExpiresAt, result.RefreshTokenExpiresAt);
        Assert.Equal(fixture.Token.ExpiresAt, result.ExpiresAt);
        Assert.Equal(fixture.Token.ExpiresAt, result.AccessTokenExpiresAt);
        Assert.InRange(result.ExpiresIn, 85, 90);
        fixture.Repository.Verify(service => service.UpdateAsync(fixture.Token, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task BindingCapDoesNotChangeTheRequestedSessionValidationDeadline()
    {
        var cap = SystemClock.UtcNow.AddHours(1);
        IssuanceFixture? fixture = null;
        fixture = new IssuanceFixture
        {
            SessionCap = cap,
            // Binding can mutate the tracked token before returning the session.
            // PostgreSQL's stored precision can be lower than the returned DateTime.
            AfterBinding = () => fixture!.Token.ExpiresAt = cap.AddTicks(-1)
        };
        var result = await fixture.IssueAsync();
        Assert.True(result.Success);
        Assert.Equal(fixture.Token.ExpiresAt, result.RefreshTokenExpiresAt);
        Assert.Equal(fixture.Token.ExpiresAt, result.ExpiresAt);
        Assert.True(result.RefreshTokenExpiresAt <= cap);
    }

    [Fact]
    public async Task CancellationAfterPersistedBindingCannotIssueAccessCredentials()
    {
        using var cancellation = new CancellationTokenSource();
        var fixture = new IssuanceFixture { AfterBinding = cancellation.Cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.IssueAsync(cancellationToken: cancellation.Token));
        Assert.NotNull(fixture.Token.SessionId);
        fixture.VerifyNoAccessIssued();
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("expired-jwt")]
    public async Task InvalidAccessTokenCannotReportSuccessfulSignIn(string mode)
    {
        var fixture = new IssuanceFixture { AccessFault = mode };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.IssueAsync());
    }

    private sealed class IssuanceFixture
    {
        public User User { get; } = new() { Id = Guid.NewGuid(), Email = "issuer@example.test", TokenVersion = 7, Version = 1 };
        public Guid TenantId { get; } = Guid.NewGuid();
        public RefreshTokenHasher Hasher { get; } = new();
        public Mock<ISender> Sender { get; } = new();
        public Mock<IJwtTokenService> Jwt { get; } = new();
        public Mock<IRefreshTokenRepository> Repository { get; } = new();
        public Mock<ISessionManagementService> Sessions { get; } = new();
        public RefreshToken Token { get; }
        public UserMembershipDto Membership { get; }
        public DateTime AccessExpiresAt { get; } = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds()).UtcDateTime;
        public DateTime? SessionCap { get; init; }
        public string? SessionFault { get; init; }
        public string? AccessFault { get; init; }
        public Action? AfterBinding { get; init; }

        public IssuanceFixture()
        {
            Membership = new UserMembershipDto { TenantId = TenantId, TenantIsActive = true, IsActive = true, Role = "Member", InviteStatus = "Accepted" };
            SetMemberships([Membership, Membership with { TenantId = Guid.NewGuid(), Role = "SystemAdmin" }]);
            Token = new RefreshToken { Id = Guid.NewGuid(), UserId = User.Id, Token = Hasher.HashToken("synthetic-refresh-token"),
                CreatedAt = SystemClock.UtcNow.AddSeconds(-1), ExpiresAt = SystemClock.UtcNow.AddDays(30) };
            Repository.Setup(service => service.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Token);
            Repository.Setup(service => service.UpdateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>())).ReturnsAsync((RefreshToken token, CancellationToken _) => token);
            Jwt.Setup(service => service.GenerateRefreshTokenAsync(User.Id, It.IsAny<DeviceInfo>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("synthetic-refresh-token");
            Sessions.Setup(service => service.CreateSessionAsync(It.IsAny<Guid>(), User.Id, It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, Guid owner, string _, string _, string hash, DateTime expiry, string? _, CancellationToken _) =>
                {
                    if (SessionFault != "unbound-token")
                    {
                        Token.SessionId = id;
                    }
                    AfterBinding?.Invoke();
                    return new UserSession { Id = id, UserId = SessionFault == "wrong-session-owner" ? Guid.NewGuid() : owner,
                        RefreshToken = SessionFault == "wrong-session-hash" ? Hasher.HashToken("other-token") : hash,
                        ExpiresAt = SessionCap ?? expiry, IsActive = SessionFault != "inactive-session" };
                });
            Jwt.Setup(service => service.GenerateAccessTokenAsync(User.Id, User.Email, It.IsAny<string[]>(), It.IsAny<Guid?>(), User.TokenVersion,
                    It.IsAny<DateTimeOffset>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => AccessFault == "not-a-jwt" ? "invalid-token" : new JwtSecurityTokenHandler().WriteToken(
                    new JwtSecurityToken(expires: AccessFault == "expired-jwt" ? SystemClock.UtcNow.AddSeconds(-1) : AccessExpiresAt)));
        }

        public void SetMemberships(IReadOnlyList<UserMembershipDto> values) => Sender
            .Setup(service => service.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserMembershipsResponse { Memberships = values, TotalCount = values.Count });

        public Task<SignInResponse> IssueAsync(Guid? requestedTenantId = null, CancellationToken cancellationToken = default) =>
            new AuthenticatedSessionIssuer(Sender.Object, Jwt.Object, Hasher, Repository.Object, Sessions.Object)
                .IssueAsync(User, requestedTenantId, new DeviceInfo { Fingerprint = "synthetic-device", IpAddress = "127.0.0.1", UserAgent = "test-agent" }, cancellationToken);

        public void VerifyNoAccessIssued() => Jwt.Verify(service => service.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
