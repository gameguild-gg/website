using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class HardToSoftConversionRiskDecisionIssuerTests
{
    [Fact]
    public async Task IssueAsync_RejectsAnUnconfiguredDailyLimitBeforeTouchingPersistence()
    {
        var issuer = new PostgreSqlHardToSoftConversionRiskDecisionIssuer(
            null!);

        var act = () => issuer.IssueAsync(
            Request() with { MaximumHardCoinUnitsPerDay = 0 }, CancellationToken.None);

        await act.Should().ThrowAsync<EconomySelfServiceCommandRejectedException>()
            .WithMessage("*daily risk limit*");
    }

    [Fact]
    public async Task IssueAsync_RejectsARequestThatExceedsTheSignedDailyLimitBeforeTouchingPersistence()
    {
        var issuer = new PostgreSqlHardToSoftConversionRiskDecisionIssuer(
            null!);

        var act = () => issuer.IssueAsync(
            Request() with { TotalHardCoinUnits = 100, MaximumHardCoinUnitsPerDay = 99 },
            CancellationToken.None);

        await act.Should().ThrowAsync<EconomySelfServiceCommandRejectedException>()
            .WithMessage("*exceeds the signed daily HardCoin risk limit*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task IssueAsync_RejectsMissingRequiredIdentifiersBeforeTouchingPersistence(int missingIdentifier)
    {
        var request = Request();
        request = missingIdentifier switch
        {
            0 => request with { ActorId = Guid.Empty },
            1 => request with { TenantId = Guid.Empty },
            2 => request with { ReservationOperationId = Guid.Empty },
            _ => throw new ArgumentOutOfRangeException(nameof(missingIdentifier))
        };
        var issuer = new PostgreSqlHardToSoftConversionRiskDecisionIssuer(
            null!);

        var act = () => issuer.IssueAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("request");
    }

    [Theory]
    [InlineData(29)]
    [InlineData(901)]
    public async Task IssueAsync_RejectsAnUnsafeDecisionLifetimeBeforeTouchingPersistence(int lifetimeSeconds)
    {
        var issuer = new PostgreSqlHardToSoftConversionRiskDecisionIssuer(
            null!);

        var act = () => issuer.IssueAsync(
            Request() with { DecisionLifetimeSeconds = lifetimeSeconds }, CancellationToken.None);

        await act.Should().ThrowAsync<EconomySelfServiceCommandRejectedException>()
            .WithMessage("*lifetime must be between 30 and 900 seconds*");
    }

    private static HardToSoftConversionRiskDecisionRequest Request()
    {
        var now = DateTimeOffset.UtcNow;
        return new HardToSoftConversionRiskDecisionRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new WalletId(Guid.NewGuid()),
            Guid.NewGuid(),
            new IdempotencyKey("issuer-test-key"),
            0,
            100,
            200,
            300,
            "BRA",
            7,
            "signed-policy-hash",
            [
                new(ExternalRiskSource.FinancialCrime, 1, now.AddMinutes(-1), now.AddMinutes(5), ExternalRiskOutcome.Allow, "financial-crime"),
                new(ExternalRiskSource.TrustSafety, 1, now.AddMinutes(-1), now.AddMinutes(5), ExternalRiskOutcome.Allow, "trust-safety")
            ],
            now);
    }
}
