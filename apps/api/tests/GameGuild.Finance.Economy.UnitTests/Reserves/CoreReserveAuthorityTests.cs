using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Reserves;

namespace GameGuild.Finance.Economy.UnitTests.Reserves;

public sealed class CoreReserveAuthorityTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CoreAtomicallyActivatesOneProposalAndRejectsAStaleExpectedVersion()
    {
        var authority = new CoreReserveAuthority();
        var first = Proposal(new ReserveVersion(1), expected: null, epoch: 1);

        var activated = authority.ValidateAndActivate(first, Now);

        activated.Version.Should().Be(new ReserveVersion(1));
        activated.PolicyVersion.Should().Be(first.PolicyVersion);
        activated.AssetAllocations.Should().BeEquivalentTo(first.AssetAllocations);
        activated.Requirements.HardFaceValueUsdMinor.Should().BeGreaterThan(0);
        activated.Requirements.SoftFaceValueUsdNanos.Should().BeGreaterThan(0);
        activated.Requirements.StressedExpectedRedemptionCostUsdNanos.Should().BeGreaterThan(0);
        activated.Coverage.Should().Be(ReserveCoverageState.Covered);
        authority.ActiveHead.Should().Be(activated);

        FluentActions.Invoking(() => authority.ValidateAndActivate(
                Proposal(new ReserveVersion(2), expected: null, epoch: 2), Now))
            .Should().Throw<ReserveVersionConflictException>();
        authority.ActiveHead.Should().Be(activated);
    }

    [Fact]
    public async Task CoreSerializesConcurrentProposalActivation()
    {
        var authority = new CoreReserveAuthority();
        var active = authority.ValidateAndActivate(Proposal(new ReserveVersion(1), null, 1), Now);
        using var start = new Barrier(2);

        async Task<(ReserveHead? Head, Exception? Error)> AttemptAsync()
        {
            await Task.Yield();
            start.SignalAndWait();
            try
            {
                return (authority.ValidateAndActivate(
                    Proposal(new ReserveVersion(2), active.Version, 2), Now), null);
            }
            catch (Exception exception)
            {
                return (null, exception);
            }
        }

        var results = await Task.WhenAll(
            Task.Run(AttemptAsync),
            Task.Run(AttemptAsync));

        var activated = results.Single(result => result.Head is not null).Head!;
        results.Should().ContainSingle(result => result.Error is ReserveVersionConflictException);
        authority.ActiveHead.Should().BeSameAs(activated);
        activated.Version.Should().Be(new ReserveVersion(2));
    }

    [Fact]
    public void CoreRejectsDuplicateExternalAssetAllocationWithoutChangingTheHead()
    {
        var authority = new CoreReserveAuthority();
        var active = authority.ValidateAndActivate(Proposal(new ReserveVersion(1), null, 1), Now);
        var duplicateAssets = new[]
        {
            new ExternalReserveAsset("cash-ledger-1", ReserveBackingPurpose.HardCoin, 1_000_000_000),
            new ExternalReserveAsset("cash-ledger-1", ReserveBackingPurpose.SoftCoin, 1_000_000_000)
        };

        FluentActions.Invoking(() => authority.ValidateAndActivate(
                Proposal(new ReserveVersion(2), active.Version, 2) with { AssetAllocations = duplicateAssets }, Now))
            .Should().Throw<DuplicateReserveAssetException>();
        authority.ActiveHead.Should().Be(active);
    }

    [Fact]
    public void CoreRejectsStaleProposalAndNonIncreasingVersionOrAuthorizationEpoch()
    {
        var authority = new CoreReserveAuthority();
        var active = authority.ValidateAndActivate(Proposal(new ReserveVersion(1), null, 5), Now);

        FluentActions.Invoking(() => authority.ValidateAndActivate(
                Proposal(new ReserveVersion(2), active.Version, 6) with { ExpiresAt = Now }, Now))
            .Should().Throw<ReserveInputUnknownException>();
        FluentActions.Invoking(() => authority.ValidateAndActivate(
                Proposal(new ReserveVersion(1), active.Version, 6), Now))
            .Should().Throw<ReserveVersionConflictException>();
        FluentActions.Invoking(() => authority.ValidateAndActivate(
                Proposal(new ReserveVersion(2), active.Version, 5), Now))
            .Should().Throw<ReserveAuthorizationEpochException>();
    }

    [Fact]
    public void CoreRejectsAnExpectedVersionWhenNoHeadExists()
    {
        var authority = new CoreReserveAuthority();

        FluentActions.Invoking(() => authority.ValidateAndActivate(
                Proposal(new ReserveVersion(2), new ReserveVersion(1), 1), Now))
            .Should().Throw<ReserveVersionConflictException>();
    }

    [Theory]
    [MemberData(nameof(InvalidAssets))]
    public void CoreRejectsInvalidExternalAssetAllocations(ExternalReserveAsset invalidAsset)
    {
        var authority = new CoreReserveAuthority();

        FluentActions.Invoking(() => authority.ValidateAndActivate(
                Proposal(new ReserveVersion(1), null, 1) with { AssetAllocations = [invalidAsset] }, Now))
            .Should().Throw<ReserveInputUnknownException>();
    }

    [Fact]
    public void CoreRejectsBackingThatExceedsTheSupportedUnitRange()
    {
        var authority = new CoreReserveAuthority();
        var assets = new[]
        {
            new ExternalReserveAsset("hard-1", ReserveBackingPurpose.HardCoin, long.MaxValue),
            new ExternalReserveAsset("hard-2", ReserveBackingPurpose.HardCoin, long.MaxValue),
            new ExternalReserveAsset("soft", ReserveBackingPurpose.SoftCoin, 1)
        };

        FluentActions.Invoking(() => authority.ValidateAndActivate(
                Proposal(new ReserveVersion(1), null, 1) with { AssetAllocations = assets }, Now))
            .Should().Throw<OverflowException>();
    }

    [Fact]
    public void AuthorizationLocksTheActiveCoveredVersionAndEpoch()
    {
        var authority = new CoreReserveAuthority();
        authority.ValidateAndActivate(Proposal(new ReserveVersion(1), null, 7), Now);

        var authorization = authority.Authorize(new ReserveVersion(1), 7, Now);
        authorization.Should().Be(new ReservePostingAuthorization(new ReserveVersion(1), 7, Now));
        authorization.LockedAt.Should().Be(Now);
        FluentActions.Invoking(() => authority.Authorize(new ReserveVersion(2), 7, Now))
            .Should().Throw<ReserveAuthorizationException>();
        FluentActions.Invoking(() => authority.Authorize(new ReserveVersion(1), 6, Now))
            .Should().Throw<ReserveAuthorizationEpochException>();
        FluentActions.Invoking(() => authority.Authorize(
                new ReserveVersion(1), 7, Now.AddMinutes(-2)))
            .Should().Throw<ReserveInputUnknownException>();
        FluentActions.Invoking(() => authority.Authorize(new ReserveVersion(1), 7, Now.AddMinutes(6)))
            .Should().Throw<ReserveInputUnknownException>();
    }

    [Fact]
    public void AuthorizationFailsClosedWithoutAHeadOrDuringAShortfall()
    {
        var authority = new CoreReserveAuthority();
        FluentActions.Invoking(() => authority.Authorize(new ReserveVersion(1), 1, Now))
            .Should().Throw<ReserveAuthorizationException>();

        var underfunded = Proposal(new ReserveVersion(1), null, 1) with
        {
            AssetAllocations =
            [
                new ExternalReserveAsset("hard", ReserveBackingPurpose.HardCoin, 1),
                new ExternalReserveAsset("soft", ReserveBackingPurpose.SoftCoin, 1)
            ]
        };
        authority.ValidateAndActivate(underfunded, Now).Coverage.Should().Be(ReserveCoverageState.Shortfall);
        FluentActions.Invoking(() => authority.Authorize(new ReserveVersion(1), 1, Now))
            .Should().Throw<ReserveShortfallException>();
    }

    [Fact]
    public void AuthorizationTokenCannotBeConstructedFromDefaultOrInvalidAuthorityState()
    {
        FluentActions.Invoking(() => new ReservePostingAuthorization(default, 1, Now))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new ReservePostingAuthorization(new ReserveVersion(1), 0, Now))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void IssuanceAuthorization_CoversSoftCoinHeadroomAndIncrementalShortfall()
    {
        var authority = new CoreReserveAuthority();
        authority.ValidateAndActivate(Proposal(new ReserveVersion(1), null, 1), Now);

        authority.AuthorizeIssuance(
                new ReserveVersion(1), 1, new CoinAmount(CurrencyCode.SoftCoin, 1), Now)
            .Version.Should().Be(new ReserveVersion(1));
        FluentActions.Invoking(() => authority.AuthorizeIssuance(
                new ReserveVersion(1), 1, new CoinAmount(CurrencyCode.SoftCoin, long.MaxValue), Now))
            .Should().Throw<ReserveShortfallException>();
        FluentActions.Invoking(() => authority.AuthorizeIssuance(
                new ReserveVersion(1), 1, default, Now))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    private static ReserveProposal Proposal(
        ReserveVersion version,
        ReserveVersion? expected,
        long epoch) =>
        new(
            version,
            expected,
            new PolicyVersion(1),
            epoch,
            Now.AddMinutes(-1),
            Now.AddMinutes(5),
            new ReserveLiabilityPosition(100, 100_000, 100_000, 0),
            new ReserveBufferPosition(0, 0, 0, 0, 0, 0, 0),
            [new ReserveServiceObservation("render", 100_000, 10_000_000, 10_000_000, 10_000_000, 0, true, Now.AddMinutes(-1), Now.AddMinutes(5))],
            [
                new ExternalReserveAsset("hard", ReserveBackingPurpose.HardCoin, 2_000_000_000),
                new ExternalReserveAsset("soft", ReserveBackingPurpose.SoftCoin, 2_000_000_000)
            ],
            "treasury-evidence");

    public static TheoryData<ExternalReserveAsset> InvalidAssets =>
        new()
        {
            null!,
            new ExternalReserveAsset(" ", ReserveBackingPurpose.HardCoin, 1),
            new ExternalReserveAsset("asset", (ReserveBackingPurpose)99, 1),
            new ExternalReserveAsset("asset", ReserveBackingPurpose.HardCoin, 0)
        };
}
