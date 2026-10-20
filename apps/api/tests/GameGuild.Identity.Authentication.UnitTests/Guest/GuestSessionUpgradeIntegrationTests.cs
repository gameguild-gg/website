using GameGuild.Identity.Authentication.UnitTests.Guest;

namespace GameGuild.Identity.Authentication.UnitTests.Guest;

/// <summary>
///     Session-continuity wiring (issue #213): the sign-in/sign-up response records the
///     upgraded guest session, and an invalid guest token never blocks the sign-in.
/// </summary>
public sealed class GuestSessionUpgradeIntegrationTests
{
    [Fact]
    public async Task SuccessfulUpgrade_StampsTheResponseContinuity()
    {
        var service = new StubGuestSessionService(
            new GuestSessionUpgradeResult { Success = true, GuestSessionId = Guid.NewGuid() });
        var response = new SignInResponse { Success = true, UserId = Guid.NewGuid(), SessionId = Guid.NewGuid() };

        var upgraded = await GuestSessionUpgradeIntegration.ApplyGuestUpgradeAsync(
            response, service, "guest-token", CancellationToken.None);

        upgraded.Should().BeSameAs(response);
        upgraded.UpgradedFromGuestSessionId.Should().Be(service.UpgradeResult.GuestSessionId);
    }

    [Fact]
    public async Task RejectedUpgrade_KeepsTheSignInSuccessful_WithoutContinuity()
    {
        var service = new StubGuestSessionService(
            new GuestSessionUpgradeResult { Success = false, FailureReason = GuestSessionFailureReason.InvalidGuestToken });
        var response = new SignInResponse { Success = true, UserId = Guid.NewGuid(), SessionId = Guid.NewGuid() };

        var upgraded = await GuestSessionUpgradeIntegration.ApplyGuestUpgradeAsync(
            response, service, "forged", CancellationToken.None);

        upgraded.Success.Should().BeTrue();
        upgraded.UpgradedFromGuestSessionId.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingGuestToken_SkipsTheUpgradeEntirely(string? token)
    {
        var service = new StubGuestSessionService(new GuestSessionUpgradeResult { Success = true });
        var response = new SignInResponse { Success = true, UserId = Guid.NewGuid(), SessionId = Guid.NewGuid() };

        await GuestSessionUpgradeIntegration.ApplyGuestUpgradeAsync(response, service, token, CancellationToken.None);

        service.Called.Should().BeFalse();
        response.UpgradedFromGuestSessionId.Should().BeNull();
    }

    [Fact]
    public async Task UnissuedSession_SkipsTheUpgradeEntirely()
    {
        var service = new StubGuestSessionService(new GuestSessionUpgradeResult { Success = true });
        var response = new SignInResponse { Success = false, UserId = Guid.NewGuid(), SessionId = Guid.NewGuid() };

        await GuestSessionUpgradeIntegration.ApplyGuestUpgradeAsync(response, service, "token", CancellationToken.None);

        service.Called.Should().BeFalse();
    }

    private sealed class StubGuestSessionService(GuestSessionUpgradeResult upgradeResult) : IGuestSessionService
    {
        public GuestSessionUpgradeResult UpgradeResult { get; } = upgradeResult;

        public bool Called { get; private set; }

        public Task<GuestSessionResult> IssueAsync(GuestSessionRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<GuestSessionSnapshot?> ValidateAsync(string? guestToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<GuestSessionUpgradeResult> UpgradeAsync(string? guestToken, Guid userId, Guid authenticatedSessionId, CancellationToken cancellationToken = default)
        {
            Called = true;
            return Task.FromResult(UpgradeResult);
        }
    }
}
