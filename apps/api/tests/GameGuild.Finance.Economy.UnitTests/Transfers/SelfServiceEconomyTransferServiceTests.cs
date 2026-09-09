using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Finance.Economy.Transfers;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Finance.Economy.UnitTests.Transfers;

public sealed class SelfServiceEconomyTransferServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 30, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("a1000000-0000-0000-0000-000000000002");
    private static readonly Guid RecipientId = Guid.Parse("a1000000-0000-0000-0000-000000000003");
    private static readonly WalletId SourceWalletId = new(Guid.Parse("a1000000-0000-0000-0000-000000000004"));
    private static readonly WalletId DestinationWalletId = new(Guid.Parse("a1000000-0000-0000-0000-000000000005"));
    private static readonly PostingId PostingId = new(Guid.Parse("a1000000-0000-0000-0000-000000000006"));
    private static readonly SourceStampId SourceRootId = new(Guid.Parse("a1000000-0000-0000-0000-000000000007"));

    [Theory]
    [InlineData(CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard)]
    [InlineData(CurrencyCode.SoftCoin, ProvenanceKind.ConvertedSoft)]
    public async Task TransferAsync_DerivesAuthorityAndPostsTheServerPreparedIntent(
        CurrencyCode currency,
        ProvenanceKind expectedProvenance)
    {
        var harness = new Harness();
        var request = new SelfServiceEconomyTransferRequest(
            RecipientId,
            SelfServiceEconomyTransferType.CreatorSupport,
            currency,
            37,
            "transfer-key");

        var receipt = await harness.Service.TransferAsync(request);

        receipt.Should().Be(new SelfServiceEconomyTransferReceipt(
            PostingId.Value,
            SelfServiceEconomyTransferType.CreatorSupport,
            currency,
            37,
            RecipientId,
            41,
            "journal-hash",
            false));
        harness.Wallets.Lookups.Should().Equal(
            (TenantId, ActorId),
            (TenantId, RecipientId));
        harness.Intents.Draft.Should().Be(new SelfServiceEconomyTransferIntentDraft(
            TenantId,
            ActorId,
            RecipientId,
            SelfServiceEconomyTransferType.CreatorSupport,
            currency,
            expectedProvenance,
            37,
            new IdempotencyKey("transfer-key"),
            Now));
        harness.Orchestrator.Intent.Should().BeEquivalentTo(new EconomyProtectedOperationIntent(
            EconomyValueMovementCapability.Transfer,
            PostingTemplateKind.Spend,
            SourceWalletId,
            DestinationWalletId,
            new CoinAmount(currency, 37),
            new[] { new RiskCurrencyLeg(currency, 37) },
            new[] { SourceRootId },
            harness.Intents.Prepared.ProviderReferenceHash,
            harness.Intents.Prepared.DestinationHash,
            new IdempotencyKey("transfer-key"),
            Now));
        harness.SourceRoots.Request.Should().Be(new SelfServiceEconomyTransferSourceRootRequest(
            PostingId, TenantId, ActorId, SourceWalletId, DestinationWalletId));
        harness.Transaction.Executions.Should().Be(1);
        harness.Authorities.Request.Should().Be(("fifo-transfer", PostingTemplateKind.Spend));
        harness.Gateway.Request!.Command.Should().Be(new TransferFragmentsCommand(
            PostingId,
            new IdempotencyKey("transfer-key"),
            SourceWalletId,
            DestinationWalletId,
            new CoinAmount(currency, 37),
            expectedProvenance,
            new ReserveVersion(8),
            new PolicyVersion(7),
            Now));
        harness.Gateway.Request.DispatchSnapshotHash.Should().Be(harness.Intents.Prepared.RequestHash);
    }

    [Fact]
    public async Task TransferAsync_RejectsSelfTransferBeforePersistingAnIntent()
    {
        var harness = new Harness();

        var operation = () => harness.Service.TransferAsync(new SelfServiceEconomyTransferRequest(
            ActorId,
            SelfServiceEconomyTransferType.Gift,
            CurrencyCode.SoftCoin,
            1,
            "self-transfer"));

        await operation.Should().ThrowAsync<SelfServiceEconomyTransferException>()
            .WithMessage("A transfer recipient must differ from the authenticated actor.");
        harness.Intents.Draft.Should().BeNull();
        harness.Orchestrator.Intent.Should().BeNull();
    }

    [Fact]
    public async Task TransferAsync_RequiresAnAuthenticatedTenantActor()
    {
        var harness = new Harness(authenticated: false);

        var operation = () => harness.Service.TransferAsync(new SelfServiceEconomyTransferRequest(
            RecipientId,
            SelfServiceEconomyTransferType.Tip,
            CurrencyCode.HardCoin,
            1,
            "anonymous-transfer"));

        await operation.Should().ThrowAsync<UnauthorizedAccessException>();
        harness.Wallets.Lookups.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "not-a-guid")]
    public async Task TransferAsync_RequiresTenantAndGuidSubjectBindings(
        bool includeTenant,
        string? subjectId)
    {
        var harness = new Harness(includeTenant: includeTenant, subjectId: subjectId);

        var operation = () => harness.Service.TransferAsync(new SelfServiceEconomyTransferRequest(
            RecipientId,
            SelfServiceEconomyTransferType.Tip,
            CurrencyCode.HardCoin,
            1,
            "invalid-actor-binding"));

        await operation.Should().ThrowAsync<UnauthorizedAccessException>();
        harness.Wallets.Lookups.Should().BeEmpty();
    }

    [Fact]
    public async Task TransferAsync_RejectsWalletAliasesBeforeStartingTheTransaction()
    {
        var harness = new Harness(aliasWallets: true);

        var operation = () => harness.Service.TransferAsync(new SelfServiceEconomyTransferRequest(
            RecipientId,
            SelfServiceEconomyTransferType.Gift,
            CurrencyCode.SoftCoin,
            1,
            "wallet-alias"));

        await operation.Should().ThrowAsync<SelfServiceEconomyTransferException>()
            .WithMessage("A transfer recipient must have a distinct Economy wallet.");
        harness.Transaction.Executions.Should().Be(0);
    }

    [Fact]
    public async Task TransferAsync_RejectsAnEmptyReservedRootSetBeforeRiskEvaluation()
    {
        var harness = new Harness(includeSourceRoots: false);

        var operation = () => harness.Service.TransferAsync(new SelfServiceEconomyTransferRequest(
            RecipientId,
            SelfServiceEconomyTransferType.Gift,
            CurrencyCode.SoftCoin,
            1,
            "empty-roots"));

        await operation.Should().ThrowAsync<SelfServiceEconomyTransferException>()
            .WithMessage("The transfer could not reserve an authorized source-root set.");
        harness.Orchestrator.Intent.Should().BeNull();
    }

    [Fact]
    public async Task TransferAsync_RejectsAnAuthorizationBoundToAnotherActor()
    {
        var harness = new Harness(authorizationActorId: Guid.Parse("a1000000-0000-0000-0000-000000000099"));

        var operation = () => harness.Service.TransferAsync(new SelfServiceEconomyTransferRequest(
            RecipientId,
            SelfServiceEconomyTransferType.Tip,
            CurrencyCode.HardCoin,
            5,
            "wrong-actor"));

        await operation.Should().ThrowAsync<SelfServiceEconomyTransferException>()
            .WithMessage("The protected transfer authorization is not bound to the authenticated actor.");
        harness.Authorities.Request.Should().BeNull();
        harness.Gateway.Request.Should().BeNull();
    }

    [Theory]
    [InlineData(SelfServiceEconomyTransferType.Tip)]
    [InlineData(SelfServiceEconomyTransferType.Gift)]
    [InlineData(SelfServiceEconomyTransferType.CreatorSupport)]
    public async Task TransferAsync_BindsEverySupportedTransferTypeToTheDestinationHash(
        SelfServiceEconomyTransferType transferType)
    {
        var harness = new Harness();

        await harness.Service.TransferAsync(new SelfServiceEconomyTransferRequest(
            RecipientId,
            transferType,
            CurrencyCode.HardCoin,
            9,
            $"type-{transferType}"));

        harness.Intents.Draft!.TransferType.Should().Be(transferType);
        harness.Orchestrator.Intent!.DestinationHash.Should().Be(harness.Intents.Prepared.DestinationHash);
    }

    private sealed class Harness
    {
        public Harness(
            bool authenticated = true,
            Guid? authorizationActorId = null,
            bool includeTenant = true,
            string? subjectId = null,
            bool aliasWallets = false,
            bool includeSourceRoots = true)
        {
            var actorContexts = new ActorContextAccessor();
            actorContexts.SetActorContext(new ActorContext
            {
                ActorKind = ActorKind.User,
                SubjectId = subjectId ?? ActorId.ToString(),
                TenantId = includeTenant ? TenantId : null,
                Roles = new HashSet<string>(),
                Permissions = new HashSet<string>(),
                TypedAttributes = ActorAttributes.Empty,
                IsAuthenticated = authenticated
            });
            Wallets = new CapturingWalletDirectory(aliasWallets);
            Intents = new CapturingIntentStore();
            SourceRoots = new CapturingSourceRootPlanner(includeSourceRoots);
            Orchestrator = new CapturingOrchestrator(authorizationActorId ?? ActorId);
            Transaction = new CapturingTransaction();
            Authorities = new CapturingAuthorityResolver();
            Gateway = new CapturingTransferGateway();
            Service = new SelfServiceEconomyTransferService(
                actorContexts,
                Wallets,
                Intents,
                SourceRoots,
                Orchestrator,
                Transaction,
                Authorities,
                Gateway,
                new FixedTimeProvider(Now));
        }

        public SelfServiceEconomyTransferService Service { get; }
        public CapturingWalletDirectory Wallets { get; }
        public CapturingIntentStore Intents { get; }
        public CapturingSourceRootPlanner SourceRoots { get; }
        public CapturingOrchestrator Orchestrator { get; }
        public CapturingTransaction Transaction { get; }
        public CapturingAuthorityResolver Authorities { get; }
        public CapturingTransferGateway Gateway { get; }
    }

    private sealed class CapturingSourceRootPlanner(bool includeSourceRoots)
        : ISelfServiceEconomyTransferSourceRootPlanner
    {
        public SelfServiceEconomyTransferSourceRootRequest? Request { get; private set; }

        public ValueTask<IReadOnlyList<SourceStampId>> ReserveAsync(
            SelfServiceEconomyTransferSourceRootRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return ValueTask.FromResult<IReadOnlyList<SourceStampId>>(
                includeSourceRoots ? [SourceRootId] : []);
        }
    }

    private sealed class CapturingTransaction : IEconomyProtectedOperationTransaction
    {
        public int Executions { get; private set; }

        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken)
        {
            Executions++;
            return await operation(cancellationToken);
        }
    }

    private sealed class CapturingWalletDirectory(bool aliasWallets) : IEconomyWalletDirectory
    {
        public List<(Guid TenantId, Guid OwnerId)> Lookups { get; } = [];

        public ValueTask<EconomyWalletIdentity> GetOwnerWalletAsync(
            Guid tenantId,
            Guid ownerId,
            CancellationToken cancellationToken = default)
        {
            Lookups.Add((tenantId, ownerId));
            var walletId = ownerId == ActorId || aliasWallets ? SourceWalletId : DestinationWalletId;
            return ValueTask.FromResult(new EconomyWalletIdentity(
                walletId, tenantId, ownerId, WalletLifecycleState.Active));
        }

        public ValueTask<EconomyWalletIdentity> GetWalletAsync(
            Guid tenantId,
            WalletId walletId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CapturingIntentStore : ISelfServiceEconomyTransferIntentStore
    {
        public SelfServiceEconomyTransferIntentDraft? Draft { get; private set; }

        public PreparedSelfServiceEconomyTransferIntent Prepared { get; } = new(
            PostingId,
            TenantId,
            ActorId,
            RecipientId,
            SelfServiceEconomyTransferType.CreatorSupport,
            CurrencyCode.HardCoin,
            ProvenanceKind.PurchasedHard,
            37,
            new IdempotencyKey("transfer-key"),
            "request-hash",
            "internal-provider-hash",
            "destination-hash",
            Now);

        public ValueTask<PreparedSelfServiceEconomyTransferIntent> PrepareAsync(
            SelfServiceEconomyTransferIntentDraft draft,
            CancellationToken cancellationToken = default)
        {
            Draft = draft;
            return ValueTask.FromResult(Prepared with
            {
                TransferType = draft.TransferType,
                Currency = draft.Currency,
                Provenance = draft.Provenance,
                AmountUnits = draft.AmountUnits,
                IdempotencyKey = draft.IdempotencyKey
            });
        }
    }

    private sealed class CapturingOrchestrator(Guid authorizationActorId)
        : IEconomyProtectedOperationOrchestrator
    {
        public EconomyProtectedOperationIntent? Intent { get; private set; }

        public async Task<TResult> ExecuteAsync<TResult>(
            EconomyProtectedOperationIntent intent,
            Func<EconomyProtectedOperationAuthorization, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken)
        {
            Intent = intent;
            var receipt = new CapabilityAuthorizationReceipt(
                Guid.Parse("a1000000-0000-0000-0000-000000000010"),
                TenantId,
                authorizationActorId,
                "subject",
                "US",
                EconomyValueMovementCapability.Transfer,
                "operation-fingerprint",
                7,
                8,
                Guid.Parse("a1000000-0000-0000-0000-000000000011"),
                3,
                "provider-hash",
                "destination-hash",
                [],
                [],
                Now,
                Now.AddMinutes(5),
                "receipt-hash",
                "key-id",
                "signature");
            return await operation(new EconomyProtectedOperationAuthorization(
                TenantId,
                authorizationActorId,
                "US",
                receipt.RiskDecisionId,
                receipt.OperationFingerprint,
                receipt), cancellationToken);
        }
    }

    private sealed class CapturingAuthorityResolver : IRegisteredPostingCapabilityResolver
    {
        public (string Name, PostingTemplateKind Template)? Request { get; private set; }

        public Task<RegisteredPostingCapability> ResolveAsync(
            string capabilityName,
            PostingTemplateKind templateKind,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RegisteredPostingAuthority> ResolveAuthorityAsync(
            string capabilityName,
            PostingTemplateKind templateKind,
            CapabilityAuthorizationReceipt receipt,
            CancellationToken cancellationToken = default)
        {
            Request = (capabilityName, templateKind);
            return Task.FromResult(new RegisteredPostingAuthority(
                Guid.Parse("a1000000-0000-0000-0000-000000000020"),
                receipt.ActorId,
                receipt.TenantId,
                receipt.RiskDecisionId,
                receipt.OperationFingerprint,
                1));
        }
    }

    private sealed class CapturingTransferGateway : IFifoTransferGateway
    {
        public PersistedFifoTransferRequest? Request { get; private set; }

        public RegisteredPostingReceipt Transfer(PersistedFifoTransferRequest request)
        {
            Request = request;
            return new RegisteredPostingReceipt(request.Command.PostingId, 41, "journal-hash", false);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
