using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Treasury.UnitTests;

public sealed class PostgreSqlAdminWithdrawalProviderIntegrityTests
{
    [Fact]
    public async Task Store_RejectsDuplicateAndInvalidProviderEvidence()
    {
        var now = new DateTimeOffset(2026, 8, 10, 18, 0, 0, TimeSpan.Zero);
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("treasury_provider");
        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options);
        await context.Database.MigrateAsync();

        var run = CreateRun(now);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt")
            VALUES ({run.PlatformFeeWalletId.Value}, {run.RequestedBy}, {run.TenantId}, 1, {now});
            """);
        var store = new PostgreSqlAdminWithdrawalStore(context);
        store.Add(run);
        var approved = run with { State = AdminWithdrawalRunState.Approved, ApprovedBy = Guid.NewGuid(), Version = 2, UpdatedAt = now.AddMinutes(1) };
        store.Update(approved, run.Version);
        var dispatching = approved with { State = AdminWithdrawalRunState.Dispatching, DispatchSnapshotHash = "dispatch", Version = 3, UpdatedAt = now.AddMinutes(2) };
        store.Update(dispatching, approved.Version);
        var succeeded = dispatching with { State = AdminWithdrawalRunState.Succeeded, ProviderTransferId = "transfer", Version = 4, UpdatedAt = now.AddMinutes(3) };
        store.RecordProviderEvent("event-1", "event-hash", succeeded, dispatching.Version);

        var secondRun = CreateRun(now) with
        {
            PeriodStart = new DateOnly(2026, 9, 1),
            IdempotencyKey = new IdempotencyKey("provider-integrity-second")
        };
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt")
            VALUES ({secondRun.PlatformFeeWalletId.Value}, {secondRun.RequestedBy}, {secondRun.TenantId}, 1, {now});
            """);
        store.Add(secondRun);
        var secondApproved = secondRun with { State = AdminWithdrawalRunState.Approved, ApprovedBy = Guid.NewGuid(), Version = 2, UpdatedAt = now.AddMinutes(1) };
        store.Update(secondApproved, secondRun.Version);
        var secondDispatching = secondApproved with { State = AdminWithdrawalRunState.Dispatching, DispatchSnapshotHash = "dispatch-2", Version = 3, UpdatedAt = now.AddMinutes(2) };
        store.Update(secondDispatching, secondApproved.Version);
        var secondSucceeded = secondDispatching with { State = AdminWithdrawalRunState.Succeeded, ProviderTransferId = "transfer-2", Version = 4, UpdatedAt = now.AddMinutes(3) };


        store.RecordProviderEvent("event-1", "event-hash", secondSucceeded, secondDispatching.Version);
        store.FindProviderEvent(run.TenantId, "event-1", "event-hash").Should().Be(run.Id);
        store.FindProviderEvent(secondRun.TenantId, "event-1", "event-hash").Should().Be(secondRun.Id);

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.economy_admin_withdrawal_provider_events ("TenantId", "EventId", "EventHash", "RunId", "RecordedAt")
            VALUES ({run.TenantId}, {"invalid-time"}, {"invalid-time-hash"}, {run.Id}, {DateTimeOffset.MinValue});
            """);
        FluentActions.Invoking(() => store.FindProviderEvent("invalid-time", "invalid-time-hash"))
            .Should().Throw<AdminWithdrawalEvidenceException>();
    }

    private static AdminWithdrawalRun CreateRun(DateTimeOffset now) => new(
        Guid.NewGuid(), Guid.NewGuid(), new IdempotencyKey("provider-integrity"), "request-hash",
        new DateOnly(2026, 8, 1), Guid.NewGuid(), null, WalletId.New(),
        new CoinAmount(CurrencyCode.HardCoin, 1), "asset", "destination",
        AdminWithdrawalRunState.PendingApproval, 1, 1, 1, new ReserveVersion(1), 1,
        new PolicyVersion(1), null, null, now, now);

}
