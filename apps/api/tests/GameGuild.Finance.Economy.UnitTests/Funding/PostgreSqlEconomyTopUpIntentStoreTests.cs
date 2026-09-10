using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Commerce.Payments;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GameGuild.Finance.Economy.UnitTests.Funding;

public sealed class PostgreSqlEconomyTopUpIntentStoreTests
{
    private static readonly Guid TenantId = Guid.Parse("9a000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("9a000000-0000-0000-0000-000000000002");
    private static readonly Guid WalletId = Guid.Parse("9a000000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PrepareAndBindProvider_PersistAnImmutablePaymentIntentBinding()
    {
        await using var context = CreateContext();
        var store = new PostgreSqlEconomyTopUpIntentStore(context);
        var draft = Draft();

        var prepared = await store.PrepareAsync(draft, default);
        await store.BindProviderAsync(new EconomyTopUpProviderBinding(
            prepared.Id, "stripe", "test", "acct_platform", "pi_topup", "payment_intent",
            "capture", EconomyTopUpProviderStatus.RequiresAction, Now), default);
        var replay = await store.PrepareAsync(draft, default);

        replay.IsDuplicate.Should().BeTrue();
        replay.ProviderObjectId.Should().Be("pi_topup");
        replay.Status.Should().Be(EconomyTopUpProviderStatus.RequiresAction);
        var payment = await context.Set<Payment>().SingleAsync();
        payment.Id.Should().Be(prepared.PaymentId);
        payment.Amount.Should().Be(2.50m);
        payment.Currency.Should().Be("USD");
        payment.ProviderObjectId.Should().Be("pi_topup");
        payment.ProviderMonetaryLeg.Should().Be("capture");
        payment.Status.Should().Be(PaymentStatus.RequiresAction);
    }

    [Fact]
    public async Task PrepareAsync_RejectsIdempotencyReplayWithDifferentAuthorityInputs()
    {
        await using var context = CreateContext();
        var store = new PostgreSqlEconomyTopUpIntentStore(context);
        await store.PrepareAsync(Draft(), default);

        var action = () => store.PrepareAsync(Draft() with { HardCoinUnits = 251, UsdMinorUnits = 251 }, default).AsTask();

        await action.Should().ThrowAsync<EconomyTopUpReplayConflictException>();
    }

    [Fact]
    public async Task BindProviderAsync_RejectsRebindingAndInvalidProviderState()
    {
        await using var context = CreateContext();
        var store = new PostgreSqlEconomyTopUpIntentStore(context);
        var prepared = await store.PrepareAsync(Draft(), default);
        var binding = new EconomyTopUpProviderBinding(
            prepared.Id, "stripe", "test", "acct_platform", "pi_topup", "payment_intent",
            "capture", EconomyTopUpProviderStatus.Processing, Now);
        await store.BindProviderAsync(binding, default);

        await FluentActions.Awaiting(() => store.BindProviderAsync(
                binding with { ProviderObjectId = "pi_other" }, default).AsTask())
            .Should().ThrowAsync<EconomyTopUpReplayConflictException>();
        await FluentActions.Awaiting(() => store.BindProviderAsync(
                binding with { TopUpId = Guid.NewGuid() }, default).AsTask())
            .Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Awaiting(() => store.BindProviderAsync(
                binding with { Status = EconomyTopUpProviderStatus.Posted }, default).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await store.BindProviderAsync(binding, default);

        await using var mismatchContext = CreateContext();
        var mismatchStore = new PostgreSqlEconomyTopUpIntentStore(mismatchContext);
        var other = await mismatchStore.PrepareAsync(Draft() with
        {
            IdempotencyKey = new IdempotencyKey("provider-mismatch")
        }, default);
        await FluentActions.Awaiting(() => mismatchStore.BindProviderAsync(
                binding with { TopUpId = other.Id, Provider = "other" }, default).AsTask())
            .Should().ThrowAsync<EconomySelfServiceCommandRejectedException>();

        await using var processingContext = CreateContext();
        var processingStore = new PostgreSqlEconomyTopUpIntentStore(processingContext);
        var processing = await processingStore.PrepareAsync(Draft() with
        {
            IdempotencyKey = new IdempotencyKey("processing")
        }, default);
        await processingStore.BindProviderAsync(
            binding with
            {
                TopUpId = processing.Id,
                ProviderObjectId = "pi_processing",
                Status = EconomyTopUpProviderStatus.Processing
            }, default);
        (await processingContext.Set<Payment>().SingleAsync()).Status.Should().Be(PaymentStatus.Processing);
    }

    [Fact]
    public void ValidationAndHashing_RejectMalformedDraftsAndRemainDeterministic()
    {
        var draft = Draft();

        PostgreSqlEconomyTopUpIntentStore.RequestHash(draft)
            .Should().Be(PostgreSqlEconomyTopUpIntentStore.RequestHash(draft));
        FluentActions.Invoking(() => PostgreSqlEconomyTopUpIntentStore.RequestHash(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => PostgreSqlEconomyTopUpIntentStore.Validate((EconomyTopUpIntentDraft)null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => PostgreSqlEconomyTopUpIntentStore.Validate(
                draft with { TenantId = Guid.Empty }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => PostgreSqlEconomyTopUpIntentStore.Validate(
                draft with { ActorId = Guid.Empty }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => PostgreSqlEconomyTopUpIntentStore.Validate(
                draft with { WalletId = new WalletId(Guid.Empty) }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => PostgreSqlEconomyTopUpIntentStore.Validate(
                draft with { HardCoinUnits = 0 }))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => PostgreSqlEconomyTopUpIntentStore.Validate(
                draft with { UsdMinorUnits = 0 }))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => PostgreSqlEconomyTopUpIntentStore.Validate(
                draft with { HardCoinUnits = 1, UsdMinorUnits = 2 }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => PostgreSqlEconomyTopUpIntentStore.Validate(
                draft with { JurisdictionCode = "BR" }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => PostgreSqlEconomyTopUpIntentStore.Validate(
                draft with { PolicyVersion = 0 }))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => PostgreSqlEconomyTopUpIntentStore.Validate(
                draft with { PolicyHash = "" }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => PostgreSqlEconomyTopUpIntentStore.Validate(
                draft with { Provider = "other" }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => PostgreSqlEconomyTopUpIntentStore.Validate(
                draft with { RequestedAt = default }))
            .Should().Throw<ArgumentException>();

        var binding = new EconomyTopUpProviderBinding(
            Guid.NewGuid(), "stripe", "test", "acct", "pi", "payment_intent", "capture",
            EconomyTopUpProviderStatus.RequiresAction, Now);
        PostgreSqlEconomyTopUpIntentStore.Validate(binding);
        FluentActions.Invoking(() => PostgreSqlEconomyTopUpIntentStore.Validate((EconomyTopUpProviderBinding)null!))
            .Should().Throw<ArgumentNullException>();
        EconomyTopUpProviderBinding[] invalid =
        [
            binding with { TopUpId = Guid.Empty },
            binding with { Provider = "" },
            binding with { ProviderEnvironment = "" },
            binding with { ProviderAccountId = "" },
            binding with { ProviderObjectId = "" },
            binding with { ProviderObjectType = "charge" },
            binding with { ProviderMonetaryLeg = "refund" },
            binding with { Status = EconomyTopUpProviderStatus.Posted },
            binding with { BoundAt = default }
        ];
        foreach (var item in invalid)
            FluentActions.Invoking(() => PostgreSqlEconomyTopUpIntentStore.Validate(item))
                .Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task ReaderIsTenantScopedOrderedAndValidatesEveryBoundary()
    {
        await using var context = CreateContext();
        var store = new PostgreSqlEconomyTopUpIntentStore(context);
        var first = await store.PrepareAsync(Draft(), default);
        var second = await store.PrepareAsync(Draft() with
        {
            IdempotencyKey = new IdempotencyKey("second"),
            RequestedAt = Now.AddMinutes(1)
        }, default);

        (await store.GetAsync(TenantId, ActorId, first.Id, default))!.TopUpId.Should().Be(first.Id);
        (await store.GetAsync(TenantId, Guid.NewGuid(), first.Id, default)).Should().BeNull();
        (await store.GetAsync(TenantId, ActorId, Guid.NewGuid(), default)).Should().BeNull();
        (await store.ListAsync(TenantId, ActorId, 2, default)).Select(item => item.TopUpId)
            .Should().Equal(second.Id, first.Id);
        await FluentActions.Awaiting(() => store.GetAsync(Guid.Empty, ActorId, first.Id, default).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => store.GetAsync(TenantId, Guid.Empty, first.Id, default).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => store.GetAsync(TenantId, ActorId, Guid.Empty, default).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => store.ListAsync(TenantId, ActorId, 0, default).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => store.ListAsync(TenantId, ActorId, 101, default).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(PaymentStatus.Processing, EconomyTopUpProviderStatus.RequiresAction, PaymentStatus.RequiresAction)]
    [InlineData(PaymentStatus.RequiresAction, EconomyTopUpProviderStatus.RequiresAction, PaymentStatus.RequiresAction)]
    [InlineData(PaymentStatus.Pending, EconomyTopUpProviderStatus.Processing, PaymentStatus.Processing)]
    [InlineData(PaymentStatus.Processing, EconomyTopUpProviderStatus.Processing, PaymentStatus.Processing)]
    [InlineData(PaymentStatus.Failed, EconomyTopUpProviderStatus.Posted, PaymentStatus.Succeeded)]
    [InlineData(PaymentStatus.Pending, EconomyTopUpProviderStatus.Held, PaymentStatus.Succeeded)]
    [InlineData(PaymentStatus.Succeeded, EconomyTopUpProviderStatus.Posted, PaymentStatus.Succeeded)]
    [InlineData(PaymentStatus.Processing, EconomyTopUpProviderStatus.Failed, PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Failed, EconomyTopUpProviderStatus.Failed, PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Processing, EconomyTopUpProviderStatus.Cancelled, PaymentStatus.Cancelled)]
    [InlineData(PaymentStatus.Cancelled, EconomyTopUpProviderStatus.Cancelled, PaymentStatus.Cancelled)]
    public async Task ApplyAsync_MapsEveryDurableProviderStateToThePaymentLifecycle(
        PaymentStatus initialPaymentStatus,
        EconomyTopUpProviderStatus providerStatus,
        PaymentStatus expectedPaymentStatus)
    {
        await using var context = CreateContext();
        var store = new PostgreSqlEconomyTopUpIntentStore(context);
        var prepared = await store.PrepareAsync(Draft(), default);
        var identity = Identity();
        await store.BindProviderAsync(Binding(prepared.Id, identity), default);
        var payment = await context.Set<Payment>().SingleAsync();
        MovePaymentTo(payment, initialPaymentStatus);
        await context.SaveChangesAsync();
        Guid? postingGroupId = providerStatus == EconomyTopUpProviderStatus.Posted ? Guid.NewGuid() : null;

        var result = await store.ApplyAsync(Event(
            identity,
            providerStatus,
            "evt-state",
            Now.AddMinutes(1),
            postingGroupId), default);

        result.Applied.Should().BeTrue();
        result.Status.Should().Be(providerStatus);
        (await context.Set<Payment>().SingleAsync()).Status.Should().Be(expectedPaymentStatus);
    }

    [Fact]
    public async Task ApplyAsync_IsIdempotentOrderedTerminalAndFailClosedForUnknownBindings()
    {
        await using var context = CreateContext();
        var store = new PostgreSqlEconomyTopUpIntentStore(context);
        var prepared = await store.PrepareAsync(Draft(), default);
        var identity = Identity();
        await store.BindProviderAsync(Binding(prepared.Id, identity), default);
        var current = Event(identity, EconomyTopUpProviderStatus.Processing, "evt-current", Now.AddMinutes(2));

        (await store.ApplyAsync(current, default)).Applied.Should().BeTrue();
        (await store.ApplyAsync(current, default)).Duplicate.Should().BeTrue();
        (await store.ApplyAsync(current with
        {
            EventId = "evt-old",
            OccurredAt = Now.AddMinutes(1),
            Status = EconomyTopUpProviderStatus.RequiresAction
        }, default)).Applied.Should().BeFalse();

        var row = await context.Set<EconomyTopUpIntentRow>().SingleAsync();
        row.Status = EconomyTopUpProviderStatus.Posted;
        (await store.ApplyAsync(current with { EventId = "evt-terminal" }, default)).Applied.Should().BeFalse();
        row.Status = EconomyTopUpProviderStatus.Cancelled;
        (await store.ApplyAsync(current with { EventId = "evt-cancelled" }, default)).Applied.Should().BeFalse();
        row.Status = EconomyTopUpProviderStatus.Held;
        (await store.ApplyAsync(current with { EventId = "evt-held" }, default)).Applied.Should().BeFalse();

        await FluentActions.Awaiting(() => store.ApplyAsync(current with
            {
                Identity = identity with { ProviderObjectId = "pi_unknown" }
            }, default).AsTask())
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public void ProviderEventValidationRejectsEveryUntrustedBoundary()
    {
        var identity = Identity();
        var valid = Event(identity, EconomyTopUpProviderStatus.Processing, "evt-valid", Now.AddMinutes(1));
        PostgreSqlEconomyTopUpIntentStore.Validate(identity);
        PostgreSqlEconomyTopUpIntentStore.Validate(valid);
        FluentActions.Invoking(() => PostgreSqlEconomyTopUpIntentStore.Validate((EconomyTopUpProviderIdentity)null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => PostgreSqlEconomyTopUpIntentStore.Validate((EconomyTopUpProviderEvent)null!))
            .Should().Throw<ArgumentNullException>();
        EconomyTopUpProviderIdentity[] invalidIdentities =
        [
            identity with { Provider = "other" },
            identity with { ProviderEnvironment = "" },
            identity with { ProviderAccountId = "" },
            identity with { ProviderObjectId = "" },
            identity with { ProviderObjectType = "charge" },
            identity with { ProviderMonetaryLeg = "refund" }
        ];
        foreach (var invalid in invalidIdentities)
            FluentActions.Invoking(() => PostgreSqlEconomyTopUpIntentStore.Validate(invalid))
                .Should().Throw<ArgumentException>();

        EconomyTopUpProviderEvent[] invalidEvents =
        [
            valid with { OccurredAt = default },
            valid with { Status = EconomyTopUpProviderStatus.ProviderSucceeded },
            valid with { EvidenceHash = "short" },
            valid with { EvidenceHash = new string('z', 64) },
            valid with { ProviderUsdMinorUnits = 0 },
            valid with { Currency = "EUR" },
            valid with { PostingGroupId = Guid.NewGuid() },
            valid with { Status = EconomyTopUpProviderStatus.Posted },
            valid with { Status = EconomyTopUpProviderStatus.Failed },
            valid with { Status = EconomyTopUpProviderStatus.Cancelled }
        ];
        foreach (var invalid in invalidEvents)
            FluentActions.Invoking(() => PostgreSqlEconomyTopUpIntentStore.Validate(invalid))
                .Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task FindAsync_RejectsEveryPaymentAndTopUpAuthorityDivergence()
    {
        for (var mismatch = -1; mismatch < 9; mismatch++)
        {
            await using var context = CreateContext();
            var identity = Identity();
            var payment = Payment.Create(
                mismatch == 0 ? Guid.NewGuid() : TenantId,
                mismatch == 1 ? 3m : 2.50m,
                mismatch == 2 ? "EUR" : "USD",
                $"binding-{mismatch}",
                mismatch == 3 ? "other" : identity.Provider);
            payment.BindProviderMapping(
                payment.Provider,
                mismatch == 4 ? "live" : identity.ProviderEnvironment,
                mismatch == 5 ? "acct_other" : identity.ProviderAccountId,
                mismatch == 6 ? "pi_other" : identity.ProviderObjectId,
                mismatch == 7 ? "charge" : identity.ProviderObjectType,
                mismatch == 8 ? "refund" : identity.ProviderMonetaryLeg);
            context.Set<Payment>().Add(payment);
            context.Set<EconomyTopUpIntentRow>().Add(new EconomyTopUpIntentRow
            {
                Id = Guid.NewGuid(),
                PaymentId = payment.Id,
                TenantId = TenantId,
                ActorId = ActorId,
                WalletId = WalletId,
                HardCoinUnits = 250,
                UsdMinorUnits = 250,
                JurisdictionCode = "USA",
                PolicyVersion = 1,
                PolicyHash = "policy",
                Provider = identity.Provider,
                IdempotencyKey = $"binding-{mismatch}",
                RequestHash = new string('b', 64),
                ProviderEnvironment = identity.ProviderEnvironment,
                ProviderAccountId = identity.ProviderAccountId,
                ProviderObjectId = identity.ProviderObjectId,
                ProviderObjectType = identity.ProviderObjectType,
                ProviderMonetaryLeg = identity.ProviderMonetaryLeg,
                Status = EconomyTopUpProviderStatus.Processing,
                RequestedAt = Now,
                ProviderBoundAt = Now,
                UpdatedAt = Now,
                Version = 2
            });
            await context.SaveChangesAsync();
            var store = new PostgreSqlEconomyTopUpIntentStore(context);

            if (mismatch < 0)
            {
                var found = await store.FindAsync(identity, default);
                found.Should().NotBeNull();
                found!.Payment.Id.Should().Be(payment.Id);
            }
            else
            {
                await FluentActions.Awaiting(() => store.FindAsync(identity, default).AsTask())
                    .Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("*bindings diverged*");
            }
        }
    }

    [Fact]
    public void ConstructorRequiresTheApplicationDbContextImplementation()
    {
        FluentActions.Invoking(() => new PostgreSqlEconomyTopUpIntentStore(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlEconomyTopUpIntentStore(
                Mock.Of<IApplicationDbContext>()))
            .Should().Throw<InvalidOperationException>();
    }

    private static EconomyTopUpIntentDraft Draft() => new(
        TenantId,
        ActorId,
        new WalletId(WalletId),
        250,
        250,
        "BRA",
        11,
        "policy-hash",
        "stripe",
        new IdempotencyKey("top-up-key"),
        Now);

    private static EconomyTopUpProviderIdentity Identity() => new(
        "stripe", "test", "acct_platform", "pi_topup", "payment_intent", "capture");

    private static EconomyTopUpProviderBinding Binding(
        Guid topUpId,
        EconomyTopUpProviderIdentity identity) => new(
        topUpId,
        identity.Provider,
        identity.ProviderEnvironment,
        identity.ProviderAccountId,
        identity.ProviderObjectId,
        identity.ProviderObjectType,
        identity.ProviderMonetaryLeg,
        EconomyTopUpProviderStatus.Processing,
        Now);

    private static EconomyTopUpProviderEvent Event(
        EconomyTopUpProviderIdentity identity,
        EconomyTopUpProviderStatus status,
        string eventId,
        DateTimeOffset occurredAt,
        Guid? postingGroupId = null) => new(
        identity,
        eventId,
        occurredAt,
        status,
        new string('a', 64),
        250,
        "USD",
        postingGroupId,
        status is EconomyTopUpProviderStatus.Failed or EconomyTopUpProviderStatus.Cancelled
            ? "provider-terminal"
            : null);

    private static void MovePaymentTo(Payment payment, PaymentStatus status)
    {
        if (status == PaymentStatus.Processing)
            return;
        if (status == PaymentStatus.Pending)
        {
            payment.MarkAsFailed("retry");
            payment.PrepareForRetry();
            return;
        }
        if (status == PaymentStatus.RequiresAction)
        {
            payment.MarkAsRequiresAction("pi_topup");
            return;
        }
        if (status == PaymentStatus.Failed)
        {
            payment.MarkAsFailed("failed");
            return;
        }
        if (status == PaymentStatus.Succeeded)
        {
            payment.MarkAsSucceeded("pi_topup", "pi_topup");
            return;
        }
        payment.Cancel("cancelled");
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
}
