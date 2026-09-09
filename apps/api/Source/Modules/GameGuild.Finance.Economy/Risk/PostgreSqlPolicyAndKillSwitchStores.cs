using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Risk;

public static class EconomyCanonicalJson
{
    public static string Serialize(JsonElement value)
    {
        var builder = new StringBuilder();
        Append(builder, value);
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                var properties = value.EnumerateObject().ToArray();
                if (properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
                    throw new ArgumentException("Canonical JSON cannot contain duplicate property names.", nameof(value));
                var firstProperty = true;
                foreach (var property in properties.OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    if (!firstProperty) builder.Append(',');
                    firstProperty = false;
                    builder.Append(JsonSerializer.Serialize(property.Name)).Append(':');
                    Append(builder, property.Value);
                }
                builder.Append('}');
                break;
            case JsonValueKind.Array:
                builder.Append('[');
                var firstItem = true;
                foreach (var item in value.EnumerateArray())
                {
                    if (!firstItem) builder.Append(',');
                    firstItem = false;
                    Append(builder, item);
                }
                builder.Append(']');
                break;
            case JsonValueKind.String:
                builder.Append(JsonSerializer.Serialize(value.GetString()));
                break;
            case JsonValueKind.Number:
                if (value.TryGetInt64(out var integer))
                    builder.Append(integer.ToString(CultureInfo.InvariantCulture));
                else if (value.TryGetDecimal(out var decimalValue))
                    builder.Append(decimalValue.ToString("G29", CultureInfo.InvariantCulture));
                else
                    builder.Append(value.GetDouble().ToString("R", CultureInfo.InvariantCulture));
                break;
            case JsonValueKind.True:
                builder.Append("true");
                break;
            case JsonValueKind.False:
                builder.Append("false");
                break;
            case JsonValueKind.Null:
                builder.Append("null");
                break;
            default:
                throw new ArgumentException("Canonical JSON requires a complete JSON value.", nameof(value));
        }
    }
}

public enum EconomyCapabilityPolicyState
{
    PendingApproval = 1,
    Approved = 2,
    Active = 3,
    Expired = 4
}

public sealed record EconomyCapabilityPolicyProposal(
    Guid Id,
    Guid? TenantId,
    EconomyValueMovementCapability Capability,
    string JurisdictionCode,
    long Version,
    JsonElement Payload,
    Guid ProposedBy,
    DateTimeOffset ProposedAt,
    DateTimeOffset EffectiveAt,
    DateTimeOffset ExpiresAt,
    bool ProviderReady);

public sealed record EconomyCapabilityPolicy(
    Guid Id,
    string ScopeKey,
    Guid? TenantId,
    EconomyValueMovementCapability Capability,
    string JurisdictionCode,
    long Version,
    string CanonicalPayload,
    string PayloadHash,
    string KeyId,
    string Signature,
    Guid ProposedBy,
    Guid? ApprovedBy,
    DateTimeOffset ProposedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset EffectiveAt,
    DateTimeOffset ExpiresAt,
    bool ProviderReady,
    EconomyCapabilityPolicyState State);

public interface ICapabilityPolicySigner
{
    ValueTask<CapabilityReceiptSignature> SignAsync(string canonicalPayload, CancellationToken cancellationToken);
}

public interface IEconomyCapabilityPolicyStore
{
    ValueTask<EconomyCapabilityPolicy> ProposeAsync(
        EconomyCapabilityPolicyProposal proposal,
        CancellationToken cancellationToken);

    ValueTask<EconomyCapabilityPolicy> ApproveAsync(
        Guid policyId,
        Guid actorId,
        string reauthenticationHash,
        DateTimeOffset approvedAt,
        CancellationToken cancellationToken);

    ValueTask<int> ActivateDueAsync(DateTimeOffset now, CancellationToken cancellationToken);

    ValueTask<EconomyCapabilityPolicy?> CurrentAsync(
        Guid? tenantId,
        EconomyValueMovementCapability capability,
        string jurisdictionCode,
        CancellationToken cancellationToken);
}

public sealed class PostgreSqlEconomyCapabilityPolicyStore : IEconomyCapabilityPolicyStore
{
    private readonly DbContext _db;
    private readonly ICapabilityPolicySigner _signer;
    private readonly TimeProvider _timeProvider;

    public PostgreSqlEconomyCapabilityPolicyStore(
        IApplicationDbContext context,
        ICapabilityPolicySigner signer,
        TimeProvider timeProvider)
    {
        _db = PostgreSqlEntityRiskGraphStore.RequireRelationalContext(context);
        ArgumentNullException.ThrowIfNull(signer);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _signer = signer;
        _timeProvider = timeProvider;
    }

    public async ValueTask<EconomyCapabilityPolicy> ProposeAsync(
        EconomyCapabilityPolicyProposal proposal,
        CancellationToken cancellationToken)
    {
        ValidateProposal(proposal);
        var jurisdiction = proposal.JurisdictionCode.Trim().ToUpperInvariant();
        var scopeKey = ScopeKey(proposal.TenantId, proposal.Capability, jurisdiction);
        var canonicalPayload = EconomyCanonicalJson.Serialize(proposal.Payload);
        var payloadHash = Hash(canonicalPayload);
        var requestHash = Hash(string.Join('|', proposal.Id.ToString("N"), scopeKey, proposal.Version,
            payloadHash, proposal.ProposedBy.ToString("N"), proposal.ProposedAt.UtcTicks,
            proposal.EffectiveAt.UtcTicks, proposal.ExpiresAt.UtcTicks, proposal.ProviderReady));

        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async token =>
        {
            var replay = await _db.Set<EconomyCapabilityPolicyRow>()
                .SingleOrDefaultAsync(row => row.Id == proposal.Id, token);
            if (replay is not null)
            {
                if (replay.RequestHash != requestHash)
                    throw new RiskDecisionReuseException("A policy proposal ID cannot be reused with different inputs.");
                return Map(replay, proposal.ProposedAt);
            }

            var existing = await _db.Set<EconomyCapabilityPolicyRow>()
                .Where(row => row.ScopeKey == scopeKey)
                .ToListAsync(token);
            if (existing.Any(row => row.Version >= proposal.Version))
                throw new InvalidOperationException("Capability policy versions must increase monotonically.");
            if (existing.Any(row => row.ExpiresAt > proposal.EffectiveAt && row.EffectiveAt < proposal.ExpiresAt))
                throw new InvalidOperationException("Capability policy effective windows cannot overlap.");

            var row = new EconomyCapabilityPolicyRow
            {
                Id = proposal.Id, ScopeKey = scopeKey, TenantId = proposal.TenantId,
                Capability = proposal.Capability, JurisdictionCode = jurisdiction, Version = proposal.Version,
                CanonicalPayload = canonicalPayload, PayloadHash = payloadHash, RequestHash = requestHash,
                ProposedBy = proposal.ProposedBy, ProposedAt = proposal.ProposedAt,
                EffectiveAt = proposal.EffectiveAt, ExpiresAt = proposal.ExpiresAt,
                ProviderReady = proposal.ProviderReady, IsActive = false
            };
            _db.Set<EconomyCapabilityPolicyRow>().Add(row);
            await _db.SaveChangesAsync(token);
            return Map(row, proposal.ProposedAt);
        }, cancellationToken);
    }

    public async ValueTask<EconomyCapabilityPolicy> ApproveAsync(
        Guid policyId,
        Guid actorId,
        string reauthenticationHash,
        DateTimeOffset approvedAt,
        CancellationToken cancellationToken)
    {
        if (policyId == Guid.Empty) throw new ArgumentException("Policy ID cannot be empty.", nameof(policyId));
        if (actorId == Guid.Empty) throw new ArgumentException("Actor ID cannot be empty.", nameof(actorId));
        ArgumentException.ThrowIfNullOrWhiteSpace(reauthenticationHash);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async token =>
        {
            var row = await _db.Set<EconomyCapabilityPolicyRow>()
                .SingleOrDefaultAsync(item => item.Id == policyId, token)
                ?? throw new KeyNotFoundException("Capability policy proposal was not found.");
            if (row.ProposedBy == actorId)
                throw new InvalidOperationException("The policy proposer cannot approve their own policy.");
            if (row.ApprovedBy is not null)
                throw new InvalidOperationException("Capability policy has already been approved.");
            if (approvedAt < row.ProposedAt || approvedAt > row.EffectiveAt)
                throw new ArgumentException("Policy approval must occur between proposal and effective time.", nameof(approvedAt));

            var signed = await _signer.SignAsync(row.CanonicalPayload, token);
            if (string.IsNullOrWhiteSpace(signed.KeyId) || string.IsNullOrWhiteSpace(signed.Signature))
                throw new InvalidOperationException("The policy signer returned an invalid signature.");
            row.KeyId = signed.KeyId.Trim();
            row.Signature = signed.Signature.Trim();
            row.ApprovedBy = actorId;
            row.ApprovedAt = approvedAt;
            _db.Set<EconomyCapabilityPolicyApprovalRow>().Add(new EconomyCapabilityPolicyApprovalRow
            {
                Id = Guid.NewGuid(), PolicyId = row.Id, ActorId = actorId,
                ReauthenticationHash = reauthenticationHash.Trim(), ApprovedAt = approvedAt
            });
            await _db.SaveChangesAsync(token);
            return Map(row, approvedAt);
        }, cancellationToken);
    }

    public async ValueTask<int> ActivateDueAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async token =>
        {
            var due = await _db.Set<EconomyCapabilityPolicyRow>()
                .Where(row => !row.IsActive && row.ApprovedBy != null && row.EffectiveAt <= now && row.ExpiresAt > now)
                .OrderBy(row => row.ScopeKey).ThenBy(row => row.Version)
                .ToArrayAsync(token);
            foreach (var row in due)
            {
                var active = await _db.Set<EconomyCapabilityPolicyRow>()
                    .Where(item => item.ScopeKey == row.ScopeKey && item.IsActive)
                    .ToArrayAsync(token);
                foreach (var previous in active) previous.IsActive = false;
                row.IsActive = true;
            }
            if (due.Length > 0) await _db.SaveChangesAsync(token);
            return due.Length;
        }, cancellationToken);
    }

    public async ValueTask<EconomyCapabilityPolicy?> CurrentAsync(
        Guid? tenantId,
        EconomyValueMovementCapability capability,
        string jurisdictionCode,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(capability)) throw new ArgumentOutOfRangeException(nameof(capability));
        ArgumentException.ThrowIfNullOrWhiteSpace(jurisdictionCode);
        var jurisdiction = jurisdictionCode.Trim().ToUpperInvariant();
        var scope = ScopeKey(tenantId, capability, jurisdiction);
        var row = await _db.Set<EconomyCapabilityPolicyRow>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.ScopeKey == scope && item.IsActive, cancellationToken);
        return row is null ? null : Map(row, _timeProvider.GetUtcNow());
    }

    private static void ValidateProposal(EconomyCapabilityPolicyProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (proposal.Id == Guid.Empty) throw new ArgumentException("Policy ID cannot be empty.", nameof(proposal));
        if (!Enum.IsDefined(proposal.Capability)) throw new ArgumentOutOfRangeException(nameof(proposal));
        ArgumentException.ThrowIfNullOrWhiteSpace(proposal.JurisdictionCode);
        if (proposal.JurisdictionCode.Trim() is "*" or "ALL")
            throw new ArgumentException("Capability policies require an explicit jurisdiction.", nameof(proposal));
        if (proposal.Version <= 0) throw new ArgumentOutOfRangeException(nameof(proposal));
        if (proposal.ProposedBy == Guid.Empty) throw new ArgumentException("Policy proposer cannot be empty.", nameof(proposal));
        if (proposal.EffectiveAt < proposal.ProposedAt)
            throw new ArgumentException("Policy effective time cannot predate proposal.", nameof(proposal));
        if (proposal.ExpiresAt <= proposal.EffectiveAt)
            throw new ArgumentException("Policy expiry must follow effective time.", nameof(proposal));
    }

    private static string ScopeKey(Guid? tenantId, EconomyValueMovementCapability capability, string jurisdiction) =>
        $"{tenantId?.ToString("N") ?? "global"}:{(int)capability}:{jurisdiction}";

    private static EconomyCapabilityPolicy Map(EconomyCapabilityPolicyRow row, DateTimeOffset now)
    {
        var state = row.IsActive
            ? row.ExpiresAt <= now ? EconomyCapabilityPolicyState.Expired : EconomyCapabilityPolicyState.Active
            : row.ApprovedBy is null ? EconomyCapabilityPolicyState.PendingApproval : EconomyCapabilityPolicyState.Approved;
        return new EconomyCapabilityPolicy(
            row.Id, row.ScopeKey, row.TenantId, row.Capability, row.JurisdictionCode, row.Version,
            row.CanonicalPayload, row.PayloadHash, row.KeyId, row.Signature, row.ProposedBy, row.ApprovedBy,
            row.ProposedAt, row.ApprovedAt, row.EffectiveAt, row.ExpiresAt, row.ProviderReady, state);
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed record EconomyKillSwitchScope(
    string ScopeKey,
    Guid? TenantId,
    EconomyValueMovementCapability? Capability)
{
    public static EconomyKillSwitchScope Global { get; } = new("global", null, null);

    public static EconomyKillSwitchScope ForTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        return new EconomyKillSwitchScope($"tenant:{tenantId:N}", tenantId, null);
    }

    public static EconomyKillSwitchScope ForCapability(
        Guid tenantId,
        EconomyValueMovementCapability capability)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        if (!Enum.IsDefined(capability)) throw new ArgumentOutOfRangeException(nameof(capability));
        return new EconomyKillSwitchScope($"tenant:{tenantId:N}:capability:{(int)capability}", tenantId, capability);
    }
}

public sealed record EconomyKillSwitchState(
    Guid Id,
    EconomyKillSwitchScope Scope,
    long Epoch,
    bool IsActive,
    string Reason,
    Guid ActivatedBy,
    DateTimeOffset ActivatedAt,
    Guid? ReleaseProposedBy,
    DateTimeOffset? ReleaseProposedAt,
    IReadOnlyList<Guid> ReleaseApprovers,
    DateTimeOffset? ReleasedAt);

public interface IKillSwitchReleaseReadinessGate
{
    ValueTask<bool> IsReadyAsync(EconomyKillSwitchScope scope, CancellationToken cancellationToken);
}

public interface IEconomyKillSwitchStore
{
    ValueTask<EconomyKillSwitchState> ActivateAsync(
        Guid activationId,
        EconomyKillSwitchScope scope,
        string reason,
        Guid actorId,
        DateTimeOffset activatedAt,
        CancellationToken cancellationToken);

    ValueTask<EconomyKillSwitchState> ProposeReleaseAsync(
        Guid killSwitchId,
        Guid actorId,
        string reauthenticationHash,
        DateTimeOffset proposedAt,
        CancellationToken cancellationToken);

    ValueTask<EconomyKillSwitchState> ApproveReleaseAsync(
        Guid killSwitchId,
        Guid actorId,
        string reauthenticationHash,
        DateTimeOffset approvedAt,
        CancellationToken cancellationToken);

    ValueTask<EconomyKillSwitchState> TryReleaseAsync(
        Guid killSwitchId,
        DateTimeOffset releasedAt,
        CancellationToken cancellationToken);
}

public sealed class PostgreSqlEconomyKillSwitchStore : IEconomyKillSwitchStore
{
    private readonly DbContext _db;
    private readonly IKillSwitchReleaseReadinessGate _readiness;

    public PostgreSqlEconomyKillSwitchStore(
        IApplicationDbContext context,
        IKillSwitchReleaseReadinessGate readiness)
    {
        _db = PostgreSqlEntityRiskGraphStore.RequireRelationalContext(context);
        ArgumentNullException.ThrowIfNull(readiness);
        _readiness = readiness;
    }

    public async ValueTask<EconomyKillSwitchState> ActivateAsync(
        Guid activationId,
        EconomyKillSwitchScope scope,
        string reason,
        Guid actorId,
        DateTimeOffset activatedAt,
        CancellationToken cancellationToken)
    {
        if (activationId == Guid.Empty) throw new ArgumentException("Activation ID cannot be empty.", nameof(activationId));
        ValidateScope(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (actorId == Guid.Empty) throw new ArgumentException("Actor ID cannot be empty.", nameof(actorId));
        var requestHash = Hash(string.Join('|', activationId.ToString("N"), scope.ScopeKey, reason.Trim(),
            actorId.ToString("N"), activatedAt.UtcTicks));
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async token =>
        {
            var replay = await _db.Set<EconomyKillSwitchRow>()
                .SingleOrDefaultAsync(row => row.Id == activationId, token);
            if (replay is not null)
            {
                if (replay.RequestHash != requestHash)
                    throw new RiskDecisionReuseException("A kill-switch activation ID cannot be reused with different inputs.");
                return await MapAsync(replay, token);
            }
            if (await _db.Set<EconomyKillSwitchRow>().AnyAsync(
                    row => row.ScopeKey == scope.ScopeKey && row.IsActive, token))
                throw new InvalidOperationException("A kill switch is already active for this scope.");
            var epoch = (await _db.Set<EconomyKillSwitchRow>()
                .Where(row => row.ScopeKey == scope.ScopeKey)
                .Select(row => (long?)row.Epoch)
                .MaxAsync(token) ?? 0) + 1;
            var row = new EconomyKillSwitchRow
            {
                Id = activationId, ScopeKey = scope.ScopeKey, TenantId = scope.TenantId,
                Capability = scope.Capability, Epoch = epoch, IsActive = true, Reason = reason.Trim(),
                RequestHash = requestHash, ActivatedBy = actorId, ActivatedAt = activatedAt
            };
            _db.Set<EconomyKillSwitchRow>().Add(row);
            await _db.SaveChangesAsync(token);
            return await MapAsync(row, token);
        }, cancellationToken);
    }

    public async ValueTask<EconomyKillSwitchState> ProposeReleaseAsync(
        Guid killSwitchId,
        Guid actorId,
        string reauthenticationHash,
        DateTimeOffset proposedAt,
        CancellationToken cancellationToken)
    {
        ValidateReleaseInput(killSwitchId, actorId, reauthenticationHash);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async token =>
        {
            var row = await ActiveAsync(killSwitchId, token);
            if (row.ReleaseProposedBy is not null)
                throw new InvalidOperationException("A kill-switch release has already been proposed.");
            if (proposedAt < row.ActivatedAt)
                throw new ArgumentException("Release proposal cannot predate activation.", nameof(proposedAt));
            row.ReleaseProposedBy = actorId;
            row.ReleaseProposalReauthenticationHash = reauthenticationHash.Trim();
            row.ReleaseProposedAt = proposedAt;
            await _db.SaveChangesAsync(token);
            return await MapAsync(row, token);
        }, cancellationToken);
    }

    public async ValueTask<EconomyKillSwitchState> ApproveReleaseAsync(
        Guid killSwitchId,
        Guid actorId,
        string reauthenticationHash,
        DateTimeOffset approvedAt,
        CancellationToken cancellationToken)
    {
        ValidateReleaseInput(killSwitchId, actorId, reauthenticationHash);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async token =>
        {
            var row = await ActiveAsync(killSwitchId, token);
            if (row.ReleaseProposedBy is null || row.ReleaseProposedAt is null)
                throw new InvalidOperationException("Kill-switch release must be proposed before approval.");
            if (row.ReleaseProposedBy == actorId)
                throw new InvalidOperationException("The release proposer cannot approve their own proposal.");
            if (approvedAt < row.ReleaseProposedAt)
                throw new ArgumentException("Release approval cannot predate its proposal.", nameof(approvedAt));
            var approvals = await _db.Set<EconomyKillSwitchReleaseApprovalRow>()
                .Where(item => item.KillSwitchId == killSwitchId)
                .ToArrayAsync(token);
            if (approvals.Any(item => item.ActorId == actorId))
                throw new InvalidOperationException("A release approver cannot approve twice.");
            if (approvals.Length >= 2)
                throw new InvalidOperationException("Kill-switch release already has the required approvals.");
            _db.Set<EconomyKillSwitchReleaseApprovalRow>().Add(new EconomyKillSwitchReleaseApprovalRow
            {
                Id = Guid.NewGuid(), KillSwitchId = killSwitchId, ActorId = actorId,
                ReauthenticationHash = reauthenticationHash.Trim(), ApprovedAt = approvedAt
            });
            await _db.SaveChangesAsync(token);
            return await MapAsync(row, token);
        }, cancellationToken);
    }

    public async ValueTask<EconomyKillSwitchState> TryReleaseAsync(
        Guid killSwitchId,
        DateTimeOffset releasedAt,
        CancellationToken cancellationToken)
    {
        if (killSwitchId == Guid.Empty) throw new ArgumentException("Kill switch ID cannot be empty.", nameof(killSwitchId));
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async token =>
        {
            var row = await ActiveAsync(killSwitchId, token);
            if (row.ReleaseProposedBy is null || row.ReleaseProposedAt is null)
                throw new InvalidOperationException("Kill-switch release has not been proposed.");
            var approvalCount = await _db.Set<EconomyKillSwitchReleaseApprovalRow>()
                .CountAsync(item => item.KillSwitchId == killSwitchId, token);
            if (approvalCount != 2)
                throw new InvalidOperationException("Kill-switch release requires exactly two independent approvals.");
            var scope = new EconomyKillSwitchScope(row.ScopeKey, row.TenantId, row.Capability);
            if (!await _readiness.IsReadyAsync(scope, token))
                return await MapAsync(row, token);
            if (releasedAt < row.ReleaseProposedAt)
                throw new ArgumentException("Release cannot predate its proposal.", nameof(releasedAt));
            row.IsActive = false;
            row.ReleasedAt = releasedAt;
            await _db.SaveChangesAsync(token);
            return await MapAsync(row, token);
        }, cancellationToken);
    }

    private async Task<EconomyKillSwitchRow> ActiveAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Set<EconomyKillSwitchRow>().SingleOrDefaultAsync(row => row.Id == id && row.IsActive, cancellationToken)
        ?? throw new KeyNotFoundException("Active kill switch was not found.");

    private async ValueTask<EconomyKillSwitchState> MapAsync(
        EconomyKillSwitchRow row,
        CancellationToken cancellationToken)
    {
        var approvers = await _db.Set<EconomyKillSwitchReleaseApprovalRow>().AsNoTracking()
            .Where(item => item.KillSwitchId == row.Id)
            .OrderBy(item => item.ApprovedAt)
            .Select(item => item.ActorId)
            .ToArrayAsync(cancellationToken);
        return new EconomyKillSwitchState(
            row.Id, new EconomyKillSwitchScope(row.ScopeKey, row.TenantId, row.Capability), row.Epoch,
            row.IsActive, row.Reason, row.ActivatedBy, row.ActivatedAt, row.ReleaseProposedBy,
            row.ReleaseProposedAt, approvers, row.ReleasedAt);
    }

    private static void ValidateScope(EconomyKillSwitchScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope.ScopeKey);
        if (scope.Capability is not null && !Enum.IsDefined(scope.Capability.Value))
            throw new ArgumentOutOfRangeException(nameof(scope));
        if (scope.Capability is not null && scope.TenantId is null)
            throw new ArgumentException("Capability kill switches must be tenant-scoped.", nameof(scope));
    }

    private static void ValidateReleaseInput(Guid id, Guid actorId, string reauthenticationHash)
    {
        if (id == Guid.Empty) throw new ArgumentException("Kill switch ID cannot be empty.", nameof(id));
        if (actorId == Guid.Empty) throw new ArgumentException("Actor ID cannot be empty.", nameof(actorId));
        ArgumentException.ThrowIfNullOrWhiteSpace(reauthenticationHash);
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
