using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Treasury.UnitTests;

public sealed class AdminWithdrawalPersistenceCoverageCompletionTests
{
    [Fact]
    public void PositionalViewsAndPersistenceRows_ExposeEveryMappedTenantField()
    {
        var runId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var view = new AdminWithdrawalAuditView(runId, true, []);
        var providerRow = new AdminWithdrawalProviderEventRow { TenantId = tenantId };
        var auditRow = new AdminWithdrawalAuditEventRow { TenantId = tenantId };

        view.RunId.Should().Be(runId);
        providerRow.TenantId.Should().Be(tenantId);
        auditRow.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void DefaultTenantStoreContract_ValidatesTenantAndFiltersLegacyResults()
    {
        var tenantId = Guid.NewGuid();
        var run = CreateRun(tenantId);
        IAdminWithdrawalStore store = new LegacyStore(run);

        FluentActions.Invoking(() => store.List(tenantId)).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => store.FindReplay(Guid.Empty, "key", "hash"))
            .Should().Throw<ArgumentException>();
        store.FindReplay(tenantId, "key", "hash").Should().BeSameAs(run);
        store.FindReplay(Guid.NewGuid(), "key", "hash").Should().BeNull();
        FluentActions.Invoking(() => store.FindPeriod(Guid.Empty, run.PeriodStart))
            .Should().Throw<ArgumentException>();
        store.FindPeriod(tenantId, run.PeriodStart).Should().BeSameAs(run);
        store.FindPeriod(Guid.NewGuid(), run.PeriodStart).Should().BeNull();
        FluentActions.Invoking(() => store.Get(Guid.Empty, run.Id)).Should().Throw<ArgumentException>();
        store.Get(tenantId, run.Id).Should().BeSameAs(run);
        FluentActions.Invoking(() => store.Get(Guid.NewGuid(), run.Id))
            .Should().Throw<KeyNotFoundException>();
        FluentActions.Invoking(() => store.FindProviderEvent(Guid.Empty, "event", "hash"))
            .Should().Throw<ArgumentException>();
        store.FindProviderEvent(tenantId, "event", "hash").Should().Be(run.Id);
        store.FindProviderEvent(Guid.NewGuid(), "event", "hash").Should().BeNull();
        FluentActions.Invoking(() => store.RecordProviderEvent(
                Guid.Empty, "event", "hash", run, 1))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.RecordProviderEvent(
                Guid.NewGuid(), "event", "hash", run, 1))
            .Should().Throw<ArgumentException>();
        store.RecordProviderEvent(tenantId, "event", "hash", run, 1);
    }

    [Fact]
    public void DefaultTenantAuditContract_ValidatesTenantAndDelegatesLegacyOperations()
    {
        var tenantId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        IAdminWithdrawalAuditTrail audit = new LegacyAudit();

        FluentActions.Invoking(() => audit.Append(
                Guid.Empty, runId, "kind", null, "evidence", DateTimeOffset.UtcNow))
            .Should().Throw<ArgumentException>();
        audit.Append(tenantId, runId, "kind", null, "evidence", DateTimeOffset.UtcNow)
            .RunId.Should().Be(runId);
        FluentActions.Invoking(() => audit.Events(Guid.Empty, runId))
            .Should().Throw<ArgumentException>();
        audit.Events(tenantId, runId).Should().ContainSingle();
        FluentActions.Invoking(() => audit.Verify(Guid.Empty, runId))
            .Should().Throw<ArgumentException>();
        audit.Verify(tenantId, runId).Should().BeTrue();
    }

    [Fact]
    public void InMemoryList_RejectsEveryInvalidBound()
    {
        var store = new InMemoryAdminWithdrawalStore();

        FluentActions.Invoking(() => store.List(Guid.Empty)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.List(Guid.NewGuid(), 0))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => store.List(Guid.NewGuid(), 501))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    private static AdminWithdrawalRun CreateRun(Guid tenantId) => new(
        Guid.NewGuid(), tenantId, new IdempotencyKey("legacy"), "hash",
        new DateOnly(2026, 8, 1), Guid.NewGuid(), null, WalletId.New(),
        new CoinAmount(CurrencyCode.HardCoin, 1), "asset", "destination",
        AdminWithdrawalRunState.PendingApproval, 1, 1, 1, new ReserveVersion(1), 1,
        new PolicyVersion(1), null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private sealed class LegacyStore(AdminWithdrawalRun run) : IAdminWithdrawalStore
    {
        public AdminWithdrawalRun? FindReplay(string key, string requestHash) => run;
        public AdminWithdrawalRun? FindPeriod(DateOnly periodStart) => run;
        public void Add(AdminWithdrawalRun value) { }
        public AdminWithdrawalRun Get(Guid runId) => run;
        public AdminWithdrawalRun Update(AdminWithdrawalRun value, long expectedVersion) => value;
        public Guid? FindProviderEvent(string eventId, string eventHash) => run.Id;
        public void RecordProviderEvent(
            string eventId,
            string eventHash,
            AdminWithdrawalRun value,
            long expectedVersion) { }
    }

    private sealed class LegacyAudit : IAdminWithdrawalAuditTrail
    {
        private AdminWithdrawalAuditEvent? _event;

        public AdminWithdrawalAuditEvent Append(
            Guid runId,
            string kind,
            Guid? actorId,
            string evidence,
            DateTimeOffset occurredAt) => _event = new AdminWithdrawalAuditEvent(
            runId, 1, kind, actorId, evidence, occurredAt, "previous", "hash");

        public IReadOnlyList<AdminWithdrawalAuditEvent> Events(Guid runId) =>
            _event is null ? [] : [_event];

        public bool Verify(Guid runId) => true;
    }
}
