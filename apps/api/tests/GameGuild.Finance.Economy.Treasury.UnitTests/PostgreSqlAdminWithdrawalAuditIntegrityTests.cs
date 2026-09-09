using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Treasury.UnitTests;

public sealed class PostgreSqlAdminWithdrawalAuditIntegrityTests
{
    [Fact]
    public async Task AuditTrail_VerifiesAndRejectsCorruptedRowsAndReaders()
    {
        var now = new DateTimeOffset(2026, 8, 10, 18, 0, 0, TimeSpan.Zero);
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("treasury_audit");
        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options);
        await context.Database.MigrateAsync();

        var store = new PostgreSqlAdminWithdrawalStore(context);
        var audit = new PostgreSqlAdminWithdrawalAuditTrail(context);

        async Task<AdminWithdrawalRun> AddRunAsync(int month)
        {
            var run = new AdminWithdrawalRun(
                Guid.NewGuid(), Guid.NewGuid(), new IdempotencyKey($"audit-{month}"), "request",
                new DateOnly(2026, month, 1), Guid.NewGuid(), null, WalletId.New(),
                new CoinAmount(CurrencyCode.HardCoin, 1), "asset", "destination",
                AdminWithdrawalRunState.PendingApproval, 1, 1, 1, new ReserveVersion(1), 1,
                new PolicyVersion(1), null, null, now, now);
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt")
                VALUES ({run.PlatformFeeWalletId.Value}, {run.RequestedBy}, {run.TenantId}, 1, {now});
                """);
            store.Add(run);
            return run;
        }

        async Task InsertAsync(Guid runId, long sequence, string previousHash, string hash) =>
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO public.economy_admin_withdrawal_audit_events
                    ("TenantId", "RunId", "Sequence", "Kind", "ActorId", "Evidence", "OccurredAt", "PreviousHash", "Hash")
                SELECT "TenantId", {runId}, {sequence}, {"integrity"}, {null}, {"evidence"}, {now}, {previousHash}, {hash}
                FROM public.economy_admin_withdrawal_runs WHERE "Id" = {runId};
                """);

        var valid = await AddRunAsync(8);
        audit.Append(valid.Id, "system", null, "automated", now);
        audit.Verify(valid.Id).Should().BeTrue();

        var badSequence = await AddRunAsync(9);
        await InsertAsync(badSequence.Id, 2, new string('0', 64), new string('1', 64));
        audit.Verify(badSequence.Id).Should().BeFalse();

        var badPrevious = await AddRunAsync(10);
        await InsertAsync(badPrevious.Id, 1, new string('1', 64), new string('2', 64));
        audit.Verify(badPrevious.Id).Should().BeFalse();

        var badHash = await AddRunAsync(11);
        await InsertAsync(badHash.Id, 1, new string('0', 64), new string('3', 64));
        audit.Verify(badHash.Id).Should().BeFalse();

        await context.Database.ExecuteSqlRawAsync("""
            CREATE OR REPLACE FUNCTION economy_private.read_admin_withdrawal_audit_events_v1(p_run_id uuid)
            RETURNS SETOF public.economy_admin_withdrawal_audit_events
            LANGUAGE sql STABLE SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
                SELECT * FROM public.economy_admin_withdrawal_audit_events
                WHERE "RunId" <> p_run_id
                ORDER BY "Sequence"
                LIMIT 1
            $function$;
            """);
        audit.Verify(valid.Id).Should().BeTrue(
            "tenant-scoped audit verification no longer trusts the legacy global reader function");
    }

}
