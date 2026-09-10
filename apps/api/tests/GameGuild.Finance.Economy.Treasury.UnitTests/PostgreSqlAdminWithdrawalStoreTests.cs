using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Treasury.UnitTests;

public sealed class PostgreSqlAdminWithdrawalStoreTests
{
    private static readonly DateTimeOffset Time = new(2026, 8, 10, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StoreAndAudit_PersistWithdrawalLifecycle()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("treasury_store");
        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options);
        await context.Database.MigrateAsync();

        var store = new PostgreSqlAdminWithdrawalStore(context);
        var audit = new PostgreSqlAdminWithdrawalAuditTrail(context);
        var run = CreateRun();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt")
            VALUES ({run.PlatformFeeWalletId.Value}, {run.RequestedBy}, {run.TenantId}, 1, {Time});
            """);

        store.FindPeriod(run.PeriodStart).Should().BeNull();
        store.Add(run);
        store.Get(run.Id).Should().BeEquivalentTo(run);
        store.Get(run.TenantId, run.Id).Should().BeEquivalentTo(run);
        FluentActions.Invoking(() => store.Get(Guid.Empty, run.Id)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.Get(Guid.NewGuid(), run.Id)).Should().Throw<KeyNotFoundException>();
        store.FindReplay(run.IdempotencyKey.Value, run.RequestHash).Should().BeEquivalentTo(run);
        store.FindReplay(run.TenantId, run.IdempotencyKey.Value, run.RequestHash)
            .Should().BeEquivalentTo(run);
        store.FindReplay(Guid.NewGuid(), run.IdempotencyKey.Value, run.RequestHash).Should().BeNull();
        FluentActions.Invoking(() => store.FindReplay(
                Guid.Empty, run.IdempotencyKey.Value, run.RequestHash))
            .Should().Throw<ArgumentException>();
        store.FindReplay("missing", run.RequestHash).Should().BeNull();
        store.FindPeriod(run.PeriodStart).Should().BeEquivalentTo(run);
        store.FindPeriod(run.TenantId, run.PeriodStart).Should().BeEquivalentTo(run);
        store.FindPeriod(Guid.NewGuid(), run.PeriodStart).Should().BeNull();
        FluentActions.Invoking(() => store.FindPeriod(Guid.Empty, run.PeriodStart))
            .Should().Throw<ArgumentException>();
        store.List(run.TenantId).Should().ContainSingle().Which.Id.Should().Be(run.Id);
        store.List(Guid.NewGuid()).Should().BeEmpty();
        FluentActions.Invoking(() => store.List(Guid.Empty)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.List(run.TenantId, 0))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => store.List(run.TenantId, 501))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => store.Get(Guid.NewGuid())).Should().Throw<KeyNotFoundException>();
        FluentActions.Invoking(() => store.FindReplay(run.IdempotencyKey.Value, "changed"))
            .Should().Throw<AdminWithdrawalStaleCommandException>();
        FluentActions.Invoking(() => store.Add(run with
            {
                Id = Guid.NewGuid(),
                IdempotencyKey = new IdempotencyKey("duplicate-period")
            }))
            .Should().Throw<AdminWithdrawalOverlapException>();

        var approved = run with { State = AdminWithdrawalRunState.Approved, ApprovedBy = Guid.NewGuid(), Version = 2, UpdatedAt = Time.AddMinutes(1) };
        store.Update(approved, run.Version);
        var dispatching = approved with { State = AdminWithdrawalRunState.Dispatching, DispatchSnapshotHash = "dispatch", Version = 3, UpdatedAt = Time.AddMinutes(2) };
        store.Update(dispatching, approved.Version);
        FluentActions.Invoking(() => store.RecordProviderEvent(
                "invalid-evidence", "event-hash", dispatching with
                {
                    State = AdminWithdrawalRunState.Cancelled,
                    Version = 4,
                    UpdatedAt = Time.AddMinutes(3)
                }, dispatching.Version))
            .Should().Throw<AdminWithdrawalEvidenceException>();
        var succeeded = dispatching with { State = AdminWithdrawalRunState.Succeeded, ProviderTransferId = "transfer", Version = 4, UpdatedAt = Time.AddMinutes(3) };
        FluentActions.Invoking(() => store.RecordProviderEvent(
                Guid.Empty, "event", "hash", succeeded, dispatching.Version))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.RecordProviderEvent(
                Guid.NewGuid(), "event", "hash", succeeded, dispatching.Version))
            .Should().Throw<ArgumentException>();
        store.RecordProviderEvent(run.TenantId, " event-1 ", "event-hash", succeeded, dispatching.Version);
        store.Get(run.Id).Should().BeEquivalentTo(succeeded);
        store.FindProviderEvent("event-1", "event-hash").Should().Be(run.Id);
        store.FindProviderEvent(run.TenantId, "event-1", "event-hash").Should().Be(run.Id);
        store.FindProviderEvent(Guid.NewGuid(), "event-1", "event-hash").Should().BeNull();
        FluentActions.Invoking(() => store.FindProviderEvent(
                Guid.Empty, "event-1", "event-hash"))
            .Should().Throw<ArgumentException>();
        store.FindProviderEvent("missing", "event-hash").Should().BeNull();
        FluentActions.Invoking(() => store.FindProviderEvent("event-1", "changed"))
            .Should().Throw<AdminWithdrawalEvidenceException>();
        FluentActions.Invoking(() => store.Update(dispatching, run.Version))
            .Should().Throw<AdminWithdrawalStaleCommandException>();
        context.Set<AdminWithdrawalProviderEventRow>().Add(new AdminWithdrawalProviderEventRow
        {
            TenantId = run.TenantId,
            EventId = "invalid-time",
            EventHash = "invalid-time-hash",
            RunId = run.Id,
            RecordedAt = default
        });
        await context.SaveChangesAsync();
        FluentActions.Invoking(() => store.FindProviderEvent(
                run.TenantId, "invalid-time", "invalid-time-hash"))
            .Should().Throw<AdminWithdrawalEvidenceException>();

        audit.Verify(run.Id).Should().BeFalse();
        var first = audit.Append(run.Id, "requested", run.RequestedBy, "request-hash", Time);
        var second = audit.Append(run.Id, "approved", approved.ApprovedBy, "approval-hash", Time.AddMinutes(1));
        audit.Events(run.Id).Should().Equal(first, second);
        audit.Events(run.TenantId, run.Id).Should().Equal(first, second);
        audit.Verify(run.TenantId, run.Id).Should().BeTrue();
        audit.Events(Guid.Empty).Should().BeEmpty();
        audit.Events(Guid.NewGuid()).Should().BeEmpty();
        FluentActions.Invoking(() => audit.Events(Guid.Empty, run.Id)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => audit.Append(
                Guid.NewGuid(), "kind", null, "evidence", Time))
            .Should().Throw<KeyNotFoundException>();
        FluentActions.Invoking(() => audit.Append(
                Guid.Empty, run.Id, "kind", null, "evidence", Time))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => audit.Append(
                run.TenantId, Guid.Empty, "kind", null, "evidence", Time))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => audit.Verify(Guid.Empty, run.Id)).Should().Throw<ArgumentException>();
        audit.Verify(run.Id).Should().BeTrue();
    }

    [Fact]
    public async Task FencingAllocator_UsesTheProtectedDurableSequence()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("treasury_fencing");
        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options);
        await context.Database.MigrateAsync();
        var allocator = new PostgreSqlAdminWithdrawalFencingTokenAllocator(context);

        var first = await allocator.AllocateAsync();
        var second = await allocator.AllocateAsync();

        first.Should().BePositive();
        second.Should().Be(first + 1);
        await context.Database.ExecuteSqlRawAsync("""
            CREATE OR REPLACE FUNCTION economy_private.next_admin_withdrawal_fencing_token_v1()
            RETURNS bigint
            LANGUAGE sql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS 'SELECT 0::bigint';
            """);
        await FluentActions.Awaiting(() => allocator.AllocateAsync().AsTask())
            .Should().ThrowAsync<AdminWithdrawalStaleCommandException>();
    }

    private static AdminWithdrawalRun CreateRun(DateOnly? period = null, string? key = null) => new(
        Guid.NewGuid(), Guid.NewGuid(), new IdempotencyKey(key ?? $"withdrawal-{Guid.NewGuid():N}"), "request-hash",
        period ?? new DateOnly(2026, 8, 1), Guid.NewGuid(), null, WalletId.New(),
        new CoinAmount(CurrencyCode.HardCoin, 750), "stripe:platform:cash", "destination",
        AdminWithdrawalRunState.PendingApproval, 1, 1, 1, new ReserveVersion(1), 1,
        new PolicyVersion(1), null, null, Time, Time);

}
