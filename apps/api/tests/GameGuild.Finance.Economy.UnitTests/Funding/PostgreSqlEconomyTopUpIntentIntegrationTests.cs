using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Commerce.Payments;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace GameGuild.Finance.Economy.UnitTests.Funding;

public sealed class PostgreSqlEconomyTopUpIntentIntegrationTests
{
    private static readonly Guid TenantId = Guid.Parse("9b000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("9b000000-0000-0000-0000-000000000002");
    private static readonly Guid WalletId = Guid.Parse("9b000000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RuntimeRolePreparesBindsAndReadsWithoutDirectMutationAuthority()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("top_up_runtime");
        await using var context = Context(database.ConnectionString);
        await context.Database.MigrateAsync();
        await SeedWalletAsync(context);
        await context.Database.OpenConnectionAsync();
        await context.Database.ExecuteSqlRawAsync("SET ROLE gameguild_economy_runtime;");
        var store = new PostgreSqlEconomyTopUpIntentStore(context);

        await FluentActions.Awaiting(() => store.BindProviderAsync(Binding(Guid.NewGuid()), default).AsTask())
            .Should().ThrowAsync<KeyNotFoundException>();

        var prepared = await store.PrepareAsync(Draft("runtime-key"), default);
        await store.BindProviderAsync(Binding(prepared.Id), default);
        await store.BindProviderAsync(Binding(prepared.Id), default);
        await FluentActions.Awaiting(() => store.BindProviderAsync(
                Binding(prepared.Id) with { ProviderObjectId = "pi_rebound" }, default).AsTask())
            .Should().ThrowAsync<EconomyTopUpReplayConflictException>();
        var replay = await store.PrepareAsync(Draft("runtime-key") with { RequestedAt = Now.AddMinutes(1) }, default);
        var status = await store.GetAsync(TenantId, ActorId, prepared.Id, default);

        replay.Id.Should().Be(prepared.Id);
        replay.PaymentId.Should().Be(prepared.PaymentId);
        replay.IsDuplicate.Should().BeTrue();
        status.Should().NotBeNull();
        status!.Status.Should().Be(EconomyTopUpProviderStatus.RequiresAction);
        status.ProviderObjectId.Should().Be("pi_runtime");
        var settlement = await store.FindAsync(Identity(), default);
        settlement.Should().NotBeNull();
        settlement!.TopUp.Id.Should().Be(prepared.Id);
        settlement.Payment.Id.Should().Be(prepared.PaymentId);
        var processing = await store.ApplyAsync(
            ProviderEvent("evt_processing", Now.AddSeconds(2), EconomyTopUpProviderStatus.Processing),
            default);
        var duplicate = await store.ApplyAsync(
            ProviderEvent("evt_processing", Now.AddSeconds(2), EconomyTopUpProviderStatus.Processing),
            default);
        var stale = await store.ApplyAsync(
            ProviderEvent("evt_stale", Now.AddMilliseconds(1500), EconomyTopUpProviderStatus.RequiresAction),
            default);
        processing.Should().Be(new EconomyTopUpProviderEventResult(true, false,
            EconomyTopUpProviderStatus.Processing));
        duplicate.Should().Be(new EconomyTopUpProviderEventResult(false, true,
            EconomyTopUpProviderStatus.Processing));
        stale.Should().Be(new EconomyTopUpProviderEventResult(false, false,
            EconomyTopUpProviderStatus.Processing));
        await FluentActions.Awaiting(() => store.ApplyAsync(
                ProviderEvent("evt_wrong_amount", Now.AddSeconds(3), EconomyTopUpProviderStatus.Failed)
                    with { ProviderUsdMinorUnits = 251 },
                default).AsTask())
            .Should().ThrowAsync<PostgresException>();
        await store.ApplyAsync(
            ProviderEvent("evt_failed", Now.AddSeconds(3), EconomyTopUpProviderStatus.Failed),
            default);
        var held = await store.ApplyAsync(
            ProviderEvent("evt_succeeded", Now.AddSeconds(4), EconomyTopUpProviderStatus.Held),
            default);
        held.Status.Should().Be(EconomyTopUpProviderStatus.Held);
        (await store.GetAsync(TenantId, ActorId, prepared.Id, default))!.Status
            .Should().Be(EconomyTopUpProviderStatus.Held);
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            var nested = await store.PrepareAsync(Draft("caller-transaction"), default);
            await store.BindProviderAsync(Binding(nested.Id) with { ProviderObjectId = "pi_nested" }, default);
            await transaction.CommitAsync();
        }
        var directUpdate = async () => await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE public.economy_top_up_intents SET "HardCoinUnits" = 999
            WHERE "Id" = {prepared.Id}
            """);
        (await directUpdate.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");

        await context.Database.ExecuteSqlRawAsync("RESET ROLE;");
        var protectedUpdate = async () => await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE public.economy_top_up_intents SET "HardCoinUnits" = 999, "Version" = "Version" + 1
            WHERE "Id" = {prepared.Id}
            """);
        (await protectedUpdate.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");
        var payment = await context.Set<Payment>().AsNoTracking().SingleAsync(item => item.Id == prepared.PaymentId);
        payment.Amount.Should().Be(2.50m);
        payment.ProviderObjectId.Should().Be("pi_runtime");
        payment.Status.Should().Be(PaymentStatus.Succeeded);
    }

    [Fact]
    public async Task RuntimeRoleSerializesConcurrentRetriesAndRejectsConflictingReplay()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("top_up_concurrency");
        await using (var migrationContext = Context(database.ConnectionString))
        {
            await migrationContext.Database.MigrateAsync();
            await SeedWalletAsync(migrationContext);
        }
        await using var firstContext = Context(database.ConnectionString);
        await using var secondContext = Context(database.ConnectionString);
        await firstContext.Database.OpenConnectionAsync();
        await secondContext.Database.OpenConnectionAsync();
        await firstContext.Database.ExecuteSqlRawAsync("SET ROLE gameguild_economy_runtime;");
        await secondContext.Database.ExecuteSqlRawAsync("SET ROLE gameguild_economy_runtime;");

        var results = await Task.WhenAll(
            new PostgreSqlEconomyTopUpIntentStore(firstContext)
                .PrepareAsync(Draft("concurrent-key"), default).AsTask(),
            new PostgreSqlEconomyTopUpIntentStore(secondContext)
                .PrepareAsync(Draft("concurrent-key") with { RequestedAt = Now.AddSeconds(1) }, default).AsTask());

        results[1].Id.Should().Be(results[0].Id);
        results.Select(result => result.PaymentId).Distinct().Should().ContainSingle();
        var conflict = () => new PostgreSqlEconomyTopUpIntentStore(firstContext)
            .PrepareAsync(Draft("concurrent-key") with { HardCoinUnits = 251, UsdMinorUnits = 251 }, default)
            .AsTask();
        await conflict.Should().ThrowAsync<EconomyTopUpReplayConflictException>();

        var firstStore = new PostgreSqlEconomyTopUpIntentStore(firstContext);
        var secondStore = new PostgreSqlEconomyTopUpIntentStore(secondContext);
        await using (var transaction = await firstContext.Database.BeginTransactionAsync())
        {
            await firstStore.BindProviderAsync(Binding(results[0].Id), default);
            var competing = secondStore.BindProviderAsync(
                Binding(results[0].Id) with { ProviderObjectId = "pi_competing" }, default).AsTask();
            await Task.Delay(100);
            await transaction.CommitAsync();
            await FluentActions.Awaiting(() => competing)
                .Should().ThrowAsync<EconomyTopUpReplayConflictException>();
        }
        await firstContext.Database.ExecuteSqlRawAsync("RESET ROLE;");
        var paymentCount = await firstContext.Database.SqlQuery<long>($"""
            SELECT count(*)::bigint AS "Value" FROM public.payments
            WHERE "Id" = {results[0].PaymentId}
            """).SingleAsync();
        paymentCount.Should().Be(1);
        await secondContext.Database.ExecuteSqlRawAsync("RESET ROLE;");
    }

    private static EconomyTopUpIntentDraft Draft(string key) => new(
        TenantId,
        ActorId,
        new WalletId(WalletId),
        250,
        250,
        "BRA",
        11,
        "policy-hash",
        "stripe",
        new IdempotencyKey(key),
        Now);

    private static EconomyTopUpProviderBinding Binding(Guid topUpId) => new(
        topUpId,
        "stripe",
        "test",
        "acct_platform",
        "pi_runtime",
        "payment_intent",
        "capture",
        EconomyTopUpProviderStatus.RequiresAction,
        Now.AddSeconds(1));

    private static EconomyTopUpProviderIdentity Identity() => new(
        "stripe", "test", "acct_platform", "pi_runtime", "payment_intent", "capture");

    private static EconomyTopUpProviderEvent ProviderEvent(
        string eventId,
        DateTimeOffset occurredAt,
        EconomyTopUpProviderStatus status) => new(
        Identity(),
        eventId,
        occurredAt,
        status,
        new string('a', 64),
        250,
        "USD",
        FailureCode: status switch
        {
            EconomyTopUpProviderStatus.Failed => "payment_failed",
            EconomyTopUpProviderStatus.Cancelled => "cancelled",
            _ => null
        });

    private static Task SeedWalletAsync(ApplicationDbContext context) =>
        context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt")
            VALUES ({WalletId}, {ActorId}, {TenantId}, 1, {Now.AddMinutes(-1)});
            """);

    private static ApplicationDbContext Context(string connectionString) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);
}
