using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using GameGuild;
using GameGuild.API.Database;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace GameGuild.Finance.Economy.Payouts.UnitTests;

public sealed class PostgreSqlPayoutAuthorizationEvidenceWriterTests
{
    private static readonly DateTimeOffset Time = new(2026, 8, 26, 3, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task AppendAsync_PersistsOnlyEvidenceBoundToAConsumedPayoutReceipt()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("payout_auth_evidence");
        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(database.ConnectionString).Options);
        await context.Database.MigrateAsync();

        var operationId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var payeeId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var riskDecisionId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        const string operationFingerprint = "payout-reservation-operation";
        var operationFingerprintHash = Hash(operationFingerprint);
        var receiptHash = new string('b', 64);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            SET session_replication_role = replica;
            INSERT INTO public.economy_payout_operations (
                "Id", "TenantId", "IdempotencyKey", "RequestHash", "ActorId", "PayeeId",
                "WalletId", "AmountUnits", "ProviderAccountId", "DestinationHash",
                "ProviderBindingHash", "EligibilityHash", "DispatchSnapshotHash",
                "ProviderPayoutId", "State", "Version", "FencingToken", "KillSwitchEpoch",
                "ReserveVersion", "ReserveAuthorizationEpoch", "PolicyVersion", "RiskDecisionId",
                "CreatedAt", "UpdatedAt")
            VALUES (
                {operationId}, {tenantId}, {'p' + operationId.ToString("N")}, {'r' + operationId.ToString("N")},
                {actorId}, {payeeId}, {walletId}, 100, 'acct_test', {new string('d', 64)},
                {new string('e', 64)}, {new string('f', 64)}, NULL, NULL, 1, 1, 1, 3,
                1, 1, 1, {riskDecisionId}, {Time}, {Time});
            INSERT INTO public.economy_capability_receipts (
                "Id", "TenantId", "ActorId", "SubjectReference", "JurisdictionCode",
                "Capability", "OperationFingerprint", "PolicyVersion", "ReserveVersion",
                "RiskDecisionId", "KillSwitchEpoch", "ProviderHash", "DestinationHash",
                "SourceRootHashes", "EvidenceHashes", "IssuedAt", "ExpiresAt", "ReceiptHash",
                "KeyId", "Signature")
            VALUES (
                {receiptId}, {tenantId}, {actorId}, 'subject', 'BR', 9, {operationFingerprint},
                1, 1, {riskDecisionId}, 3, {new string('1', 64)}, {new string('d', 64)},
                '[]'::jsonb, '[]'::jsonb, {Time}, {Time.AddMinutes(5)}, {receiptHash},
                'test-key', 'test-signature');
            INSERT INTO public.economy_capability_receipt_consumptions (
                "Id", "ReceiptId", "TenantId", "ActorId", "OperationFingerprint",
                "KillSwitchEpoch", "ConsumedAt")
            VALUES (
                {Guid.NewGuid()}, {receiptId}, {tenantId}, {actorId}, {operationFingerprint},
                3, {Time.AddSeconds(1)});
            SET session_replication_role = origin;
            """);
        var writer = new PostgreSqlPayoutAuthorizationEvidenceWriter(context);
        var evidence = new PayoutAuthorizationEvidence(
            operationId,
            tenantId,
            actorId,
            PayoutAuthorizationPhase.Reservation,
            riskDecisionId,
            new string('a', 64),
            operationFingerprintHash,
            receiptId,
            receiptHash,
            Time.AddSeconds(1));

        await writer.AppendAsync(evidence);

        var count = await context.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS \"Value\" FROM public.economy_payout_authorization_evidence")
            .SingleAsync();
        count.Should().Be(1);
        var persisted = await context.Set<PayoutAuthorizationEvidenceRow>().AsNoTracking().SingleAsync();
        persisted.OperationId.Should().Be(operationId);
        persisted.Phase.Should().Be(PayoutAuthorizationPhase.Reservation);
        persisted.TenantId.Should().Be(tenantId);
        persisted.ActorId.Should().Be(actorId);
        persisted.RiskDecisionId.Should().Be(riskDecisionId);
        persisted.ReauthenticationEvidenceHash.Should().Be(new string('a', 64));
        persisted.OperationFingerprintHash.Should().Be(operationFingerprintHash);
        persisted.CapabilityReceiptId.Should().Be(receiptId);
        persisted.CapabilityReceiptHash.Should().Be(receiptHash);
        persisted.RecordedAt.Should().Be(Time.AddSeconds(1));
        await FluentActions.Invoking(() => writer.AppendAsync(evidence))
            .Should().ThrowAsync<PostgresException>();
        await FluentActions.Invoking(() => writer.AppendAsync(evidence with { TenantId = Guid.NewGuid() }))
            .Should().ThrowAsync<PostgresException>();
        await FluentActions.Invoking(() => writer.AppendAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_RequiresTheApplicationRelationalDbContext()
    {
        FluentActions.Invoking(() => new PostgreSqlPayoutAuthorizationEvidenceWriter(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlPayoutAuthorizationEvidenceWriter(new NonRelationalContext()))
            .Should().Throw<InvalidOperationException>();
    }

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class NonRelationalContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
