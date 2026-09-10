using System.Data;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Risk;

public sealed record ComplianceHoldScope(
    Guid TenantId,
    string SubjectHash,
    EconomyValueMovementCapability? Capability)
{
    public string Key => $"{TenantId:N}:{SubjectHash}:{(Capability is null ? "all" : ((int)Capability.Value).ToString())}";
}

public sealed record ComplianceHoldActivation(
    Guid Id,
    ComplianceHoldScope Scope,
    string CaseReferenceHash,
    string ReasonCode,
    string EvidenceHash,
    string IdempotencyKey,
    Guid ActorId,
    DateTimeOffset ActivatedAt,
    DateTimeOffset ExpiresAt);

public sealed record ComplianceHold(
    Guid Id,
    ComplianceHoldScope Scope,
    string CaseReferenceHash,
    string ReasonCode,
    string EvidenceHash,
    Guid ActivatedBy,
    DateTimeOffset ActivatedAt,
    DateTimeOffset ExpiresAt,
    Guid? ReleasedBy,
    DateTimeOffset? ReleasedAt)
{
    public bool IsActive(DateTimeOffset now) => ReleasedAt is null && ActivatedAt <= now && ExpiresAt > now;
}

public interface IComplianceHoldStore
{
    ValueTask<ComplianceHold> ActivateAsync(
        ComplianceHoldActivation activation,
        CancellationToken cancellationToken);

    ValueTask<ComplianceHold> ReleaseAsync(
        Guid holdId,
        Guid actorId,
        string evidenceHash,
        DateTimeOffset releasedAt,
        CancellationToken cancellationToken);

    ValueTask<bool> IsActiveAsync(
        ComplianceHoldScope scope,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public sealed class PostgreSqlComplianceHoldStore : IComplianceHoldStore
{
    private readonly DbContext _db;

    public PostgreSqlComplianceHoldStore(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext ?? throw new InvalidOperationException(
            "Persistent compliance holds require the application's relational DbContext.");
    }

    public async ValueTask<ComplianceHold> ActivateAsync(
        ComplianceHoldActivation activation,
        CancellationToken cancellationToken)
    {
        ValidateActivation(activation);
        var idempotencyKeyHash = Hash(activation.IdempotencyKey.Trim());
        var requestHash = Hash(string.Join('|', activation.Scope.Key,
            activation.CaseReferenceHash.Trim(), activation.ReasonCode.Trim(), activation.EvidenceHash.Trim(),
            activation.ActorId.ToString("N"), activation.ActivatedAt.UtcTicks, activation.ExpiresAt.UtcTicks));
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var replay = await _db.Set<EconomyComplianceHoldRow>()
            .SingleOrDefaultAsync(row => row.IdempotencyKeyHash == idempotencyKeyHash, cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay.RequestHash, requestHash, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "The compliance hold idempotency key was replayed with different inputs.");
            return Map(replay);
        }
        if (await _db.Set<EconomyComplianceHoldRow>().AnyAsync(
                row => row.ScopeKey == activation.Scope.Key && row.ReleasedAt == null &&
                       row.ActivatedAt <= activation.ActivatedAt && row.ExpiresAt > activation.ActivatedAt,
                cancellationToken))
            throw new InvalidOperationException("An active compliance hold already exists for this scope.");

        var row = new EconomyComplianceHoldRow
        {
            Id = activation.Id, ScopeKey = activation.Scope.Key, TenantId = activation.Scope.TenantId,
            SubjectHash = activation.Scope.SubjectHash.Trim(), Capability = activation.Scope.Capability,
            CaseReferenceHash = activation.CaseReferenceHash.Trim(), ReasonCode = activation.ReasonCode.Trim(),
            EvidenceHash = activation.EvidenceHash.Trim(), IdempotencyKeyHash = idempotencyKeyHash,
            RequestHash = requestHash,
            ActivatedBy = activation.ActorId, ActivatedAt = activation.ActivatedAt, ExpiresAt = activation.ExpiresAt
        };
        _db.Set<EconomyComplianceHoldRow>().Add(row);
        _db.Set<EconomyComplianceHoldEventRow>().Add(new EconomyComplianceHoldEventRow
        {
            Id = Guid.NewGuid(), HoldId = row.Id, Sequence = 1, Kind = "Activated",
            ActorId = activation.ActorId, EvidenceHash = activation.EvidenceHash.Trim(),
            OccurredAt = activation.ActivatedAt
        });
        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
        }, cancellationToken);
    }

    public async ValueTask<ComplianceHold> ReleaseAsync(
        Guid holdId,
        Guid actorId,
        string evidenceHash,
        DateTimeOffset releasedAt,
        CancellationToken cancellationToken)
    {
        if (holdId == Guid.Empty) throw new ArgumentException("Hold ID cannot be empty.", nameof(holdId));
        if (actorId == Guid.Empty) throw new ArgumentException("Actor ID cannot be empty.", nameof(actorId));
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceHash);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var row = await _db.Set<EconomyComplianceHoldRow>()
            .SingleOrDefaultAsync(item => item.Id == holdId, cancellationToken)
            ?? throw new KeyNotFoundException("Compliance hold was not found.");
        if (row.ReleasedAt is not null)
        {
            var releaseEvent = await _db.Set<EconomyComplianceHoldEventRow>().AsNoTracking()
                .SingleAsync(item => item.HoldId == holdId && item.Kind == "Released", cancellationToken);
            if (row.ReleasedBy != actorId || releaseEvent.EvidenceHash != evidenceHash.Trim())
                throw new InvalidOperationException("Compliance hold release was replayed with different inputs.");
            return Map(row);
        }
        if (releasedAt < row.ActivatedAt)
            throw new ArgumentException("Hold release cannot predate activation.", nameof(releasedAt));
        var sequence = await _db.Set<EconomyComplianceHoldEventRow>()
            .Where(item => item.HoldId == holdId)
            .MaxAsync(item => item.Sequence, cancellationToken) + 1;
        row.ReleasedBy = actorId;
        row.ReleasedAt = releasedAt;
        _db.Set<EconomyComplianceHoldEventRow>().Add(new EconomyComplianceHoldEventRow
        {
            Id = Guid.NewGuid(), HoldId = row.Id, Sequence = sequence, Kind = "Released",
            ActorId = actorId, EvidenceHash = evidenceHash.Trim(), OccurredAt = releasedAt
        });
        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
        }, cancellationToken);
    }

    public async ValueTask<bool> IsActiveAsync(
        ComplianceHoldScope scope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ValidateScope(scope);
        return await _db.Set<EconomyComplianceHoldRow>().AsNoTracking().AnyAsync(
            row => row.TenantId == scope.TenantId && row.SubjectHash == scope.SubjectHash &&
                   (row.Capability == null || row.Capability == scope.Capability) &&
                   row.ReleasedAt == null && row.ActivatedAt <= now && row.ExpiresAt > now,
            cancellationToken);
    }

    private static void ValidateActivation(ComplianceHoldActivation activation)
    {
        ArgumentNullException.ThrowIfNull(activation);
        if (activation.Id == Guid.Empty) throw new ArgumentException("Hold ID cannot be empty.", nameof(activation));
        ValidateScope(activation.Scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(activation.CaseReferenceHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(activation.ReasonCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(activation.EvidenceHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(activation.IdempotencyKey);
        if (activation.ActorId == Guid.Empty) throw new ArgumentException("Actor ID cannot be empty.", nameof(activation));
        if (activation.ExpiresAt <= activation.ActivatedAt)
            throw new ArgumentException("Hold expiry must follow activation.", nameof(activation));
    }

    private static void ValidateScope(ComplianceHoldScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.TenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(scope));
        ArgumentException.ThrowIfNullOrWhiteSpace(scope.SubjectHash);
        if (scope.Capability is not null && !Enum.IsDefined(scope.Capability.Value))
            throw new ArgumentOutOfRangeException(nameof(scope));
    }

    private static ComplianceHold Map(EconomyComplianceHoldRow row) => new(
        row.Id, new ComplianceHoldScope(row.TenantId, row.SubjectHash, row.Capability),
        row.CaseReferenceHash, row.ReasonCode, row.EvidenceHash, row.ActivatedBy,
        row.ActivatedAt, row.ExpiresAt, row.ReleasedBy, row.ReleasedAt);

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
