using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Money;
using GameGuild.Finance.Economy.Persistence;

namespace GameGuild.Finance.Economy.UnitTests.Contracts;

public sealed class ContractTests
{
    [Fact]
    public void TypedIdentifiers_RejectEmptyValues()
    {
        FluentActions.Invoking(() => new WalletId(Guid.Empty)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new SourceStampId(Guid.Empty)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new CreditLotId(Guid.Empty)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new HoldId(Guid.Empty)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new PostingId(Guid.Empty)).Should().Throw<ArgumentException>();
        WalletId.New().Value.Should().NotBeEmpty();
        SourceStampId.New().Value.Should().NotBeEmpty();
        CreditLotId.New().Value.Should().NotBeEmpty();
        HoldId.New().Value.Should().NotBeEmpty();
        PostingId.New().Value.Should().NotBeEmpty();
    }

    [Fact]
    public void CoinAmount_IsTypedAndRejectsInvalidCurrency()
    {
        CoinAmount.From(new HardCoinAmount(3)).Should().Be(new CoinAmount(CurrencyCode.HardCoin, 3));
        CoinAmount.From(new SoftCoinAmount(4)).Should().Be(new CoinAmount(CurrencyCode.SoftCoin, 4));
        FluentActions.Invoking(() => new CoinAmount((CurrencyCode)99, 1)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new CoinAmount(CurrencyCode.HardCoin, -1)).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void VersionAndPostingContracts_RejectInvalidConstruction()
    {
        FluentActions.Invoking(() => new ReserveVersion(0)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new PolicyVersion(0)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new PostingTemplate((PostingTemplateKind)99, 1)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new PostingTemplate(PostingTemplateKind.Spend, 0)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new PostingLine(0, EntrySide.Debit, EconomyAccountCode.HardCoinReserve, new CoinAmount(CurrencyCode.HardCoin, 1), null, null, null))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new PostingLine(1, (EntrySide)99, EconomyAccountCode.HardCoinReserve, new CoinAmount(CurrencyCode.HardCoin, 1), null, null, null))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new PostingLine(1, EntrySide.Debit, (EconomyAccountCode)99, new CoinAmount(CurrencyCode.HardCoin, 1), null, null, null))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new PostingLine(1, EntrySide.Debit, EconomyAccountCode.HardCoinReserve, new CoinAmount(CurrencyCode.HardCoin, 0), null, null, null))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new PostingLine(1, EntrySide.Debit, EconomyAccountCode.HardCoinReserve, new CoinAmount(CurrencyCode.HardCoin, 1), null, null, (ProvenanceKind)99))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new PostingRequest(
            PostingId.New(), new PostingTemplate(PostingTemplateKind.Spend, 1), new IdempotencyKey("key"), (PostingAuthority)99,
            new ReserveVersion(1), new PolicyVersion(1), null, DateTimeOffset.UtcNow, []))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new PostingRequest(
            PostingId.New(), new PostingTemplate(PostingTemplateKind.Spend, 1), new IdempotencyKey("key"), PostingAuthority.WalletOwner,
            new ReserveVersion(1), new PolicyVersion(1), null, DateTimeOffset.UtcNow, null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WalletContract_HasIdentityAndLifecycleButNoAuthoritativeBalance()
    {
        var ownerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var createdAt = DateTimeOffset.Parse("2026-07-18T12:00:00Z");
        var walletId = WalletId.New();
        var wallet = new WalletContract(walletId, ownerId, tenantId, WalletLifecycleState.Active, createdAt);

        wallet.Id.Should().Be(walletId);
        wallet.OwnerId.Should().Be(ownerId);
        wallet.TenantId.Should().Be(tenantId);
        wallet.State.Should().Be(WalletLifecycleState.Active);
        wallet.CreatedAt.Should().Be(createdAt);
        typeof(WalletContract).GetProperty("Balance").Should().BeNull();
        FluentActions.Invoking(() => new WalletContract(WalletId.New(), Guid.Empty, tenantId, WalletLifecycleState.Active, createdAt))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new WalletContract(WalletId.New(), ownerId, Guid.Empty, WalletLifecycleState.Active, createdAt))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new WalletContract(WalletId.New(), ownerId, tenantId, (WalletLifecycleState)99, createdAt))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ReserveSnapshot_RequiresFreshOrderedEvidence()
    {
        var observed = DateTimeOffset.Parse("2026-07-18T12:00:00Z");
        var snapshot = new ReserveSnapshotContract(
            new ReserveVersion(3), observed, observed.AddMinutes(5), new HardCoinAmount(100), new SoftCoinAmount(10_000), " reserve-hash ");

        snapshot.EvidenceHash.Should().Be("reserve-hash");
        snapshot.Version.Should().Be(new ReserveVersion(3));
        snapshot.ObservedAt.Should().Be(observed);
        snapshot.ExpiresAt.Should().Be(observed.AddMinutes(5));
        snapshot.HardHeadroom.Should().Be(new HardCoinAmount(100));
        snapshot.SoftHeadroom.Should().Be(new SoftCoinAmount(10_000));
        FluentActions.Invoking(() => new ReserveSnapshotContract(
            new ReserveVersion(3), observed, observed, HardCoinAmount.Zero, SoftCoinAmount.Zero, "hash"))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new ReserveSnapshotContract(
            new ReserveVersion(3), observed, observed.AddMinutes(1), HardCoinAmount.Zero, SoftCoinAmount.Zero, " "))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MonetaryPolicy_IsEffectiveDatedAndUsesBoundedPpmRates()
    {
        var effective = DateTimeOffset.Parse("2026-07-18T12:00:00Z");
        var policy = new MonetaryPolicyContract(new PolicyVersion(2), effective, effective.AddDays(1), 10_000, 250_000);

        policy.ConversionFeePpm.Should().Be(10_000);
        policy.Version.Should().Be(new PolicyVersion(2));
        policy.EffectiveAt.Should().Be(effective);
        policy.EndsAt.Should().Be(effective.AddDays(1));
        policy.MinimumMarginPpm.Should().Be(250_000);
        FluentActions.Invoking(() => new MonetaryPolicyContract(new PolicyVersion(2), effective, effective, 0, 0))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new MonetaryPolicyContract(new PolicyVersion(2), effective, null, -1, 0))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new MonetaryPolicyContract(new PolicyVersion(2), effective, null, 0, 1_000_000))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void IdempotencyKey_RequiresBoundedNonWhitespaceValue()
    {
        FluentActions.Invoking(() => new IdempotencyKey(" ")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new IdempotencyKey(new string('a', 129))).Should().Throw<ArgumentException>();
        new IdempotencyKey(" checkout-17 ").Value.Should().Be("checkout-17");
    }

    [Fact]
    public void SourceStamp_RequiresValidConfirmationEvidence()
    {
        var observedAt = DateTimeOffset.Parse("2026-07-18T12:00:00Z");
        FluentActions.Invoking(() => new SourceStampContract(
            SourceStampId.New(), "hash", SourceConfirmationState.Confirmed, observedAt, null, "pi_1"))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new SourceStampContract(
            SourceStampId.New(), "hash", SourceConfirmationState.Observed, observedAt, observedAt.AddMinutes(1), "pi_1"))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new SourceStampContract(
            SourceStampId.New(), " ", SourceConfirmationState.Observed, observedAt, null, null))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new SourceStampContract(
            SourceStampId.New(), "hash", (SourceConfirmationState)99, observedAt, null, null))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new SourceStampContract(
            SourceStampId.New(), "hash", SourceConfirmationState.Confirmed, observedAt, observedAt.AddMinutes(-1), null))
            .Should().Throw<ArgumentException>();

        var confirmed = new SourceStampContract(
            SourceStampId.New(), " hash ", SourceConfirmationState.Confirmed, observedAt, observedAt.AddMinutes(1), " pi_1 ");
        confirmed.EvidenceHash.Should().Be("hash");
        confirmed.ProviderReference.Should().Be("pi_1");
    }

    [Fact]
    public void FragmentAllocation_RequiresPositiveBoundedRange()
    {
        var lot = CreditLotId.New();
        var wallet = WalletId.New();
        FluentActions.Invoking(() => new FragmentAllocationContract(lot, -1, 1, wallet)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new FragmentAllocationContract(lot, 0, 0, wallet)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new FragmentAllocationContract(lot, long.MaxValue, 2, wallet)).Should().Throw<OverflowException>();
        var allocation = new FragmentAllocationContract(lot, 4, 2, wallet);
        allocation.ParentLotId.Should().Be(lot);
        allocation.DestinationWalletId.Should().Be(wallet);
        allocation.EndExclusive.Should().Be(6);
    }

    [Fact]
    public void HoldContract_EnforcesStateTimestamps()
    {
        var now = DateTimeOffset.Parse("2026-07-18T12:00:00Z");
        var amount = CoinAmount.From(new HardCoinAmount(5));
        FluentActions.Invoking(() => new HoldContract(HoldId.New(), WalletId.New(), amount, HoldReason.RiskReview, HoldStatus.Active, now, now))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new HoldContract(HoldId.New(), WalletId.New(), amount, HoldReason.RiskReview, HoldStatus.Released, now, null))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new HoldContract(HoldId.New(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 0), HoldReason.RiskReview, HoldStatus.Active, now, null))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new HoldContract(HoldId.New(), WalletId.New(), amount, (HoldReason)99, HoldStatus.Active, now, null))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new HoldContract(HoldId.New(), WalletId.New(), amount, HoldReason.RiskReview, (HoldStatus)99, now, null))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new HoldContract(HoldId.New(), WalletId.New(), amount, HoldReason.RiskReview, HoldStatus.Released, now, now.AddMinutes(-1)))
            .Should().Throw<ArgumentException>();

        var active = new HoldContract(HoldId.New(), WalletId.New(), amount, HoldReason.RiskReview, HoldStatus.Active, now, null);
        var released = new HoldContract(HoldId.New(), WalletId.New(), amount, HoldReason.Dispute, HoldStatus.Released, now, now.AddMinutes(1));
        active.Status.Should().Be(HoldStatus.Active);
        released.ReleasedAt.Should().BeAfter(now);
    }

    [Fact]
    public void RootMint_RequiresConfirmedSourceAndMatchingPositiveAmount()
    {
        var now = DateTimeOffset.Parse("2026-07-18T12:00:00Z");
        var source = new SourceStampContract(SourceStampId.New(), "sha256", SourceConfirmationState.Observed, now, null, "pi_1");
        FluentActions.Invoking(() => new RootMintContract(
            null!, CreditLotId.New(), WalletId.New(), CoinAmount.From(new HardCoinAmount(10)),
            ProvenanceKind.PurchasedHard, now, null))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new RootMintContract(
            source, CreditLotId.New(), WalletId.New(), CoinAmount.From(new HardCoinAmount(10)),
            ProvenanceKind.PurchasedHard, now, null))
            .Should().Throw<ArgumentException>();

        var confirmedAt = now.AddMinutes(1);
        var confirmed = new SourceStampContract(SourceStampId.New(), "sha256", SourceConfirmationState.Confirmed, now, confirmedAt, "pi_1");
        var lotId = CreditLotId.New();
        var walletId = WalletId.New();
        FluentActions.Invoking(() => new RootMintContract(confirmed, CreditLotId.New(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 0), ProvenanceKind.PurchasedHard, confirmedAt, null))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new RootMintContract(confirmed, CreditLotId.New(), WalletId.New(), CoinAmount.From(new HardCoinAmount(10)), (ProvenanceKind)99, confirmedAt, null))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new RootMintContract(confirmed, CreditLotId.New(), WalletId.New(), CoinAmount.From(new HardCoinAmount(10)), ProvenanceKind.PurchasedHard, confirmedAt.AddMinutes(1), null))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new RootMintContract(confirmed, CreditLotId.New(), WalletId.New(), CoinAmount.From(new HardCoinAmount(10)), ProvenanceKind.PurchasedHard, confirmedAt, confirmedAt.AddMinutes(-1)))
            .Should().Throw<ArgumentException>();

        var mint = new RootMintContract(confirmed, lotId, walletId, CoinAmount.From(new HardCoinAmount(10)), ProvenanceKind.PurchasedHard, confirmedAt, null);
        mint.LotId.Should().Be(lotId);
        mint.WalletId.Should().Be(walletId);
        mint.Amount.Units.Should().Be(10);
        mint.Source.Should().BeSameAs(confirmed);
        mint.Provenance.Should().Be(ProvenanceKind.PurchasedHard);
        mint.ConfirmedAt.Should().Be(confirmedAt);
        mint.MaturesAt.Should().BeNull();
    }

    [Fact]
    public void FragmentLineage_RequiresParentsAndCopiesInput()
    {
        var allocation = new FragmentAllocationContract(CreditLotId.New(), 0, 5, WalletId.New());
        var parents = new List<FragmentAllocationContract> { allocation };
        var outputLotId = CreditLotId.New();
        var lineage = new FragmentLineageContract(outputLotId, parents);
        parents.Clear();

        lineage.OutputLotId.Should().Be(outputLotId);
        lineage.Parents.Should().ContainSingle().Which.Should().Be(allocation);
        FluentActions.Invoking(() => new FragmentLineageContract(CreditLotId.New(), null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new FragmentLineageContract(CreditLotId.New(), [])).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PublicContracts_ExposeNoMutableBalanceSetter()
    {
        var balanceProperties = typeof(EconomyModule).Assembly.GetExportedTypes()
            .SelectMany(type => type.GetProperties())
            .Where(property => property.Name.Contains("Balance", StringComparison.OrdinalIgnoreCase));

        balanceProperties.All(property => property.SetMethod == null).Should().BeTrue();
    }

    [Fact]
    public void EconomyAssembly_OwnsPersistenceMappingsButNoHttpSurface()
    {
        var assembly = typeof(EconomyModule).Assembly;

        assembly.GetExportedTypes().Should().Contain(typeof(EconomyModelConfiguration));
        assembly.GetExportedTypes()
            .Should().NotContain(type => type.Name.EndsWith("Controller", StringComparison.Ordinal) || type.Name.EndsWith("Endpoint", StringComparison.Ordinal));
    }
}
