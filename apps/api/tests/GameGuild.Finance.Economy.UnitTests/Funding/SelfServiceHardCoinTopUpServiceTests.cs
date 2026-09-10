using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Finance.Economy.UnitTests.Funding;

public sealed class SelfServiceHardCoinTopUpServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("98000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("98000000-0000-0000-0000-000000000002");
    private static readonly Guid WalletId = Guid.Parse("98000000-0000-0000-0000-000000000003");
    private static readonly Guid TopUpId = Guid.Parse("98000000-0000-0000-0000-000000000004");
    private static readonly Guid PaymentId = Guid.Parse("98000000-0000-0000-0000-000000000005");
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_DerivesAuthorityAndBindsTheProviderIntent()
    {
        var store = new StubStore();
        var provider = new StubProvider();
        var service = CreateService(store, provider);

        var receipt = await service.CreateAsync(
            new SelfServiceHardCoinTopUpRequest(250, "top-up-key"), default);

        store.Prepared.Should().Be(new EconomyTopUpIntentDraft(
            TenantId, ActorId, new WalletId(WalletId), 250, 250, "BRA", 11,
            "policy-hash", "stripe", new IdempotencyKey("top-up-key"), Now));
        provider.Created.Should().Be(new EconomyTopUpProviderCreateRequest(
            TopUpId, TenantId, 250, "USD", "top-up-key"));
        store.Bound.Should().Be(new EconomyTopUpProviderBinding(
            TopUpId, "stripe", "test", "acct_platform", "pi_topup", "payment_intent",
            "capture", EconomyTopUpProviderStatus.RequiresAction, Now));
        receipt.Should().Be(new SelfServiceHardCoinTopUpReceipt(
            TopUpId, PaymentId, 250, 250, "USD", EconomyTopUpProviderStatus.RequiresAction,
            "pi_topup_secret", "pk_test", false)
        {
            ProviderObjectId = "pi_topup"
        });
    }

    [Fact]
    public async Task CreateAsync_ReplaysTheExistingProviderObjectWithoutCreatingAnotherIntent()
    {
        var store = new StubStore(existingProviderObject: "pi_existing");
        var provider = new StubProvider();
        var service = CreateService(store, provider);

        var receipt = await service.CreateAsync(
            new SelfServiceHardCoinTopUpRequest(250, "top-up-key"), default);

        provider.Created.Should().BeNull();
        provider.RetrievedProviderObjectId.Should().Be("pi_existing");
        receipt.ProviderObjectId.Should().Be("pi_existing");
        receipt.IsDuplicate.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_RequiresAnAuthenticatedTenantActorAndValidIntent()
    {
        var anonymous = CreateService(new StubStore(), new StubProvider(), authenticated: false);

        await FluentActions.Awaiting(() => anonymous.CreateAsync(
                new SelfServiceHardCoinTopUpRequest(250, "key"), default))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        await FluentActions.Awaiting(() => CreateService(new StubStore(), new StubProvider())
                .CreateAsync(new SelfServiceHardCoinTopUpRequest(0, "key"), default))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => CreateService(new StubStore(), new StubProvider())
                .CreateAsync(new SelfServiceHardCoinTopUpRequest(250, ""), default))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => CreateService(new StubStore(), new StubProvider())
                .CreateAsync(null!, default))
            .Should().ThrowAsync<ArgumentNullException>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await FluentActions.Awaiting(() => CreateService(new StubStore(), new StubProvider())
                .CreateAsync(new SelfServiceHardCoinTopUpRequest(250, "key"), cancellation.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CreateAsync_RejectsEveryIncompleteActorAuthority()
    {
        foreach (var actor in new[]
                 {
                     Actor(true, null, ActorId.ToString()),
                     Actor(true, TenantId, null),
                     Actor(true, TenantId, "invalid")
                 })
        {
            await FluentActions.Awaiting(() => CreateService(
                    new StubStore(), new StubProvider(), actor: actor).CreateAsync(
                    new SelfServiceHardCoinTopUpRequest(250, "key"), default))
                .Should().ThrowAsync<UnauthorizedAccessException>();
        }
    }

    [Fact]
    public void EnsurePrepared_RejectsEveryAuthorityMismatchAndMissingHash()
    {
        var prepared = Prepared();
        var actor = (TenantId, ActorId);
        var wallet = new EconomyWalletIdentity(
            new WalletId(WalletId), TenantId, ActorId, WalletLifecycleState.Active);
        var policy = new HardCoinTopUpPolicyAuthorization("BRA", 11, "policy-hash", 250, 250, "stripe");
        var key = new IdempotencyKey("top-up-key");
        SelfServiceHardCoinTopUpService.EnsurePrepared(prepared, actor, wallet, policy, key);
        FluentActions.Invoking(() => SelfServiceHardCoinTopUpService.EnsurePrepared(
                null!, actor, wallet, policy, key)).Should().Throw<ArgumentNullException>();
        PreparedEconomyTopUpIntent[] invalid =
        [
            prepared with { TenantId = Guid.NewGuid() },
            prepared with { ActorId = Guid.NewGuid() },
            prepared with { WalletId = new WalletId(Guid.NewGuid()) },
            prepared with { HardCoinUnits = 251 },
            prepared with { UsdMinorUnits = 251 },
            prepared with { JurisdictionCode = "USA" },
            prepared with { PolicyVersion = 12 },
            prepared with { PolicyHash = "other" },
            prepared with { Provider = "other" },
            prepared with { IdempotencyKey = new IdempotencyKey("other-key") }
        ];
        foreach (var item in invalid)
            FluentActions.Invoking(() => SelfServiceHardCoinTopUpService.EnsurePrepared(
                    item, actor, wallet, policy, key)).Should().Throw<EconomyTopUpReplayConflictException>();
        FluentActions.Invoking(() => SelfServiceHardCoinTopUpService.EnsurePrepared(
                prepared with { RequestHash = "" }, actor, wallet, policy, key))
            .Should().Throw<ArgumentException>();

        prepared.ProviderEnvironment.Should().BeNull();
        prepared.ProviderAccountId.Should().BeNull();
        prepared.RequestedAt.Should().Be(Now);
    }

    [Fact]
    public void EnsureProviderResult_RejectsEveryIncompleteOrReboundProviderIdentity()
    {
        var prepared = Prepared();
        var result = ProviderResult();
        SelfServiceHardCoinTopUpService.EnsureProviderResult(prepared, result);
        FluentActions.Invoking(() => SelfServiceHardCoinTopUpService.EnsureProviderResult(prepared, null!))
            .Should().Throw<ArgumentNullException>();
        EconomyTopUpProviderResult[] invalid =
        [
            result with { Provider = "other" },
            result with { ProviderEnvironment = "" },
            result with { ProviderAccountId = "" },
            result with { ProviderObjectId = "" },
            result with { ProviderObjectType = "charge" },
            result with { ProviderMonetaryLeg = "refund" },
            result with { ClientSecret = "" },
            result with { PublishableKey = "" },
            result with { Status = EconomyTopUpProviderStatus.Posted }
        ];
        foreach (var item in invalid)
            FluentActions.Invoking(() => SelfServiceHardCoinTopUpService.EnsureProviderResult(prepared, item))
                .Should().Throw<EconomySelfServiceCommandRejectedException>();
        SelfServiceHardCoinTopUpService.EnsureProviderResult(
            prepared with { ProviderObjectId = result.ProviderObjectId }, result);
        FluentActions.Invoking(() => SelfServiceHardCoinTopUpService.EnsureProviderResult(
                prepared with { ProviderObjectId = "pi_other" }, result))
            .Should().Throw<EconomySelfServiceCommandRejectedException>();
    }

    private static SelfServiceHardCoinTopUpService CreateService(
        StubStore store,
        StubProvider provider,
        bool authenticated = true,
        ActorContext? actor = null)
    {
        var accessor = new ActorContextAccessor();
        if (authenticated || actor is not null)
            accessor.SetActorContext(actor ?? Actor(true, TenantId, ActorId.ToString()));

        return new SelfServiceHardCoinTopUpService(
            accessor,
            new StubWalletDirectory(),
            new StubPolicyResolver(),
            store,
            provider,
            new FixedTimeProvider(Now));
    }

    private static ActorContext Actor(bool authenticated, Guid? tenantId, string? subjectId) => new()
    {
        ActorKind = ActorKind.User,
        SubjectId = subjectId,
        TenantId = tenantId,
        Roles = new HashSet<string>(),
        Permissions = new HashSet<string>(),
        IsAuthenticated = authenticated
    };

    private static PreparedEconomyTopUpIntent Prepared() => new(
        TopUpId, PaymentId, TenantId, ActorId, new WalletId(WalletId), 250, 250, "BRA", 11,
        "policy-hash", "stripe", new IdempotencyKey("top-up-key"), "request-hash",
        null, null, null, EconomyTopUpProviderStatus.Prepared, Now, false);

    private static EconomyTopUpProviderResult ProviderResult() => new(
        "stripe", "test", "acct_platform", "pi_topup", "payment_intent", "capture",
        EconomyTopUpProviderStatus.RequiresAction, "pi_topup_secret", "pk_test");

    private sealed class StubWalletDirectory : IEconomyWalletDirectory
    {
        public ValueTask<EconomyWalletIdentity> GetOwnerWalletAsync(
            Guid tenantId, Guid ownerId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new EconomyWalletIdentity(
                new WalletId(WalletId), tenantId, ownerId, WalletLifecycleState.Active));

        public ValueTask<EconomyWalletIdentity> GetWalletAsync(
            Guid tenantId, WalletId walletId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubPolicyResolver : IHardCoinTopUpPolicyResolver
    {
        public ValueTask<HardCoinTopUpPolicyAuthorization> ResolveAsync(
            Guid tenantId, Guid actorId, long hardCoinUnits, DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken) => ValueTask.FromResult(
            new HardCoinTopUpPolicyAuthorization("BRA", 11, "policy-hash", hardCoinUnits,
                hardCoinUnits, "stripe"));
    }

    private sealed class StubStore(string? existingProviderObject = null) : IEconomyTopUpIntentStore
    {
        public EconomyTopUpIntentDraft? Prepared { get; private set; }
        public EconomyTopUpProviderBinding? Bound { get; private set; }

        public ValueTask<PreparedEconomyTopUpIntent> PrepareAsync(
            EconomyTopUpIntentDraft draft, CancellationToken cancellationToken)
        {
            Prepared = draft;
            return ValueTask.FromResult(new PreparedEconomyTopUpIntent(
                TopUpId, PaymentId, draft.TenantId, draft.ActorId, draft.WalletId,
                draft.HardCoinUnits, draft.UsdMinorUnits, draft.JurisdictionCode,
                draft.PolicyVersion, draft.PolicyHash, draft.Provider, draft.IdempotencyKey,
                "request-hash", existingProviderObject is null ? null : "test",
                existingProviderObject is null ? null : "acct_platform", existingProviderObject,
                existingProviderObject is null ? EconomyTopUpProviderStatus.Prepared :
                    EconomyTopUpProviderStatus.RequiresAction, draft.RequestedAt, existingProviderObject is not null));
        }

        public ValueTask BindProviderAsync(
            EconomyTopUpProviderBinding binding, CancellationToken cancellationToken)
        {
            Bound = binding;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubProvider : IEconomyTopUpProvider
    {
        public EconomyTopUpProviderCreateRequest? Created { get; private set; }
        public string? RetrievedProviderObjectId { get; private set; }

        public ValueTask<EconomyTopUpProviderResult> CreateAsync(
            EconomyTopUpProviderCreateRequest request, CancellationToken cancellationToken)
        {
            Created = request;
            return ValueTask.FromResult(Result("pi_topup"));
        }

        public ValueTask<EconomyTopUpProviderResult> RetrieveAsync(
            string providerObjectId, CancellationToken cancellationToken)
        {
            RetrievedProviderObjectId = providerObjectId;
            return ValueTask.FromResult(Result(providerObjectId));
        }

        private static EconomyTopUpProviderResult Result(string id) => new(
            "stripe", "test", "acct_platform", id, "payment_intent", "capture",
            EconomyTopUpProviderStatus.RequiresAction, $"{id}_secret", "pk_test");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
