using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Finance.Economy.Transfers;
using Moq;

namespace GameGuild.Finance.Economy.UnitTests.Transfers;

public sealed class SelfServiceEconomyTransferValidationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 16, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("a3000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("a3000000-0000-0000-0000-000000000002");
    private static readonly Guid RecipientId = Guid.Parse("a3000000-0000-0000-0000-000000000003");
    private static readonly WalletId SourceWallet = new(Guid.Parse("a3000000-0000-0000-0000-000000000004"));
    private static readonly WalletId DestinationWallet = new(Guid.Parse("a3000000-0000-0000-0000-000000000005"));
    private static readonly PostingId PostingId = new(Guid.Parse("a3000000-0000-0000-0000-000000000006"));

    [Fact]
    public void IntentStore_RequiresARelationalContext()
    {
        var context = new Mock<IApplicationDbContext>().Object;

        var create = () => new PostgreSqlSelfServiceEconomyTransferIntentStore(context);

        create.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SourceRootPlanner_RequiresARelationalContext()
    {
        var context = new Mock<IApplicationDbContext>().Object;

        var create = () => new PostgreSqlSelfServiceEconomyTransferSourceRootPlanner(context);

        create.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void IntentValidation_RejectsEveryIncompleteActorBinding()
    {
        var valid = Draft();
        SelfServiceEconomyTransferIntentDraft? missing = null;
        var nullValidation = () => PostgreSqlSelfServiceEconomyTransferIntentStore.Validate(missing!);
        nullValidation.Should().Throw<ArgumentNullException>();
        var invalid = new[]
        {
            valid with { TenantId = Guid.Empty },
            valid with { ActorId = Guid.Empty },
            valid with { RecipientUserId = Guid.Empty },
            valid with { RecipientUserId = ActorId }
        };

        foreach (var draft in invalid)
        {
            var validate = () => PostgreSqlSelfServiceEconomyTransferIntentStore.Validate(draft);
            validate.Should().Throw<ArgumentException>();
        }
    }

    [Fact]
    public void IntentValidation_RejectsEveryInvalidEnumAndCurrencyProvenancePair()
    {
        var valid = Draft();
        var invalidEnums = new[]
        {
            valid with { TransferType = (SelfServiceEconomyTransferType)999 },
            valid with { Currency = (CurrencyCode)999 },
            valid with { Provenance = (ProvenanceKind)999 }
        };
        foreach (var draft in invalidEnums)
        {
            var validate = () => PostgreSqlSelfServiceEconomyTransferIntentStore.Validate(draft);
            validate.Should().Throw<ArgumentOutOfRangeException>();
        }

        var invalidPairs = new[]
        {
            valid with { Provenance = ProvenanceKind.EarnedHard },
            valid with { Currency = CurrencyCode.SoftCoin, Provenance = ProvenanceKind.PurchasedHard }
        };
        foreach (var draft in invalidPairs)
        {
            var validate = () => PostgreSqlSelfServiceEconomyTransferIntentStore.Validate(draft);
            validate.Should().Throw<ArgumentException>();
        }

        PostgreSqlSelfServiceEconomyTransferIntentStore.Validate(
            valid with { Currency = CurrencyCode.SoftCoin, Provenance = ProvenanceKind.ConvertedSoft });
    }

    [Fact]
    public void IntentValidation_RequiresPositiveUnitsAndServerTime()
    {
        var valid = Draft();
        var zeroAmount = () => PostgreSqlSelfServiceEconomyTransferIntentStore.Validate(
            valid with { AmountUnits = 0 });
        var negativeAmount = () => PostgreSqlSelfServiceEconomyTransferIntentStore.Validate(
            valid with { AmountUnits = -1 });
        var missingTime = () => PostgreSqlSelfServiceEconomyTransferIntentStore.Validate(
            valid with { RequestedAt = default });

        zeroAmount.Should().Throw<ArgumentOutOfRangeException>();
        negativeAmount.Should().Throw<ArgumentOutOfRangeException>();
        missingTime.Should().Throw<ArgumentException>();
        PostgreSqlSelfServiceEconomyTransferIntentStore.Validate(valid);
    }

    [Fact]
    public void IntentRequestHashComparison_IsNullSafeAndFixedTime()
    {
        var nullActual = () => PostgreSqlSelfServiceEconomyTransferIntentStore.EnsureRequestHash(null!, "hash");
        var nullExpected = () => PostgreSqlSelfServiceEconomyTransferIntentStore.EnsureRequestHash("hash", null!);
        var mismatch = () => PostgreSqlSelfServiceEconomyTransferIntentStore.EnsureRequestHash("first", "second");

        nullActual.Should().Throw<ArgumentNullException>();
        nullExpected.Should().Throw<ArgumentNullException>();
        mismatch.Should().Throw<SelfServiceEconomyTransferException>();
        PostgreSqlSelfServiceEconomyTransferIntentStore.EnsureRequestHash("same", "same");
    }

    [Fact]
    public void SourceRootRequestValidation_RejectsEveryIncompleteBinding()
    {
        var valid = RootRequest();
        SelfServiceEconomyTransferSourceRootRequest? missing = null;
        var nullValidation = () => PostgreSqlSelfServiceEconomyTransferSourceRootPlanner.Validate(missing!);
        nullValidation.Should().Throw<ArgumentNullException>();
        var invalid = new[]
        {
            valid with { PostingId = default },
            valid with { TenantId = Guid.Empty },
            valid with { ActorId = Guid.Empty },
            valid with { SourceWalletId = default },
            valid with { DestinationWalletId = default }
        };
        foreach (var request in invalid)
        {
            var validate = () => PostgreSqlSelfServiceEconomyTransferSourceRootPlanner.Validate(request);
            validate.Should().Throw<ArgumentException>();
        }
        var sameWallet = () => PostgreSqlSelfServiceEconomyTransferSourceRootPlanner.Validate(
            valid with { DestinationWalletId = SourceWallet });

        sameWallet.Should().Throw<ArgumentException>();
        PostgreSqlSelfServiceEconomyTransferSourceRootPlanner.Validate(valid);
    }

    [Fact]
    public void SourceRootNormalization_RejectsInvalidSetsAndReturnsCanonicalOrder()
    {
        IReadOnlyCollection<Guid>? missing = null;
        var nullRoots = () => PostgreSqlSelfServiceEconomyTransferSourceRootPlanner.NormalizeRoots(missing!);
        var emptyRoots = () => PostgreSqlSelfServiceEconomyTransferSourceRootPlanner.NormalizeRoots([]);
        var emptyId = () => PostgreSqlSelfServiceEconomyTransferSourceRootPlanner.NormalizeRoots([Guid.Empty]);
        var root = Guid.NewGuid();
        var duplicates = () => PostgreSqlSelfServiceEconomyTransferSourceRootPlanner.NormalizeRoots([root, root]);
        var second = Guid.NewGuid();

        nullRoots.Should().Throw<ArgumentNullException>();
        emptyRoots.Should().Throw<SelfServiceEconomyTransferException>();
        emptyId.Should().Throw<SelfServiceEconomyTransferException>();
        duplicates.Should().Throw<SelfServiceEconomyTransferException>();
        PostgreSqlSelfServiceEconomyTransferSourceRootPlanner.NormalizeRoots([second, root])
            .Select(value => value.Value).Should().Equal(new[] { root, second }.Order());
    }

    [Fact]
    public void RequestValidation_RejectsEveryInvalidBrowserInput()
    {
        SelfServiceEconomyTransferRequest? missing = null;
        var nullRequest = () => SelfServiceEconomyTransferService.Validate(missing!);
        nullRequest.Should().Throw<ArgumentNullException>();
        var valid = Request();
        var invalid = new SelfServiceEconomyTransferRequest[]
        {
            valid with { RecipientUserId = Guid.Empty },
            valid with { TransferType = (SelfServiceEconomyTransferType)999 },
            valid with { Currency = (CurrencyCode)999 },
            valid with { AmountUnits = 0 },
            valid with { AmountUnits = -1 }
        };
        foreach (var request in invalid)
        {
            var validate = () => SelfServiceEconomyTransferService.Validate(request);
            validate.Should().Throw<ArgumentException>();
        }
        var invalidKey = () => SelfServiceEconomyTransferService.Validate(valid with { IdempotencyKey = " " });

        invalidKey.Should().Throw<ArgumentException>();
        SelfServiceEconomyTransferService.Validate(valid);
    }

    [Fact]
    public void ProvenanceMapping_IsClosedToSupportedCurrencies()
    {
        SelfServiceEconomyTransferService.ProvenanceFor(CurrencyCode.HardCoin)
            .Should().Be(ProvenanceKind.PurchasedHard);
        SelfServiceEconomyTransferService.ProvenanceFor(CurrencyCode.SoftCoin)
            .Should().Be(ProvenanceKind.ConvertedSoft);
        var invalid = () => SelfServiceEconomyTransferService.ProvenanceFor((CurrencyCode)999);

        invalid.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void PreparedIntentValidation_RejectsEveryIdempotencyBindingMismatch()
    {
        var prepared = Prepared();
        var request = Request();
        PreparedSelfServiceEconomyTransferIntent? missing = null;
        var nullPrepared = () => SelfServiceEconomyTransferService.EnsurePreparedIntent(
            missing!, (TenantId, ActorId), request, ProvenanceKind.PurchasedHard, new IdempotencyKey("key"));
        nullPrepared.Should().Throw<ArgumentNullException>();
        var mutations = new Func<PreparedSelfServiceEconomyTransferIntent, PreparedSelfServiceEconomyTransferIntent>[]
        {
            value => value with { TenantId = Guid.NewGuid() },
            value => value with { ActorId = Guid.NewGuid() },
            value => value with { RecipientUserId = Guid.NewGuid() },
            value => value with { TransferType = SelfServiceEconomyTransferType.Gift },
            value => value with { Currency = CurrencyCode.SoftCoin },
            value => value with { Provenance = ProvenanceKind.ConvertedSoft },
            value => value with { AmountUnits = 8 },
            value => value with { IdempotencyKey = new IdempotencyKey("other") }
        };
        foreach (var mutate in mutations)
        {
            var validate = () => SelfServiceEconomyTransferService.EnsurePreparedIntent(
                mutate(prepared), (TenantId, ActorId), request,
                ProvenanceKind.PurchasedHard, new IdempotencyKey("key"));
            validate.Should().Throw<SelfServiceEconomyTransferException>();
        }
    }

    [Fact]
    public void PreparedIntentValidation_RequiresEveryServerHash()
    {
        var prepared = Prepared();
        var request = Request();
        var mutations = new Func<PreparedSelfServiceEconomyTransferIntent, PreparedSelfServiceEconomyTransferIntent>[]
        {
            value => value with { RequestHash = " " },
            value => value with { ProviderReferenceHash = " " },
            value => value with { DestinationHash = " " }
        };
        foreach (var mutate in mutations)
        {
            var validate = () => SelfServiceEconomyTransferService.EnsurePreparedIntent(
                mutate(prepared), (TenantId, ActorId), request,
                ProvenanceKind.PurchasedHard, new IdempotencyKey("key"));
            validate.Should().Throw<ArgumentException>();
        }
        SelfServiceEconomyTransferService.EnsurePreparedIntent(
            prepared, (TenantId, ActorId), request,
            ProvenanceKind.PurchasedHard, new IdempotencyKey("key"));
    }

    [Fact]
    public void AuthorizationValidation_RejectsEveryActorReceiptAndRiskMismatch()
    {
        var authorization = Authorization();
        EconomyProtectedOperationAuthorization? missing = null;
        var nullAuthorization = () => SelfServiceEconomyTransferService.EnsureAuthorization(
            missing!, (TenantId, ActorId));
        nullAuthorization.Should().Throw<ArgumentNullException>();
        var mutations = new Func<EconomyProtectedOperationAuthorization, EconomyProtectedOperationAuthorization>[]
        {
            value => value with { TenantId = Guid.NewGuid() },
            value => value with { ActorId = Guid.NewGuid() },
            value => value with { Receipt = value.Receipt with { TenantId = Guid.NewGuid() } },
            value => value with { Receipt = value.Receipt with { ActorId = Guid.NewGuid() } },
            value => value with { Receipt = value.Receipt with { Capability = EconomyValueMovementCapability.PayoutExecution } },
            value => value with { RiskDecisionId = Guid.NewGuid() },
            value => value with { OperationFingerprint = "other-fingerprint" }
        };
        foreach (var mutate in mutations)
        {
            var validate = () => SelfServiceEconomyTransferService.EnsureAuthorization(
                mutate(authorization), (TenantId, ActorId));
            validate.Should().Throw<SelfServiceEconomyTransferException>();
        }
        SelfServiceEconomyTransferService.EnsureAuthorization(authorization, (TenantId, ActorId));
    }

    private static SelfServiceEconomyTransferIntentDraft Draft() => new(
        TenantId,
        ActorId,
        RecipientId,
        SelfServiceEconomyTransferType.Tip,
        CurrencyCode.HardCoin,
        ProvenanceKind.PurchasedHard,
        7,
        new IdempotencyKey("key"),
        Now);

    private static SelfServiceEconomyTransferSourceRootRequest RootRequest() => new(
        PostingId,
        TenantId,
        ActorId,
        SourceWallet,
        DestinationWallet);

    private static SelfServiceEconomyTransferRequest Request() => new(
        RecipientId,
        SelfServiceEconomyTransferType.Tip,
        CurrencyCode.HardCoin,
        7,
        "key");

    private static PreparedSelfServiceEconomyTransferIntent Prepared() => new(
        PostingId,
        TenantId,
        ActorId,
        RecipientId,
        SelfServiceEconomyTransferType.Tip,
        CurrencyCode.HardCoin,
        ProvenanceKind.PurchasedHard,
        7,
        new IdempotencyKey("key"),
        "request-hash",
        "provider-hash",
        "destination-hash",
        Now);

    private static EconomyProtectedOperationAuthorization Authorization()
    {
        var riskDecisionId = Guid.Parse("a3000000-0000-0000-0000-000000000007");
        var receipt = new CapabilityAuthorizationReceipt(
            Guid.Parse("a3000000-0000-0000-0000-000000000008"),
            TenantId,
            ActorId,
            "subject",
            "US",
            EconomyValueMovementCapability.Transfer,
            "fingerprint",
            1,
            1,
            riskDecisionId,
            1,
            "provider-hash",
            "destination-hash",
            [],
            [],
            Now,
            Now.AddMinutes(5),
            "receipt-hash",
            "key-id",
            "signature");
        return new EconomyProtectedOperationAuthorization(
            TenantId,
            ActorId,
            "US",
            riskDecisionId,
            "fingerprint",
            receipt);
    }
}
