using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GameGuild.Finance.Economy.Marketplace.UnitTests;

public sealed class PostgreSqlMarketplaceWriterTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SettlementAndRefund_PreserveProvenanceAndRecordDebtWhenProceedsAreUnavailable()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("marketplace_writer");
        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(database.ConnectionString)
                .Options);
        await context.Database.MigrateAsync();
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();

        var ids = TestIds.Create();
        await SeedAsync(connection, ids);
        await ExecuteAsync(connection, $$"""
            SELECT * FROM economy_private.reserve_marketplace_fifo_fragments_v1(
                '{{ids.Settlement}}', '{{ids.BuyerWallet}}',
                '[{"currency":1,"units":100}]'::jsonb, 7, '{{Now:O}}');
            """);

        var settlement = await ReceiptAsync(connection, $$"""
            SELECT * FROM economy_private.post_marketplace_settlement_v1(
                '{{ids.Capability}}', '{{ids.Actor}}', '{{ids.Tenant}}', '{{ids.Settlement}}',
                '{{ids.SettlementPosting}}', 'marketplace-settlement', 1, 1,
                '{{ids.SettlementRisk}}', 'marketplace-settlement-risk', 1,
                '{{ids.Buyer}}', '{{ids.BuyerWallet}}', '{{ids.Seller}}', '{{ids.SellerWallet}}',
                '{{ids.FeeWallet}}', 1, 1,
                '{"order_id":"{{ids.Order}}","line_item_id":"{{ids.LineItem}}","product_id":"{{ids.Product}}","pricing_version_id":"{{ids.Pricing}}","price_version":1,"quantity":1,"unit_price":1,"fiat_currency":"USD","snapshot_hash":"order-snapshot"}'::jsonb,
                '[{"currency":1,"units":100,"seller_units":90,"platform_fee_units":10}]'::jsonb,
                (SELECT jsonb_agg("Id") FROM public.economy_fragment_reservations
                 WHERE "OperationId" = '{{ids.Settlement}}'),
                '{{ids.Entitlement}}', '{{Now.AddDays(7):O}}', '{{Now:O}}',
                '{{ids.SettlementReceipt}}', 'settlement-receipt', 0, 'US',
                '["settlement-evidence"]'::jsonb);
            """);
        settlement.PostingId.Should().Be(ids.SettlementPosting);
        settlement.Sequence.Should().BePositive();
        settlement.Duplicate.Should().BeFalse();
        (await ScalarAsync<long>(connection, $"""
            SELECT count(*) FROM public.economy_marketplace_settlement_credits
            WHERE "SettlementId" = '{ids.Settlement}';
            """)).Should().Be(2);

        var fifo = new PostgreSqlFifoFragmentReservationGateway(context);
        Action spendHeldProceeds = () => fifo.Reserve(new FifoFragmentReservationRequest(
            Guid.NewGuid(),
            new WalletId(ids.SellerWallet),
            CurrencyCode.HardCoin,
            ProvenanceKind.EarnedHard,
            new CoinAmount(CurrencyCode.HardCoin, 1),
            PersistedFragmentReservationPurpose.Spend,
            Now.AddMinutes(1)));
        spendHeldProceeds.Should().Throw<RegisteredPostingRejectedException>();

        // An active reservation represents proceeds which another value operation
        // has already claimed. Refund must not steal those fragments; it records a
        // receivable while still restoring the buyer from immutable provenance.
        await ExecuteAsync(connection, $"""
            INSERT INTO public.economy_fragment_reservations (
                "Id", "OperationId", "ParentLotId", "WalletId", "Currency", "Purpose", "Status",
                "RootSourceStampId", "ReversalEpoch", "StartInclusive", "EndExclusive", "ReservedAt", "TerminalAt")
            SELECT '{ids.BlockingReservation}', '{ids.BlockingOperation}', credit."CreditLotId",
                credit."WalletId", credit."Currency", 4, 1, ranges."RootSourceStampId",
                ranges."ReversalEpoch", ranges."StartInclusive", ranges."EndExclusive",
                '{Now.AddMinutes(1):O}', NULL
            FROM public.economy_marketplace_settlement_credits credit
            JOIN public.economy_fragment_root_ranges ranges ON ranges."CreditLotId" = credit."CreditLotId"
            WHERE credit."SettlementId" = '{ids.Settlement}' AND credit."Purpose" = 1;
            """);

        var refund = await ReceiptAsync(connection, $$"""
            SELECT * FROM economy_private.post_marketplace_refund_v1(
                '{{ids.Capability}}', '{{ids.Actor}}', '{{ids.Tenant}}', '{{ids.Refund}}',
                '{{ids.Settlement}}', '{{ids.RefundPosting}}', 'marketplace-refund', 1, 1,
                '{{ids.RefundRisk}}', 'marketplace-refund-risk', 1, '{{ids.Buyer}}', 1, 1, 1,
                '[{"currency":1,"units":100}]'::jsonb,
                'CUSTOMER_REQUEST', 'refund-reason', '{{Now.AddMinutes(2):O}}',
                '{{ids.RefundReceipt}}', 'refund-receipt', 0, 'US',
                '["refund-evidence"]'::jsonb);
            """);
        refund.PostingId.Should().Be(ids.RefundPosting);
        refund.Sequence.Should().BeGreaterThan(settlement.Sequence);
        refund.Duplicate.Should().BeFalse();

        (await ScalarAsync<long>(connection, $"""
            SELECT COALESCE(sum("AmountUnits"), 0)
            FROM public.economy_marketplace_refund_debts
            WHERE "RefundId" = '{ids.Refund}' AND "ResponsibleWalletId" = '{ids.SellerWallet}';
            """)).Should().Be(90);
        (await ScalarAsync<long>(connection, $"""
            SELECT COALESCE(sum(lot."AmountUnits"), 0)
            FROM public.economy_journal_entries entry
            JOIN public.economy_journal_lines line ON line."JournalEntryId" = entry."Id"
            JOIN public.economy_credit_lots lot ON lot."Id" = line."CreditLotId"
            WHERE entry."PostingGroupId" = '{ids.RefundPosting}'
              AND line."WalletId" = '{ids.BuyerWallet}' AND line."Side" = 2;
            """)).Should().Be(100);
        (await ScalarAsync<long>(connection, $"""
            SELECT count(*) FROM public.economy_credit_lots restored
            JOIN public.economy_lot_lineage_edges lineage ON lineage."ChildLotId" = restored."Id"
            WHERE restored."WalletId" = '{ids.BuyerWallet}'
              AND restored."Provenance" = 1
              AND restored."OriginalMaturesAt" = '{Now.AddDays(-30):O}';
            """)).Should().BePositive();
        (await ScalarAsync<int>(connection, $"""
            SELECT "Status" FROM public.economy_marketplace_settlements WHERE "Id" = '{ids.Settlement}';
            """)).Should().Be(3);
        (await ScalarAsync<int>(connection, $"""
            SELECT "EntitlementStatus" FROM public.economy_marketplace_settlements WHERE "Id" = '{ids.Settlement}';
            """)).Should().Be(3);
        (await ScalarAsync<long>(connection, $"""
            SELECT count(*) FROM public.economy_marketplace_outbox
            WHERE "SettlementId" = '{ids.Settlement}'
              AND "MessageType" = 'marketplace.entitlement.revoke.v1';
            """)).Should().Be(1);
        (await ScalarAsync<long>(connection, $"""
            SELECT count(*) FROM (
                SELECT line."Currency",
                    sum(CASE WHEN line."Side" = 1 THEN line."AmountUnits" ELSE -line."AmountUnits" END) balance
                FROM public.economy_journal_entries entry
                JOIN public.economy_journal_lines line ON line."JournalEntryId" = entry."Id"
                WHERE entry."PostingGroupId" IN ('{ids.SettlementPosting}', '{ids.RefundPosting}')
                GROUP BY entry."PostingGroupId", line."Currency"
                HAVING sum(CASE WHEN line."Side" = 1 THEN line."AmountUnits" ELSE -line."AmountUnits" END) <> 0
            ) imbalance;
            """)).Should().Be(0);
    }

    private static async Task SeedAsync(NpgsqlConnection connection, TestIds ids)
    {
        await ExecuteAsync(connection, $"""
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt") VALUES
                ('{ids.BuyerWallet}', '{ids.Buyer}', '{ids.Tenant}', 1, '{Now.AddDays(-40):O}'),
                ('{ids.SellerWallet}', '{ids.Seller}', '{ids.Tenant}', 1, '{Now.AddDays(-40):O}'),
                ('{ids.FeeWallet}', '{ids.FeeOwner}', '{ids.Tenant}', 1, '{Now.AddDays(-40):O}');
            INSERT INTO public.economy_accounts ("Id", "WalletId", "Code", "Currency", "Provenance", "CreatedAt") VALUES
                ('{Guid.NewGuid()}', '{ids.BuyerWallet}', 2, 1, 1, '{Now.AddDays(-40):O}'),
                ('{Guid.NewGuid()}', '{ids.SellerWallet}', 3, 1, 2, '{Now.AddDays(-40):O}'),
                ('{Guid.NewGuid()}', '{ids.FeeWallet}', 3, 1, 2, '{Now.AddDays(-40):O}'),
                ('{Guid.NewGuid()}', NULL, 13, 1, NULL, '{Now.AddDays(-40):O}');
            INSERT INTO public.economy_registered_capabilities (
                "Id", "Name", "AllowedTemplateKinds", "IsEnabled", "CreatedAt", "RevokedAt")
            VALUES ('{ids.Capability}', 'marketplace-production-writer', '[25,26]'::jsonb,
                true, '{Now.AddDays(-1):O}', NULL);
            INSERT INTO public.economy_risk_counters (
                "Id", "Dimension", "SubjectHash", "Operation", "Currency", "WindowStartedAt", "WindowEndsAt",
                "CounterVersion", "MaxUnits", "UsedUnits", "UpdatedAt") VALUES
                ('{ids.SettlementCounter}', 1, 'marketplace-settlement', 1, 1,
                    '{Now.AddHours(-1):O}', '{Now.AddHours(1):O}', 1, 1000, 0, '{Now.AddHours(-1):O}'),
                ('{ids.RefundCounter}', 1, 'marketplace-refund', 1, 1,
                    '{Now.AddHours(-1):O}', '{Now.AddHours(1):O}', 1, 1000, 0, '{Now.AddHours(-1):O}');
            INSERT INTO public.economy_risk_decisions (
                "Id", "Outcome", "OperationFingerprint", "ActorHash", "TemplateKind", "SourceWalletId",
                "DestinationWalletId", "Currency", "AmountUnits", "CurrencyLegs", "SourceRoots",
                "ProviderReferenceHash", "PolicyVersion", "ReserveVersion", "ReserveAuthorizationEpoch",
                "FeatureVersion", "KillSwitchEpoch", "CounterVersion", "EntityGraphVersion",
                "EntityGraphEvidenceHash", "ReasonCodes", "IssuedAt", "ExpiresAt") VALUES
                ('{ids.SettlementRisk}', 1, 'marketplace-settlement-risk', 'actor', 25,
                    '{ids.BuyerWallet}', '{ids.SellerWallet}', 1, 100, '[]', '[]', 'provider',
                    1, 1, 1, 1, 0, 1, 1, 'graph', '[]', '{Now.AddMinutes(-5):O}', '{Now.AddHours(1):O}'),
                ('{ids.RefundRisk}', 1, 'marketplace-refund-risk', 'actor', 26,
                    '{ids.SellerWallet}', '{ids.BuyerWallet}', 1, 100, '[]', '[]', 'provider',
                    1, 1, 1, 1, 0, 1, 1, 'graph', '[]', '{Now.AddMinutes(-5):O}', '{Now.AddHours(1):O}');
            INSERT INTO public.economy_risk_counter_reservations (
                "Id", "RiskDecisionId", "RiskCounterId", "AmountUnits", "ReservedAt", "ExpiresAt",
                "InputFingerprint", "ReservationGroupId", "Status") VALUES
                ('{Guid.NewGuid()}', '{ids.SettlementRisk}', '{ids.SettlementCounter}', 100,
                    '{Now.AddMinutes(-4):O}', '{Now.AddHours(1):O}', 'settlement-reservation',
                    '{Guid.NewGuid()}', 1),
                ('{Guid.NewGuid()}', '{ids.RefundRisk}', '{ids.RefundCounter}', 100,
                    '{Now.AddMinutes(-4):O}', '{Now.AddHours(1):O}', 'refund-reservation',
                    '{Guid.NewGuid()}', 1);
            INSERT INTO public.economy_capability_receipts (
                "Id", "TenantId", "ActorId", "SubjectReference", "JurisdictionCode", "Capability",
                "OperationFingerprint", "PolicyVersion", "ReserveVersion", "RiskDecisionId",
                "KillSwitchEpoch", "ProviderHash", "DestinationHash", "SourceRootHashes", "EvidenceHashes",
                "IssuedAt", "ExpiresAt", "ReceiptHash", "KeyId", "Signature") VALUES
                ('{ids.SettlementReceipt}', '{ids.Tenant}', '{ids.Actor}', 'buyer', 'US', 8,
                    'marketplace-settlement-risk', 1, 1, '{ids.SettlementRisk}', 0, 'provider', 'destination',
                    '["root"]', '["settlement-evidence"]', '{Now.AddMinutes(-1):O}', '{Now.AddMinutes(5):O}',
                    'settlement-receipt', 'test-key', 'signature'),
                ('{ids.RefundReceipt}', '{ids.Tenant}', '{ids.Actor}', 'buyer', 'US', 11,
                    'marketplace-refund-risk', 1, 1, '{ids.RefundRisk}', 0, 'provider', 'destination',
                    '["root"]', '["refund-evidence"]', '{Now.AddMinutes(-1):O}', '{Now.AddMinutes(5):O}',
                    'refund-receipt', 'test-key', 'signature');
            INSERT INTO public.economy_capability_receipt_consumptions (
                "Id", "ReceiptId", "TenantId", "ActorId", "OperationFingerprint", "KillSwitchEpoch", "ConsumedAt") VALUES
                ('{Guid.NewGuid()}', '{ids.SettlementReceipt}', '{ids.Tenant}', '{ids.Actor}',
                    'marketplace-settlement-risk', 0, '{Now:O}'),
                ('{Guid.NewGuid()}', '{ids.RefundReceipt}', '{ids.Tenant}', '{ids.Actor}',
                    'marketplace-refund-risk', 0, '{Now.AddMinutes(2):O}');
            INSERT INTO public.economy_marketplace_currency_policy_versions (
                "TenantId", "ProductId", "Version", "SellerId", "Mode", "HardPriceUnits",
                "SoftPriceUnits", "PlatformFeePpm", "EffectiveAt", "ExpiresAt", "PlatformFeeWalletId",
                "RefundHoldTicks", "CanonicalPayload", "PayloadHash", "KeyId", "Signature",
                "ProposedBy", "ApprovedBy", "PublishedAt")
            VALUES ('{ids.Tenant}', '{ids.Product}', 1, '{ids.Seller}', 1, 100, 0, 100000,
                '{Now.AddDays(-1):O}', '{Now.AddDays(30):O}', '{ids.FeeWallet}', {TimeSpan.FromDays(7).Ticks},
                'policy', 'policy-hash', 'kms-key', 'signature', '{ids.PolicyProposer}',
                '{ids.PolicyApprover}', '{Now.AddDays(-1):O}');
            INSERT INTO public.economy_source_stamps (
                "Id", "SourceKind", "InternalSourceId", "SourceLegId", "Provider", "ProviderReference",
                "EvidenceHash", "Provenance", "State", "ActorId", "TenantId", "PostingReferenceId",
                "PolicyVersion", "AuthoritativeUnits", "ObservedAt", "ConfirmedAt")
            VALUES ('{ids.Root}', 'test', 'marketplace-root', 'principal', NULL, NULL,
                'source-evidence', 1, 2, '{ids.Actor}', '{ids.Tenant}', NULL, 1, 100,
                '{Now.AddDays(-30):O}', '{Now.AddDays(-30):O}');
            INSERT INTO public.economy_credit_lots (
                "Id", "WalletId", "RootSourceStampId", "Currency", "AmountUnits", "Provenance",
                "CreditedAt", "ConfirmedAt", "OriginalMaturesAt", "CashOutEligible",
                "JournalSequence", "State", "ReversalEpoch")
            VALUES ('{ids.BuyerLot}', '{ids.BuyerWallet}', '{ids.Root}', 1, 100, 1,
                '{Now.AddDays(-30):O}', '{Now.AddDays(-30):O}', '{Now.AddDays(-30):O}',
                false, 1, 1, 0);
            INSERT INTO public.economy_root_reversal_states (
                "RootSourceStampId", "Epoch", "CumulativeProviderUnits", "ReversedUnits",
                "State", "TargetedRanges", "UpdatedAt")
            VALUES ('{ids.Root}', 0, 0, 0, 'active', '[]', '{Now.AddDays(-30):O}');
            INSERT INTO public.economy_fragment_root_ranges (
                "Id", "RootSourceStampId", "CreditLotId", "EntryAllocationId",
                "StartInclusive", "EndExclusive", "ReversalEpoch")
            VALUES ('{Guid.NewGuid()}', '{ids.Root}', '{ids.BuyerLot}', NULL, 0, 100000, 0);
            """);
    }

    private static async Task<PostingReceipt> ReceiptAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        return new PostingReceipt(reader.GetGuid(0), reader.GetInt64(1), reader.GetBoolean(3));
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        if (value is null or DBNull) return default!;
        if (value is T typed) return typed;
        return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed record PostingReceipt(Guid PostingId, long Sequence, bool Duplicate);

    private sealed record TestIds(
        Guid Tenant, Guid Actor, Guid Buyer, Guid Seller, Guid FeeOwner,
        Guid BuyerWallet, Guid SellerWallet, Guid FeeWallet,
        Guid Capability, Guid SettlementCounter, Guid RefundCounter,
        Guid SettlementRisk, Guid RefundRisk, Guid SettlementReceipt, Guid RefundReceipt,
        Guid Product, Guid PolicyProposer, Guid PolicyApprover, Guid Root, Guid BuyerLot,
        Guid Settlement, Guid SettlementPosting, Guid Order, Guid LineItem, Guid Pricing,
        Guid Entitlement, Guid BlockingReservation, Guid BlockingOperation,
        Guid Refund, Guid RefundPosting)
    {
        public static TestIds Create() => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    }
}
