using System.Globalization;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Operations;

public sealed record EconomyOperationalPage<T>(
    IReadOnlyList<T> Items,
    string? NextCursor);

public sealed record EconomyPolicyOperationalDetails(
    EconomyCapabilityPolicyOperationalStatus Summary,
    string CanonicalPayload,
    Guid ProposedBy,
    Guid? ApprovedBy,
    DateTimeOffset ProposedAt,
    DateTimeOffset? ApprovedAt);

public sealed record EconomyPolicyAuditEntry(
    string Kind,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string EvidenceHash);

public interface IEconomyPolicyQueryReader
{
    ValueTask<EconomyOperationalPage<EconomyCapabilityPolicyOperationalStatus>> ListAsync(
        Guid tenantId,
        EconomyValueMovementCapability? capability,
        int limit,
        string? cursor,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    ValueTask<EconomyPolicyOperationalDetails?> FindAsync(
        Guid tenantId,
        Guid policyId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<EconomyPolicyAuditEntry>> ReadAuditAsync(
        Guid tenantId,
        Guid policyId,
        CancellationToken cancellationToken);
}

public sealed class PostgreSqlEconomyPolicyQueryReader : IEconomyPolicyQueryReader
{
    private readonly DbContext _db;

    public PostgreSqlEconomyPolicyQueryReader(IApplicationDbContext context) =>
        _db = PostgreSqlEntityRiskGraphStore.RequireRelationalContext(context);

    public async ValueTask<EconomyOperationalPage<EconomyCapabilityPolicyOperationalStatus>> ListAsync(
        Guid tenantId,
        EconomyValueMovementCapability? capability,
        int limit,
        string? cursor,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ValidateTenantAndLimit(tenantId, limit);
        if (capability is not null && !Enum.IsDefined(capability.Value))
            throw new ArgumentOutOfRangeException(nameof(capability));
        var position = DecodeCursor(cursor);
        var query = _db.Set<EconomyCapabilityPolicyRow>().AsNoTracking()
            .Where(row => row.TenantId == null || row.TenantId == tenantId);
        if (capability is not null) query = query.Where(row => row.Capability == capability.Value);
        if (position is not null)
        {
            var proposedAt = position.Value.ProposedAt;
            var id = position.Value.Id;
            query = query.Where(row => row.ProposedAt < proposedAt ||
                                       row.ProposedAt == proposedAt && row.Id.CompareTo(id) > 0);
        }

        var rows = await query.OrderByDescending(row => row.ProposedAt).ThenBy(row => row.Id)
            .Take(limit + 1).ToArrayAsync(cancellationToken);
        var pageRows = rows.Take(limit).ToArray();
        var items = pageRows.Select(row => MapSummary(row, now)).ToArray();
        var nextCursor = rows.Length > limit && pageRows.Length > 0
            ? EncodeCursor(pageRows[^1].ProposedAt, pageRows[^1].Id)
            : null;
        return new EconomyOperationalPage<EconomyCapabilityPolicyOperationalStatus>(
            Array.AsReadOnly(items), nextCursor);
    }

    public async ValueTask<EconomyPolicyOperationalDetails?> FindAsync(
        Guid tenantId,
        Guid policyId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ValidateTenantAndPolicy(tenantId, policyId);
        var row = await _db.Set<EconomyCapabilityPolicyRow>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == policyId &&
                                          (item.TenantId == null || item.TenantId == tenantId),
                cancellationToken);
        return row is null
            ? null
            : new EconomyPolicyOperationalDetails(
                MapSummary(row, now), row.CanonicalPayload, row.ProposedBy, row.ApprovedBy,
                row.ProposedAt, row.ApprovedAt);
    }

    public async ValueTask<IReadOnlyList<EconomyPolicyAuditEntry>> ReadAuditAsync(
        Guid tenantId,
        Guid policyId,
        CancellationToken cancellationToken)
    {
        ValidateTenantAndPolicy(tenantId, policyId);
        var policy = await _db.Set<EconomyCapabilityPolicyRow>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == policyId &&
                                          (item.TenantId == null || item.TenantId == tenantId),
                cancellationToken);
        if (policy is null) return Array.Empty<EconomyPolicyAuditEntry>();
        var approvals = await _db.Set<EconomyCapabilityPolicyApprovalRow>().AsNoTracking()
            .Where(item => item.PolicyId == policyId)
            .OrderBy(item => item.ApprovedAt)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
        var audit = new List<EconomyPolicyAuditEntry>(approvals.Length + 1)
        {
            new("Proposed", policy.ProposedBy, policy.ProposedAt, policy.RequestHash)
        };
        audit.AddRange(approvals.Select(item => new EconomyPolicyAuditEntry(
            "Approved", item.ActorId, item.ApprovedAt, item.ReauthenticationHash)));
        return audit.AsReadOnly();
    }

    internal static string EncodeCursor(DateTimeOffset proposedAt, Guid id) =>
        $"{proposedAt.UtcTicks:X16}{id:N}";

    internal static (DateTimeOffset ProposedAt, Guid Id)? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        if (cursor.Length != 48 ||
            !long.TryParse(cursor.AsSpan(0, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                out var ticks) ||
            !Guid.TryParseExact(cursor[16..], "N", out var id) ||
            ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
            throw new ArgumentException("Economy policy cursor is invalid.", nameof(cursor));
        return (new DateTimeOffset(ticks, TimeSpan.Zero), id);
    }

    private static EconomyCapabilityPolicyOperationalStatus MapSummary(
        EconomyCapabilityPolicyRow row,
        DateTimeOffset now) => new(
        row.Id,
        row.TenantId,
        row.Capability,
        row.JurisdictionCode,
        row.Version,
        row.PayloadHash,
        row.KeyId,
        row.ProviderReady,
        row.IsActive
            ? row.ExpiresAt <= now
                ? EconomyCapabilityPolicyState.Expired
                : EconomyCapabilityPolicyState.Active
            : row.ApprovedBy is null
                ? EconomyCapabilityPolicyState.PendingApproval
                : EconomyCapabilityPolicyState.Approved,
        row.EffectiveAt,
        row.ExpiresAt);

    private static void ValidateTenantAndLimit(Guid tenantId, int limit)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
    }

    private static void ValidateTenantAndPolicy(Guid tenantId, Guid policyId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        if (policyId == Guid.Empty) throw new ArgumentException("Policy ID cannot be empty.", nameof(policyId));
    }
}
