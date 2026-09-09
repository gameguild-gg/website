using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Finance.Economy.UnitTests.Funding;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Reflection;

namespace GameGuild.Finance.Economy.UnitTests.Persistence;

public sealed class PostgreSqlGatewayValidationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 23, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConversionRejectsInvalidFeeAndDispatchBindingsBeforePersistence()
    {
        using var context = CreateContext();
        var gateway = new PostgreSqlHardToSoftConversionGateway(context);
        var authority = Authority();

        var inconsistentNoFee = Conversion(feeUnits: 0, feePostingId: PostingId.New());
        var inconsistentFee = Conversion(feeUnits: 1, feePostingId: default);
        var oversizedSnapshot = Conversion();

        Action noFee = () => gateway.Convert(new PersistedHardToSoftConversion(inconsistentNoFee, authority));
        Action fee = () => gateway.Convert(new PersistedHardToSoftConversion(inconsistentFee, authority));
        Action snapshot = () => gateway.Convert(new PersistedHardToSoftConversion(
            oversizedSnapshot,
            authority,
            new string('a', 129)));

        noFee.Should().Throw<ArgumentException>().WithMessage("*fee posting ID*");
        fee.Should().Throw<ArgumentException>().WithMessage("*fee posting ID*");
        snapshot.Should().Throw<ArgumentException>().WithMessage("*Dispatch snapshot hashes*");
    }

    [Fact]
    public void ConversionRejectsAnAuthorizationWithoutSourceRoots()
    {
        using var context = CreateContext();
        var gateway = new PostgreSqlHardToSoftConversionGateway(context);
        var command = Conversion(sourceRoots: []);

        Action act = () => gateway.Convert(new PersistedHardToSoftConversion(command, Authority()));

        act.Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*explicit source-root authorization*");
    }

    [Fact]
    public void ConversionTranslatesRelationalWriterFailures()
    {
        using var context = CreateContext();
        var gateway = new PostgreSqlHardToSoftConversionGateway(context);

        Action act = () => gateway.Convert(new PersistedHardToSoftConversion(Conversion(), Authority()));

        act.Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*conversion writer rejected*");
    }

    [Fact]
    public void FundingObservationTranslatesRelationalWriterFailures()
    {
        using var context = CreateContext();
        var gateway = new PostgreSqlHardCoinFundingGateway(context);
        var request = new PersistedHardCoinFundingObservation(
            new ObserveHardCoinTopUpCommand(
                new SourceStampId(Guid.NewGuid()),
                new WalletId(Guid.NewGuid()),
                new ProviderMonetaryLeg("stripe", "test", "platform", "payment", "principal"),
                "provider-observation",
                100,
                Now),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new PolicyVersion(1));

        Action act = () => gateway.Observe(request);

        act.Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*funding writer rejected the observed provider fact*");
    }

    [Fact]
    public void FundingObservationRejectsMissingActorTenantOrPolicyBeforeWriting()
    {
        using var context = CreateContext();
        var gateway = new PostgreSqlHardCoinFundingGateway(context);
        var request = FundingObservation();

        FluentActions.Invoking(() => gateway.Observe(request with { ActorId = Guid.Empty }))
            .Should().Throw<ArgumentException>().WithMessage("*Actor ID is required*");
        FluentActions.Invoking(() => gateway.Observe(request with { TenantId = Guid.Empty }))
            .Should().Throw<ArgumentException>().WithMessage("*Tenant ID is required*");
        FluentActions.Invoking(() => gateway.Observe(request with { PolicyVersion = default }))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FundingConfirmationRejectsAnUnknownObservedSource()
    {
        using var context = CreateContext();
        var gateway = new PostgreSqlHardCoinFundingGateway(context);
        var source = new SourceStampId(Guid.NewGuid());
        var key = new IdempotencyKey("missing-funding-source");
        var command = new ConfirmObservedTopUpCommand(
            PostingId.New(),
            key,
            source,
            CreditLotId.New(),
            new ReserveVersion(1),
            new PolicyVersion(1),
            "provider-confirmation",
            Now,
            FundingAuthorizationFixture.Create(
                PostingTemplateKind.ConfirmedTopUpMint,
                key,
                new WalletId(Guid.NewGuid()),
                new CoinAmount(CurrencyCode.HardCoin, 100),
                [source],
                Now,
                new CoinAmount(CurrencyCode.HardCoin, 100)));

        Action act = () => gateway.Confirm(new PersistedHardCoinFundingConfirmation(command, Authority()));

        act.Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*observed funding source was not found*");
    }

    [Fact]
    public void FundingConfirmationRejectsAnObservedSourceWithoutItsFundingClaim()
    {
        using var context = CreateContext();
        var source = new SourceStampId(Guid.NewGuid());
        context.Set<EconomySourceStampRow>().Add(new EconomySourceStampRow
        {
            Id = source.Value,
            EvidenceHash = "observed-evidence",
            ObservedAt = Now
        });
        context.SaveChanges();
        var key = new IdempotencyKey("missing-funding-claim");
        var command = new ConfirmObservedTopUpCommand(
            PostingId.New(), key, source, CreditLotId.New(), new ReserveVersion(1), new PolicyVersion(1),
            "provider-confirmation", Now,
            FundingAuthorizationFixture.Create(
                PostingTemplateKind.ConfirmedTopUpMint, key, WalletId.New(),
                new CoinAmount(CurrencyCode.HardCoin, 100), [source], Now,
                new CoinAmount(CurrencyCode.HardCoin, 100)));

        FluentActions.Invoking(() => new PostgreSqlHardCoinFundingGateway(context)
                .Confirm(new PersistedHardCoinFundingConfirmation(command, Authority())))
            .Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*observed funding claim was not found*");
    }

    [Fact]
    public void TransferRejectsInvalidBindingsAndTranslatesRelationalWriterFailures()
    {
        using var context = CreateContext();
        var gateway = new PostgreSqlFifoTransferGateway(context);
        var authority = Authority();
        var wallet = new WalletId(Guid.NewGuid());
        var sameWallet = Transfer(sourceWallet: wallet, destinationWallet: wallet);
        var invalidProvenance = Transfer(provenance: (ProvenanceKind)999);
        var oversizedSnapshot = Transfer();

        Action sameWalletAct = () => gateway.Transfer(new PersistedFifoTransferRequest(sameWallet, authority));
        Action invalidProvenanceAct = () => gateway.Transfer(new PersistedFifoTransferRequest(invalidProvenance, authority));
        Action snapshotAct = () => gateway.Transfer(new PersistedFifoTransferRequest(
            oversizedSnapshot,
            authority,
            new string('a', 129)));
        Action writerAct = () => gateway.Transfer(new PersistedFifoTransferRequest(Transfer(), authority));

        sameWalletAct.Should().Throw<ArgumentException>().WithMessage("*must differ*");
        invalidProvenanceAct.Should().Throw<ArgumentOutOfRangeException>();
        snapshotAct.Should().Throw<ArgumentException>().WithMessage("*Dispatch snapshot hashes*");
        writerAct.Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*FIFO transfer writer rejected*");
    }

    [Fact]
    public void ProviderReversalRejectsInvalidBindingsAndTranslatesRelationalWriterFailures()
    {
        using var context = CreateContext();
        var gateway = new PostgreSqlProviderReversalGateway(context);
        var authority = Authority();
        var oversizedSnapshot = Reversal();
        var invalidDisposition = Reversal(disposition: (ProviderReversalDisposition)999);

        Action snapshotAct = () => gateway.Reverse(new PersistedProviderReversal(
            oversizedSnapshot,
            authority,
            new string('a', 129)));
        Action dispositionAct = () => gateway.Reverse(new PersistedProviderReversal(invalidDisposition, authority));
        Action writerAct = () => gateway.Reverse(new PersistedProviderReversal(Reversal(), authority));

        snapshotAct.Should().Throw<ArgumentException>().WithMessage("*Dispatch snapshot hashes*");
        dispositionAct.Should().Throw<ArgumentOutOfRangeException>();
        writerAct.Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*provider reversal writer rejected*");
    }

    [Fact]
    public void FifoReservationRejectsCurrencyMismatchAndTranslatesWriterFailures()
    {
        using var context = CreateContext();
        var gateway = new PostgreSqlFifoFragmentReservationGateway(context);
        var valid = Reservation();
        var mismatch = valid with { Amount = new CoinAmount(CurrencyCode.SoftCoin, valid.Amount.Units) };

        Action mismatchAct = () => gateway.Reserve(mismatch);
        Action reserveAct = () => gateway.Reserve(valid);
        Action transitionAct = () => gateway.Transition(
            valid.OperationId,
            PersistedFragmentReservationStatus.Reserved,
            PersistedFragmentReservationStatus.Consumed,
            Now);

        mismatchAct.Should().Throw<ArgumentException>().WithMessage("*must match its amount*");
        reserveAct.Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*FIFO reservation writer rejected*");
        transitionAct.Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*reservation transition was rejected*");
    }

    [Fact]
    public void FifoReservationRejectsInvalidOperationPurposeAmountAndTransitionStates()
    {
        using var context = CreateContext();
        var gateway = new PostgreSqlFifoFragmentReservationGateway(context);
        var valid = Reservation();

        FluentActions.Invoking(() => gateway.Reserve(valid with { OperationId = Guid.Empty }))
            .Should().Throw<ArgumentException>().WithMessage("*Operation ID is required*");
        FluentActions.Invoking(() => gateway.Reserve(valid with { Purpose = (PersistedFragmentReservationPurpose)999 }))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => gateway.Reserve(valid with
            { Amount = new CoinAmount(CurrencyCode.HardCoin, 0) }))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => gateway.Reserve(valid with
            { Purpose = PersistedFragmentReservationPurpose.BountyEscrow }))
            .Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*FIFO reservation writer rejected*");
        FluentActions.Invoking(() => gateway.Transition(
                Guid.Empty, PersistedFragmentReservationStatus.Reserved,
                PersistedFragmentReservationStatus.Released, Now))
            .Should().Throw<ArgumentException>().WithMessage("*Operation ID is required*");
        FluentActions.Invoking(() => gateway.Transition(
                valid.OperationId, (PersistedFragmentReservationStatus)999,
                PersistedFragmentReservationStatus.Released, Now))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => gateway.Transition(
                valid.OperationId, PersistedFragmentReservationStatus.Reserved,
                (PersistedFragmentReservationStatus)999, Now))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => gateway.Read(Guid.Empty, PersistedFragmentReservationStatus.Reserved))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => gateway.Read(valid.OperationId, (PersistedFragmentReservationStatus)999))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => gateway.Read(
                valid.OperationId, PersistedFragmentReservationStatus.Reserved))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RegisteredPostingRequestAndAuthorityRejectInvalidBoundaryContracts()
    {
        var authority = Authority();
        var posting = Posting(lines: []);
        var duplicateLines = Posting(lines:
        [
            Line(1),
            Line(1)
        ]);
        var validPosting = Posting(lines: [Line(1)]);
        var allocation = new RegisteredPostingAllocation(
            2,
            CreditLotId.New(),
            1,
            [new RootTraceRange(new SourceStampId(Guid.NewGuid()), 0, 1, 0)]);

        Action emptyLines = () => _ = new RegisteredPostingRequest(authority, posting);
        Action duplicates = () => _ = new RegisteredPostingRequest(authority, duplicateLines);
        Action invalidAllocation = () => _ = new RegisteredPostingRequest(authority, validPosting, [allocation]);
        Action oversizedSnapshot = () => _ = new RegisteredPostingRequest(
            authority,
            validPosting,
            dispatchSnapshotHash: new string('a', 129));
        Action emptyAuthority = () => _ = new RegisteredPostingAuthority(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "binding",
            1);

        emptyLines.Should().Throw<ArgumentException>().WithMessage("*requires journal lines*");
        duplicates.Should().Throw<ArgumentException>().WithMessage("*must be unique*");
        invalidAllocation.Should().Throw<ArgumentException>().WithMessage("*must target a posting line*");
        oversizedSnapshot.Should().Throw<ArgumentException>().WithMessage("*Dispatch snapshot hashes*");
        emptyAuthority.Should().Throw<ArgumentException>().WithMessage("*Capability ID is required*");
    }

    [Fact]
    public void RegisteredPostingRejectsAnUnprovisionedAccountBeforeSqlExecution()
    {
        using var context = CreateContext();
        var gateway = new PostgreSqlRegisteredPostingGateway(context);
        var request = new RegisteredPostingRequest(Authority(), Posting(lines: [Line(1)]));

        Action act = () => gateway.Post(request);

        act.Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*account that is not provisioned*");
    }

    [Fact]
    public void RegisteredPostingTranslatesWriterFailuresAfterResolvingProvisionedAccounts()
    {
        using var context = CreateContext();
        var posting = Posting(lines: [Line(1)]);
        var line = posting.Lines.Single();
        context.Set<EconomyAccountRow>().Add(new EconomyAccountRow
        {
            Id = Guid.NewGuid(),
            WalletId = line.WalletId!.Value.Value,
            Code = line.Account,
            Currency = line.Amount.Currency,
            Provenance = line.Provenance,
            CreatedAt = Now
        });
        context.SaveChanges();
        var request = new RegisteredPostingRequest(Authority(), posting);

        var sourceBoundPosting = posting with
        {
            Source = new SourceStampContract(
                SourceStampId.New(),
                "source-bound-posting",
                SourceConfirmationState.Confirmed,
                Now.AddMinutes(-1),
                Now,
                "source-bound")
        };
        Action act = () => new PostgreSqlRegisteredPostingGateway(context).Post(request);
        Action sourceBoundAct = () => new PostgreSqlRegisteredPostingGateway(context)
            .Post(new RegisteredPostingRequest(Authority(), sourceBoundPosting));

        act.Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*registered economy writer rejected the posting*");
        sourceBoundAct.Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*registered economy writer rejected the posting*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegisteredBountyEscrowUsesV2WriterBeforeTranslatingRelationalFailures(bool includeSource)
    {
        using var context = CreateContext();
        var wallet = WalletId.New();
        PostingLine[] lines =
        [
            new PostingLine(
                1,
                EntrySide.Debit,
                EconomyAccountCode.PurchasedHardLiability,
                new CoinAmount(CurrencyCode.HardCoin, 10),
                wallet,
                null,
                ProvenanceKind.PurchasedHard),
            new PostingLine(
                2,
                EntrySide.Credit,
                EconomyAccountCode.HardCoinEscrow,
                new CoinAmount(CurrencyCode.HardCoin, 10),
                null,
                null,
                null)
        ];
        foreach (var line in lines)
        {
            context.Set<EconomyAccountRow>().Add(new EconomyAccountRow
            {
                Id = Guid.NewGuid(),
                WalletId = line.WalletId?.Value,
                Code = line.Account,
                Currency = line.Amount.Currency,
                Provenance = line.Provenance,
                CreatedAt = Now
            });
        }
        context.SaveChanges();

        var source = includeSource
            ? new SourceStampContract(
                SourceStampId.New(),
                "bounty-escrow-source",
                SourceConfirmationState.Confirmed,
                Now.AddMinutes(-1),
                Now,
                "bounty-escrow")
            : null;
        var posting = new PostingRequest(
            PostingId.New(),
            new PostingTemplate(PostingTemplateKind.BountyEscrow, PostingTemplate.CurrentVersion),
            new IdempotencyKey($"bounty-escrow-{includeSource}"),
            PostingAuthority.WalletOwner,
            new ReserveVersion(1),
            new PolicyVersion(1),
            source,
            Now,
            lines);

        Action act = () => new PostgreSqlRegisteredPostingGateway(context)
            .Post(new RegisteredPostingRequest(Authority(), posting));

        act.Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*registered economy writer rejected the posting*");
    }

    [Fact]
    public void RegisteredPostingClassifiesEveryDatabaseFailureShape()
    {
        var method = typeof(PostgreSqlRegisteredPostingGateway).GetMethod(
            "IsDatabaseFailure",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        bool Classifies(Exception exception) => (bool)method.Invoke(null, [exception])!;

        Classifies(new TestDbException()).Should().BeTrue();
        Classifies(new DbUpdateException()).Should().BeTrue();
        Classifies(new InvalidOperationException()).Should().BeTrue();
        Classifies(new Exception("wrapper", new TestDbException())).Should().BeTrue();
        Classifies(new ArgumentException()).Should().BeFalse();
    }

    [Fact]
    public void DurableGatewaysRejectNonRelationalContextsAndClassifyDatabaseFailures()
    {
        var context = DispatchProxy.Create<IApplicationDbContext, NonRelationalApplicationDbContextProxy>();
        Action[] construct =
        [
            () => _ = new PostgreSqlHardCoinFundingGateway(context),
            () => _ = new PostgreSqlHardToSoftConversionGateway(context),
            () => _ = new PostgreSqlProviderReversalGateway(context),
            () => _ = new PostgreSqlFifoFragmentReservationGateway(context),
            () => _ = new PostgreSqlFifoTransferGateway(context),
            () => _ = new PostgreSqlRegisteredPostingGateway(context),
            () => _ = new PostgreSqlRiskDecisionAuthorizer(context)
        ];
        var gatewayTypes = new[]
        {
            typeof(PostgreSqlHardCoinFundingGateway),
            typeof(PostgreSqlHardToSoftConversionGateway),
            typeof(PostgreSqlProviderReversalGateway),
            typeof(PostgreSqlFifoFragmentReservationGateway),
            typeof(PostgreSqlFifoTransferGateway),
            typeof(PostgreSqlAdRewardIssuanceGateway),
            typeof(PostgreSqlMarketplaceLedgerGateway),
            typeof(PostgreSqlEconomyWalletProvisioner),
            typeof(PostgreSqlLegacyBalanceBackfillGateway)
        };

        foreach (var create in construct)
            create.Should().Throw<InvalidOperationException>().WithMessage("*relational DbContext*");
        foreach (var gatewayType in gatewayTypes)
            AssertDatabaseFailureClassifier(gatewayType);
    }

    [Fact]
    public void FundingConfirmationRejectsAnUnprovisionedAccountBeforeWriting()
    {
        using var context = CreateContext();
        var source = new SourceStampId(Guid.NewGuid());
        var wallet = new WalletId(Guid.NewGuid());
        var key = new IdempotencyKey("funding-unprovisioned-account");
        context.Set<EconomySourceStampRow>().Add(new EconomySourceStampRow
        {
            Id = source.Value,
            EvidenceHash = "observed-evidence",
            ObservedAt = Now
        });
        context.Set<EconomyFundingClaimRow>().Add(new EconomyFundingClaimRow
        {
            SourceStampId = source.Value,
            WalletId = wallet.Value,
            Provider = "stripe",
            Environment = "test",
            ConnectedAccount = "platform",
            ProviderObject = "payment",
            ProviderMonetaryLeg = "principal",
            AuthoritativeUsdMinorUnits = 100,
            ObservedAt = Now,
            Version = 1
        });
        context.SaveChanges();
        var command = new ConfirmObservedTopUpCommand(
            PostingId.New(),
            key,
            source,
            CreditLotId.New(),
            new ReserveVersion(1),
            new PolicyVersion(1),
            "provider-confirmation",
            Now,
            FundingAuthorizationFixture.Create(
                PostingTemplateKind.ConfirmedTopUpMint,
                key,
                wallet,
                new CoinAmount(CurrencyCode.HardCoin, 100),
                [source],
                Now));

        Action act = () => new PostgreSqlHardCoinFundingGateway(context)
            .Confirm(new PersistedHardCoinFundingConfirmation(command, Authority()));

        act.Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*economy account that is not provisioned*");
    }

    [Fact]
    public void FundingConfirmationTranslatesRelationalWriterFailuresAfterResolvingAccounts()
    {
        using var context = CreateContext();
        var source = new SourceStampId(Guid.NewGuid());
        var wallet = new WalletId(Guid.NewGuid());
        var key = new IdempotencyKey("funding-relational-writer-failure");
        context.Set<EconomySourceStampRow>().Add(new EconomySourceStampRow
        {
            Id = source.Value,
            EvidenceHash = "observed-evidence",
            ObservedAt = Now
        });
        context.Set<EconomyFundingClaimRow>().Add(new EconomyFundingClaimRow
        {
            SourceStampId = source.Value,
            WalletId = wallet.Value,
            Provider = "stripe",
            Environment = "test",
            ConnectedAccount = "platform",
            ProviderObject = "payment",
            ProviderMonetaryLeg = "principal",
            AuthoritativeUsdMinorUnits = 100,
            ObservedAt = Now,
            Version = 1
        });
        context.Set<EconomyAccountRow>().AddRange(
            new EconomyAccountRow
            {
                Id = Guid.NewGuid(),
                Code = EconomyAccountCode.ExternalClearingHard,
                Currency = CurrencyCode.HardCoin,
                CreatedAt = Now
            },
            new EconomyAccountRow
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Value,
                Code = EconomyAccountCode.PurchasedHardLiability,
                Currency = CurrencyCode.HardCoin,
                Provenance = ProvenanceKind.PurchasedHard,
                CreatedAt = Now
            });
        context.SaveChanges();
        var command = new ConfirmObservedTopUpCommand(
            PostingId.New(),
            key,
            source,
            CreditLotId.New(),
            new ReserveVersion(1),
            new PolicyVersion(1),
            "provider-confirmation",
            Now,
            FundingAuthorizationFixture.Create(
                PostingTemplateKind.ConfirmedTopUpMint,
                key,
                wallet,
                new CoinAmount(CurrencyCode.HardCoin, 100),
                [source],
                Now));

        Action act = () => new PostgreSqlHardCoinFundingGateway(context)
            .Confirm(new PersistedHardCoinFundingConfirmation(command, Authority()));

        act.Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*funding writer rejected the confirmation*");
    }

    [Fact]
    public void PersistedEconomyRecords_ExposeEveryPersistenceResult()
    {
        var operation = Guid.NewGuid();
        var lot = CreditLotId.New();
        var root = SourceStampId.New();
        var reservation = new PersistedFragmentReservation(
            Guid.NewGuid(),
            operation,
            lot,
            root,
            2,
            new RootTraceRange(root, 0, 1_000, 2),
            new CoinAmount(CurrencyCode.HardCoin, 1));
        var reversal = new PersistedProviderReversalReceipt(
            PostingId.New(),
            1,
            2,
            3,
            4,
            true);

        reservation.OperationId.Should().Be(operation);
        reservation.ParentLotId.Should().Be(lot);
        reservation.RootSourceStampId.Should().Be(root);
        reservation.ReversalEpoch.Should().Be(2);
        reservation.Range.Should().Be(new RootTraceRange(root, 0, 1_000, 2));
        reversal.OperationId.Should().NotBe(default);
        reversal.RecoveredHardUnits.Should().Be(1);
        reversal.RecoveredConvertedSoftUnits.Should().Be(2);
        reversal.ResponsibleDebtHardUnits.Should().Be(3);
        reversal.PlatformLossHardUnits.Should().Be(4);
        reversal.IsDuplicate.Should().BeTrue();
    }

    [Fact]
    public void LegacyBackfillGatewayRejectsUnsafeRequestsAndTranslatesDatabaseFailures()
    {
        using var context = CreateContext();
        var gateway = new PostgreSqlLegacyBalanceBackfillGateway(context);
        var request = LegacyBackfillRequest();

        FluentActions.Invoking(() => gateway.Post(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gateway.Post(request with { Authority = null! }))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gateway.Post(request with { CapabilityReceipt = null! }))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gateway.Post(request with { LegacyWalletId = Guid.Empty }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => gateway.Post(request with { HardUnits = 0 }))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => gateway.Post(request with { SnapshotHash = " " }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => gateway.Post(request with { ProviderHash = " " }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => gateway.Post(request with { DestinationHash = " " }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => gateway.Post(request with { SourceRootHash = " " }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => gateway.Post(request with
            {
                CapabilityReceipt = request.CapabilityReceipt with
                {
                    Capability = EconomyValueMovementCapability.PayoutExecution
                }
            }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => gateway.Post(request))
            .Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*legacy-balance writer rejected*");
    }

    [Fact]
    public void LegacyBackfillGatewayRequiresRelationalContextAndClassifiesDatabaseFailures()
    {
        FluentActions.Invoking(() => new PostgreSqlLegacyBalanceBackfillGateway(null!))
            .Should().Throw<ArgumentNullException>();
        var proxy = DispatchProxy.Create<IApplicationDbContext, NonRelationalApplicationDbContextProxy>();
        FluentActions.Invoking(() => new PostgreSqlLegacyBalanceBackfillGateway(proxy))
            .Should().Throw<InvalidOperationException>();
        AssertDatabaseFailureClassifier(typeof(PostgreSqlLegacyBalanceBackfillGateway));
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static void AssertDatabaseFailureClassifier(Type gatewayType)
    {
        var method = gatewayType.GetMethod("IsDatabaseFailure", BindingFlags.NonPublic | BindingFlags.Static)!;
        bool Classifies(Exception exception) => (bool)method.Invoke(null, [exception])!;

        Classifies(new TestDbException()).Should().BeTrue();
        Classifies(new DbUpdateException()).Should().BeTrue();
        Classifies(new InvalidOperationException()).Should().BeTrue();
        Classifies(new Exception("wrapper", new TestDbException())).Should().BeTrue();
        Classifies(new ArgumentException()).Should().BeFalse();
    }

    private static PersistedHardCoinFundingObservation FundingObservation() => new(
        new ObserveHardCoinTopUpCommand(
            new SourceStampId(Guid.NewGuid()),
            new WalletId(Guid.NewGuid()),
            new ProviderMonetaryLeg("stripe", "test", "platform", "payment", "principal"),
            "provider-observation",
            100,
            Now),
        Guid.NewGuid(),
        Guid.NewGuid(),
        new PolicyVersion(1));

    private static RegisteredPostingAuthority Authority() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "durable-gateway-test",
        1);

    private static LegacyBalanceBackfillPostingRequest LegacyBackfillRequest()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var riskDecisionId = Guid.NewGuid();
        var receipt = new CapabilityAuthorizationReceipt(
            Guid.NewGuid(), tenantId, actorId, "legacy-wallet", "BR",
            EconomyValueMovementCapability.LegacyBalanceBackfill, "legacy-backfill", 1, 1,
            riskDecisionId, 0, "provider", "destination", ["source"], ["evidence"],
            Now, Now.AddMinutes(5), "receipt", "key", "signature");
        return new LegacyBalanceBackfillPostingRequest(
            new RegisteredPostingAuthority(Guid.NewGuid(), actorId, tenantId, riskDecisionId, "legacy-backfill", 1),
            receipt,
            Guid.NewGuid(),
            new WalletId(Guid.NewGuid()),
            new SourceStampId(Guid.NewGuid()),
            PostingId.New(),
            CreditLotId.New(),
            new IdempotencyKey($"legacy-{Guid.NewGuid():N}"),
            1,
            "snapshot",
            "provider",
            "destination",
            "source",
            Now);
    }

    private static ConvertHardToSoftCommand Conversion(
        long feeUnits = 0,
        PostingId? feePostingId = null,
        IReadOnlyList<SourceStampId>? sourceRoots = null)
    {
        var key = new IdempotencyKey($"conversion-{Guid.NewGuid():N}");
        var wallet = new WalletId(Guid.NewGuid());
        var roots = sourceRoots ?? [new SourceStampId(Guid.NewGuid())];
        return new ConvertHardToSoftCommand(
            PostingId.New(),
            feePostingId ?? default,
            key,
            wallet,
            CreditLotId.New(),
            10,
            feeUnits,
            new ReserveVersion(1),
            new PolicyVersion(1),
            Now,
            FundingAuthorizationFixture.Create(
                PostingTemplateKind.HardToSoftConversion,
                key,
                wallet,
                new CoinAmount(CurrencyCode.HardCoin, 10 + feeUnits),
                roots,
                Now,
                new CoinAmount(CurrencyCode.SoftCoin, 10_000)));
    }

    private static TransferFragmentsCommand Transfer(
        WalletId? sourceWallet = null,
        WalletId? destinationWallet = null,
        CurrencyCode currency = CurrencyCode.HardCoin,
        ProvenanceKind provenance = ProvenanceKind.PurchasedHard) => new(
        PostingId.New(),
        new IdempotencyKey($"transfer-{Guid.NewGuid():N}"),
        sourceWallet ?? new WalletId(Guid.NewGuid()),
        destinationWallet ?? new WalletId(Guid.NewGuid()),
        new CoinAmount(currency, 1),
        provenance,
        new ReserveVersion(1),
        new PolicyVersion(1),
        Now);

    private static ReverseTopUpCommand Reversal(
        ProviderReversalDisposition disposition = ProviderReversalDisposition.ResponsibleDebt) => new(
        PostingId.New(),
        new IdempotencyKey($"reversal-{Guid.NewGuid():N}"),
        new SourceStampId(Guid.NewGuid()),
        1,
        disposition,
        "provider-evidence",
        new ReserveVersion(1),
        new PolicyVersion(1),
        Now);

    private static FifoFragmentReservationRequest Reservation() => new(
        Guid.NewGuid(),
        new WalletId(Guid.NewGuid()),
        CurrencyCode.HardCoin,
        ProvenanceKind.PurchasedHard,
        new CoinAmount(CurrencyCode.HardCoin, 1),
        PersistedFragmentReservationPurpose.HardToSoftConversion,
        Now);

    private static PostingRequest Posting(IReadOnlyCollection<PostingLine> lines) => new(
        PostingId.New(),
        new PostingTemplate(PostingTemplateKind.Spend, PostingTemplate.CurrentVersion),
        new IdempotencyKey($"posting-{Guid.NewGuid():N}"),
        PostingAuthority.WalletOwner,
        new ReserveVersion(1),
        new PolicyVersion(1),
        null,
        Now,
        lines);

    private static PostingLine Line(int sequence) => new(
        sequence,
        EntrySide.Debit,
        EconomyAccountCode.SoftCoinLiability,
        new CoinAmount(CurrencyCode.SoftCoin, 1),
        new WalletId(Guid.NewGuid()),
        null,
        ProvenanceKind.ConvertedSoft);

    private sealed class TestDbException : DbException;

    private class NonRelationalApplicationDbContextProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException();
    }
}
