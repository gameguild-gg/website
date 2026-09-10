using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Treasury;

internal sealed class AdminWithdrawalRunRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public Guid RequestedBy { get; set; }
    public Guid? ApprovedBy { get; set; }
    public Guid PlatformFeeWalletId { get; set; }
    public long AmountUnits { get; set; }
    public string SourceAssetKey { get; set; } = string.Empty;
    public string DestinationHash { get; set; } = string.Empty;
    public AdminWithdrawalRunState State { get; set; }
    public long Version { get; set; }
    public long FencingToken { get; set; }
    public long ExecutionEpoch { get; set; }
    public long ReserveVersion { get; set; }
    public long ReserveAuthorizationEpoch { get; set; }
    public long PolicyVersion { get; set; }
    public string? DispatchSnapshotHash { get; set; }
    public string? ProviderTransferId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class AdminWithdrawalProviderEventRow
{
    public Guid TenantId { get; set; }
    public string EventId { get; set; } = string.Empty;
    public string EventHash { get; set; } = string.Empty;
    public Guid RunId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

internal sealed class AdminWithdrawalAuditEventRow
{
    public Guid TenantId { get; set; }
    public Guid RunId { get; set; }
    public long Sequence { get; set; }
    public string Kind { get; set; } = string.Empty;
    public Guid? ActorId { get; set; }
    public string Evidence { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public string PreviousHash { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
}

public sealed class AdminWithdrawalDispatchOutboxRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RunId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset AvailableAt { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public string? LeaseOwner { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? LastErrorCode { get; set; }
}

public sealed class TreasuryModelConfiguration : IModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<AdminWithdrawalRunRow>(builder =>
        {
            builder.ToTable("economy_admin_withdrawal_runs", table =>
            {
                table.HasCheckConstraint("ck_economy_admin_withdrawal_runs_state", "\"State\" BETWEEN 1 AND 7");
                table.HasCheckConstraint(
                    "ck_economy_admin_withdrawal_runs_positive_versions",
                    "\"Version\" > 0 AND \"FencingToken\" > 0 AND \"ExecutionEpoch\" > 0 AND \"ReserveVersion\" > 0 AND \"ReserveAuthorizationEpoch\" > 0 AND \"PolicyVersion\" > 0");
                table.HasCheckConstraint("ck_economy_admin_withdrawal_runs_amount_positive", "\"AmountUnits\" > 0");
                table.HasCheckConstraint(
                    "ck_economy_admin_withdrawal_runs_approval",
                    "(\"State\" = 1 AND \"ApprovedBy\" IS NULL) OR (\"State\" BETWEEN 2 AND 7 AND \"ApprovedBy\" IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_economy_admin_withdrawal_runs_dispatch_snapshot",
                    "(\"State\" IN (1, 2) AND \"DispatchSnapshotHash\" IS NULL) OR (\"State\" BETWEEN 3 AND 7 AND \"DispatchSnapshotHash\" IS NOT NULL)");
                table.HasCheckConstraint("ck_economy_admin_withdrawal_runs_timestamps", "\"UpdatedAt\" >= \"CreatedAt\"");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Id).ValueGeneratedNever();
            builder.Property(row => row.IdempotencyKey).HasMaxLength(256);
            builder.Property(row => row.RequestHash).HasMaxLength(128);
            builder.Property(row => row.SourceAssetKey).HasMaxLength(256);
            builder.Property(row => row.DestinationHash).HasMaxLength(128);
            builder.Property(row => row.DispatchSnapshotHash).HasMaxLength(128);
            builder.Property(row => row.ProviderTransferId).HasMaxLength(256);
            builder.Property(row => row.Version).IsConcurrencyToken();
            builder.HasIndex(row => new { row.TenantId, row.IdempotencyKey }).IsUnique()
                .HasDatabaseName("ux_economy_admin_withdrawal_runs_idempotency");
            builder.HasIndex(row => new { row.TenantId, row.PeriodStart }).IsUnique()
                .HasFilter("\"State\" NOT IN (6, 7)")
                .HasDatabaseName("ux_economy_admin_withdrawal_runs_active_period");
            builder.HasIndex(row => new { row.State, row.UpdatedAt })
                .HasDatabaseName("ix_economy_admin_withdrawal_runs_state_updated");
        });

        modelBuilder.Entity<AdminWithdrawalProviderEventRow>(builder =>
        {
            builder.ToTable("economy_admin_withdrawal_provider_events");
            builder.HasKey(row => new { row.TenantId, row.EventId });
            builder.Property(row => row.EventId).HasMaxLength(256);
            builder.Property(row => row.EventHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.RunId, row.RecordedAt })
                .HasDatabaseName("ix_economy_admin_withdrawal_provider_events_run_recorded");
            builder.HasOne<AdminWithdrawalRunRow>()
                .WithMany()
                .HasForeignKey(row => row.RunId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdminWithdrawalAuditEventRow>(builder =>
        {
            builder.ToTable("economy_admin_withdrawal_audit_events", table =>
                table.HasCheckConstraint(
                    "ck_economy_admin_withdrawal_audit_events_sequence",
                    "\"Sequence\" > 0"));
            builder.HasKey(row => new { row.RunId, row.Sequence });
            builder.Property(row => row.Kind).HasMaxLength(100);
            builder.Property(row => row.Evidence).HasColumnType("text");
            builder.Property(row => row.PreviousHash).HasMaxLength(128);
            builder.Property(row => row.Hash).HasMaxLength(128);
            builder.HasIndex(row => row.Hash).IsUnique()
                .HasDatabaseName("ux_economy_admin_withdrawal_audit_events_hash");
            builder.HasOne<AdminWithdrawalRunRow>()
                .WithMany()
                .HasForeignKey(row => row.RunId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdminWithdrawalDispatchOutboxRow>(builder =>
        {
            builder.ToTable("economy_admin_withdrawal_dispatch_outbox", table =>
                table.HasCheckConstraint("ck_economy_admin_withdrawal_dispatch_outbox_attempts", "\"AttemptCount\" >= 0"));
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Id).ValueGeneratedNever();
            builder.Property(row => row.IdempotencyKey).HasMaxLength(256);
            builder.Property(row => row.Payload).HasColumnType("jsonb");
            builder.Property(row => row.PayloadHash).HasMaxLength(128);
            builder.Property(row => row.LeaseOwner).HasMaxLength(200);
            builder.Property(row => row.LastErrorCode).HasMaxLength(100);
            builder.HasIndex(row => row.RunId).IsUnique();
            builder.HasIndex(row => new { row.TenantId, row.RunId }).IsUnique();
            builder.HasIndex(row => new { row.CompletedAt, row.AvailableAt, row.LeaseExpiresAt });
            builder.HasOne<AdminWithdrawalRunRow>().WithMany().HasForeignKey(row => row.RunId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}


public sealed class PostgreSqlAdminWithdrawalStore : IAdminWithdrawalStore
{
    private readonly DbContext _db;

    public PostgreSqlAdminWithdrawalStore(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "PostgreSQL admin withdrawal persistence requires the application's relational DbContext.");
    }

    public AdminWithdrawalRun? FindReplay(string key, string requestHash)
        => FindReplayCore(null, key, requestHash);

    public AdminWithdrawalRun? FindReplay(Guid tenantId, string key, string requestHash)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        return FindReplayCore(tenantId, key, requestHash);
    }

    private AdminWithdrawalRun? FindReplayCore(Guid? tenantId, string key, string requestHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        var query = _db.Set<AdminWithdrawalRunRow>().AsNoTracking()
            .Where(row => row.IdempotencyKey == key.Trim());
        if (tenantId.HasValue) query = query.Where(row => row.TenantId == tenantId.Value);
        var row = query.SingleOrDefault();
        if (row is null) return null;
        if (!string.Equals(row.RequestHash, requestHash, StringComparison.Ordinal))
            throw new AdminWithdrawalStaleCommandException(
                "The withdrawal idempotency key is bound to a different request.");
        return ToContract(row);
    }

    public AdminWithdrawalRun? FindPeriod(DateOnly periodStart)
        => FindPeriodCore(null, periodStart);

    public AdminWithdrawalRun? FindPeriod(Guid tenantId, DateOnly periodStart)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        return FindPeriodCore(tenantId, periodStart);
    }

    private AdminWithdrawalRun? FindPeriodCore(Guid? tenantId, DateOnly periodStart)
    {
        var query = _db.Set<AdminWithdrawalRunRow>().AsNoTracking()
            .Where(row => row.PeriodStart == periodStart &&
                          row.State != AdminWithdrawalRunState.Failed &&
                          row.State != AdminWithdrawalRunState.Cancelled);
        if (tenantId.HasValue) query = query.Where(row => row.TenantId == tenantId.Value);
        var row = query.SingleOrDefault();
        return row is null ? null : ToContract(row);
    }

    public void Add(AdminWithdrawalRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        Execute($"""
            SELECT economy_private.create_admin_withdrawal_run_v2(
                {run.Id},
                {run.TenantId},
                {run.IdempotencyKey.Value},
                {run.RequestHash},
                {run.PeriodStart},
                {run.RequestedBy},
                {run.PlatformFeeWalletId.Value},
                {run.Amount.Units},
                {run.SourceAssetKey},
                {run.DestinationHash},
                {(int)run.State},
                {run.Version},
                {run.FencingToken},
                {run.ExecutionEpoch},
                {run.ReserveVersion.Value},
                {run.ReserveAuthorizationEpoch},
                {run.PolicyVersion.Value},
                {run.DispatchSnapshotHash},
                {run.ProviderTransferId},
                {run.CreatedAt},
                {run.UpdatedAt});
            """);
    }

    public AdminWithdrawalRun Get(Guid runId)
        => GetCore(null, runId);

    public AdminWithdrawalRun Get(Guid tenantId, Guid runId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        return GetCore(tenantId, runId);
    }

    private AdminWithdrawalRun GetCore(Guid? tenantId, Guid runId)
    {
        if (runId == Guid.Empty) throw new ArgumentException("Run ID is required.", nameof(runId));
        var query = _db.Set<AdminWithdrawalRunRow>().AsNoTracking().Where(row => row.Id == runId);
        if (tenantId.HasValue) query = query.Where(row => row.TenantId == tenantId.Value);
        var row = query.SingleOrDefault();
        return row is null
            ? throw new KeyNotFoundException("Admin withdrawal run was not found.")
            : ToContract(row);
    }

    public AdminWithdrawalRun Update(AdminWithdrawalRun run, long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(run);

        Execute($"""
            SELECT economy_private.transition_admin_withdrawal_run_v2(
                {run.TenantId},
                {run.Id},
                {expectedVersion},
                {(int)run.State},
                {run.ApprovedBy},
                {run.DispatchSnapshotHash},
                {run.ProviderTransferId},
                {run.UpdatedAt});
            """);
        return run;
    }

    public Guid? FindProviderEvent(string eventId, string eventHash)
        => FindProviderEventCore(null, eventId, eventHash);

    public Guid? FindProviderEvent(Guid tenantId, string eventId, string eventHash)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        return FindProviderEventCore(tenantId, eventId, eventHash);
    }

    public IReadOnlyList<AdminWithdrawalRun> List(Guid tenantId, int limit = 100)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (limit is <= 0 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        return _db.Set<AdminWithdrawalRunRow>().AsNoTracking()
            .Where(row => row.TenantId == tenantId)
            .OrderByDescending(row => row.CreatedAt).ThenBy(row => row.Id)
            .Take(limit)
            .AsEnumerable()
            .Select(ToContract)
            .ToArray();
    }

    private Guid? FindProviderEventCore(Guid? tenantId, string eventId, string eventHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventHash);
        var query = _db.Set<AdminWithdrawalProviderEventRow>().AsNoTracking()
            .Where(row => row.EventId == eventId.Trim());
        if (tenantId.HasValue) query = query.Where(row => row.TenantId == tenantId.Value);
        var row = query.SingleOrDefault();
        if (row is null) return null;
        if (!string.Equals(row.EventHash, eventHash, StringComparison.Ordinal))
            throw new AdminWithdrawalEvidenceException(
                "The provider event ID is bound to different evidence.");
        if (row.RecordedAt == default)
            throw new AdminWithdrawalEvidenceException(
                "The provider event has an invalid recorded timestamp.");
        return row.RunId;
    }

    public void RecordProviderEvent(string eventId, string eventHash, AdminWithdrawalRun run, long expectedVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventHash);
        ArgumentNullException.ThrowIfNull(run);

        Execute($"""
            SELECT economy_private.complete_admin_withdrawal_provider_event_v2(
                {run.TenantId},
                {eventId.Trim()},
                {eventHash},
                {run.Id},
                {expectedVersion},
                {(int)run.State},
                {run.ProviderTransferId},
                {run.UpdatedAt});
            """);
    }

    public void RecordProviderEvent(
        Guid tenantId,
        string eventId,
        string eventHash,
        AdminWithdrawalRun run,
        long expectedVersion)
    {
        if (tenantId == Guid.Empty || run.TenantId != tenantId)
            throw new ArgumentException("The run must belong to the actor tenant.", nameof(tenantId));
        RecordProviderEvent(eventId, eventHash, run, expectedVersion);
    }

    private void Execute(FormattableString sql)
    {
        try
        {
            _db.Database.ExecuteSqlInterpolated(sql);
        }
        catch (Exception exception) when (
            exception.Message.Contains("admin withdrawal", StringComparison.OrdinalIgnoreCase))
        {
            throw Translate(exception);
        }
    }

    private static Exception Translate(Exception exception)
    {
        var message = exception.Message;
        if (message.Contains("overlap", StringComparison.OrdinalIgnoreCase))
            return new AdminWithdrawalOverlapException(message);
        if (message.Contains("evidence", StringComparison.OrdinalIgnoreCase))
            return new AdminWithdrawalEvidenceException(message);
        return new AdminWithdrawalStaleCommandException(message);
    }

    private static AdminWithdrawalRun ToContract(AdminWithdrawalRunRow row) => new(
        row.Id,
        row.TenantId,
        new IdempotencyKey(row.IdempotencyKey),
        row.RequestHash,
        row.PeriodStart,
        row.RequestedBy,
        row.ApprovedBy,
        new WalletId(row.PlatformFeeWalletId),
        new CoinAmount(CurrencyCode.HardCoin, row.AmountUnits),
        row.SourceAssetKey,
        row.DestinationHash,
        row.State,
        row.Version,
        row.FencingToken,
        row.ExecutionEpoch,
        new ReserveVersion(row.ReserveVersion),
        row.ReserveAuthorizationEpoch,
        new PolicyVersion(row.PolicyVersion),
        row.DispatchSnapshotHash,
        row.ProviderTransferId,
        row.CreatedAt,
        row.UpdatedAt);
}


public sealed class PostgreSqlAdminWithdrawalAuditTrail : IAdminWithdrawalAuditTrail
{
    private readonly DbContext _db;

    public PostgreSqlAdminWithdrawalAuditTrail(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "PostgreSQL admin withdrawal persistence requires the application's relational DbContext.");
    }

    public AdminWithdrawalAuditEvent Append(
        Guid runId,
        string kind,
        Guid? actorId,
        string evidence,
        DateTimeOffset occurredAt)
    {
        if (runId == Guid.Empty) throw new ArgumentException("Run ID is required.", nameof(runId));
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        var tenantId = _db.Set<AdminWithdrawalRunRow>().AsNoTracking()
            .Where(row => row.Id == runId)
            .Select(row => row.TenantId)
            .SingleOrDefault();
        if (tenantId == Guid.Empty)
            throw new KeyNotFoundException("Admin withdrawal run was not found.");
        return Append(tenantId, runId, kind, actorId, evidence, occurredAt);
    }

    public AdminWithdrawalAuditEvent Append(
        Guid tenantId,
        Guid runId,
        string kind,
        Guid? actorId,
        string evidence,
        DateTimeOffset occurredAt)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (runId == Guid.Empty) throw new ArgumentException("Run ID is required.", nameof(runId));
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);

        var item = _db.Database.SqlQuery<AdminWithdrawalAuditEventProjection>($"""
            SELECT "RunId", "Sequence", "Kind", "ActorId", "Evidence", "OccurredAt", "PreviousHash", "Hash"
            FROM economy_private.append_admin_withdrawal_audit_event_v2(
                {tenantId}, {runId}, {kind.Trim()}, {actorId}, {evidence.Trim()}, {occurredAt})
            """).Single();

        return item.ToContract();
    }

    public IReadOnlyList<AdminWithdrawalAuditEvent> Events(Guid runId)
    {
        if (runId == Guid.Empty) return [];
        var tenantId = _db.Set<AdminWithdrawalRunRow>().AsNoTracking()
            .Where(row => row.Id == runId)
            .Select(row => row.TenantId)
            .SingleOrDefault();
        return tenantId == Guid.Empty ? [] : Events(tenantId, runId);
    }

    public IReadOnlyList<AdminWithdrawalAuditEvent> Events(Guid tenantId, Guid runId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        return _db.Set<AdminWithdrawalAuditEventRow>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.RunId == runId)
            .AsEnumerable()
            .OrderBy(item => item.Sequence)
            .Select(item => new AdminWithdrawalAuditEvent(
                item.RunId,
                item.Sequence,
                item.Kind,
                item.ActorId,
                item.Evidence,
                item.OccurredAt,
                item.PreviousHash,
                item.Hash))
            .ToArray();
    }


    public bool Verify(Guid runId)
    {
        var events = Events(runId);
        return VerifyEvents(runId, events);
    }

    public bool Verify(Guid tenantId, Guid runId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        var events = Events(tenantId, runId);
        return VerifyEvents(runId, events);
    }

    private static bool VerifyEvents(Guid runId, IReadOnlyList<AdminWithdrawalAuditEvent> events)
    {
        if (events.Count == 0) return false;
        var previousHash = new string('0', 64);
        for (var index = 0; index < events.Count; index++)
        {
            var item = events[index];
            var expected = Hash(
                item.RunId, index + 1L, item.Kind, item.ActorId,
                item.Evidence, item.OccurredAt, previousHash);
            if (item.RunId != runId || item.Sequence != index + 1L ||
                !string.Equals(item.PreviousHash, previousHash, StringComparison.Ordinal) ||
                !string.Equals(item.Hash, expected, StringComparison.Ordinal))
                return false;
            previousHash = item.Hash;
        }
        return true;
    }

    private static string Hash(
        Guid runId,
        long sequence,
        string kind,
        Guid? actorId,
        string evidence,
        DateTimeOffset occurredAt,
        string previousHash)
    {
        var canonical = string.Join('|',
            runId.ToString("N"),
            sequence.ToString(CultureInfo.InvariantCulture),
            kind,
            actorId?.ToString("N") ?? string.Empty,
            evidence,
            occurredAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture),
            previousHash);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed class AdminWithdrawalAuditEventProjection
    {
        public Guid RunId { get; init; }
        public long Sequence { get; init; }
        public string Kind { get; init; } = string.Empty;
        public Guid? ActorId { get; init; }
        public string Evidence { get; init; } = string.Empty;
        public DateTimeOffset OccurredAt { get; init; }
        public string PreviousHash { get; init; } = string.Empty;
        public string Hash { get; init; } = string.Empty;

        public AdminWithdrawalAuditEvent ToContract() => new(
            RunId, Sequence, Kind, ActorId, Evidence, OccurredAt, PreviousHash, Hash);
    }
}
