using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Risk;

public static class ComplianceHoldEventKinds
{
    public const string Activated = "Activated";
    public const string ReleaseProposed = "ReleaseProposed";
    public const string ReleaseApproved = "ReleaseApproved";
    public const string Released = "Released";
}

public sealed record ComplianceHoldEvent(
    int Sequence,
    Guid HoldId,
    string Kind,
    Guid ActorId,
    string EvidenceHash,
    DateTimeOffset OccurredAt);

public sealed record ComplianceHoldAdministrationState(
    ComplianceHold Hold,
    Guid? ReleaseProposedBy,
    DateTimeOffset? ReleaseProposedAt,
    int? RequiredReleaseApprovals,
    string? ReleasePolicyEvidenceHash,
    IReadOnlyList<Guid> ReleaseApprovers);

public sealed record ComplianceHoldPage(
    IReadOnlyList<ComplianceHoldAdministrationState> Items,
    string? NextCursor);

public sealed record ComplianceHoldReleasePolicyAuthorization(
    int RequiredApprovals,
    string EvidenceHash);

public interface IComplianceHoldReleasePolicyResolver
{
    ValueTask<ComplianceHoldReleasePolicyAuthorization> ResolveAsync(
        Guid tenantId,
        EconomyValueMovementCapability? capability,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public interface IComplianceHoldAdministrationStore
{
    ValueTask<ComplianceHoldPage> ListAsync(
        Guid tenantId,
        bool? active,
        EconomyValueMovementCapability? capability,
        int limit,
        string? cursor,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    ValueTask<ComplianceHoldAdministrationState> CurrentAsync(
        Guid tenantId,
        Guid holdId,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<ComplianceHoldEvent>> EventsAsync(
        Guid tenantId,
        Guid holdId,
        CancellationToken cancellationToken);

    ValueTask<ComplianceHoldAdministrationState> ProposeReleaseAsync(
        Guid tenantId,
        Guid holdId,
        Guid actorId,
        string evidenceHash,
        DateTimeOffset proposedAt,
        CancellationToken cancellationToken);

    ValueTask<ComplianceHoldAdministrationState> ApproveReleaseAsync(
        Guid tenantId,
        Guid holdId,
        Guid actorId,
        string evidenceHash,
        DateTimeOffset approvedAt,
        CancellationToken cancellationToken);
}

public sealed class PostgreSqlComplianceHoldReleasePolicyResolver(
    IApplicationDbContext context,
    ICapabilityPolicySignatureVerifier signatureVerifier) : IComplianceHoldReleasePolicyResolver
{
    private readonly DbContext _db = PostgreSqlEntityRiskGraphStore.RequireRelationalContext(context);
    private readonly ICapabilityPolicySignatureVerifier _signatureVerifier =
        signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));

    public async ValueTask<ComplianceHoldReleasePolicyAuthorization> ResolveAsync(
        Guid tenantId,
        EconomyValueMovementCapability? capability,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        if (capability is not null && !Enum.IsDefined(capability.Value))
            throw new ArgumentOutOfRangeException(nameof(capability));

        var query = _db.Set<EconomyCapabilityPolicyRow>().AsNoTracking()
            .Where(row =>
                row.IsActive &&
                row.ApprovedBy != null &&
                row.EffectiveAt <= now &&
                row.ExpiresAt > now &&
                (row.TenantId == null || row.TenantId == tenantId));
        if (capability is not null)
            query = query.Where(row => row.Capability == capability.Value);

        var policies = await query
            .OrderBy(row => row.Capability)
            .ThenBy(row => row.JurisdictionCode)
            .ThenBy(row => row.TenantId == null ? 0 : 1)
            .ThenBy(row => row.Version)
            .ToArrayAsync(cancellationToken);
        if (policies.Length == 0)
            throw new InvalidOperationException(
                "No active signed policy authorizes administrative compliance-hold release.");

        var approvals = new List<int>(policies.Length);
        foreach (var policy in policies)
        {
            if (string.IsNullOrWhiteSpace(policy.KeyId) ||
                string.IsNullOrWhiteSpace(policy.Signature) ||
                !await _signatureVerifier.VerifyAsync(
                    policy.CanonicalPayload,
                    policy.KeyId,
                    policy.Signature,
                    cancellationToken))
                throw new InvalidOperationException(
                    "A policy governing compliance-hold release has an invalid signature.");

            approvals.Add(EconomyProtectedRiskPolicy.Parse(policy.CanonicalPayload).RequiredReviewApprovals);
        }

        var canonicalEvidence = string.Join(
            '|',
            policies.Select(policy => string.Join(
                ':',
                policy.Id.ToString("N"),
                policy.Version.ToString(CultureInfo.InvariantCulture),
                policy.PayloadHash,
                policy.KeyId)));
        return new ComplianceHoldReleasePolicyAuthorization(
            approvals.Max(),
            Hash(canonicalEvidence));
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed class PostgreSqlComplianceHoldAdministrationStore(
    IApplicationDbContext context,
    IComplianceHoldReleasePolicyResolver releasePolicy) : IComplianceHoldAdministrationStore
{
    private readonly DbContext _db = PostgreSqlEntityRiskGraphStore.RequireRelationalContext(context);
    private readonly IComplianceHoldReleasePolicyResolver _releasePolicy =
        releasePolicy ?? throw new ArgumentNullException(nameof(releasePolicy));

    public async ValueTask<ComplianceHoldPage> ListAsync(
        Guid tenantId,
        bool? active,
        EconomyValueMovementCapability? capability,
        int limit,
        string? cursor,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ValidateTenant(tenantId);
        if (capability is not null && !Enum.IsDefined(capability.Value))
            throw new ArgumentOutOfRangeException(nameof(capability));
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var position = DecodeCursor(cursor);
        var query = _db.Set<EconomyComplianceHoldRow>().AsNoTracking()
            .Where(row => row.TenantId == tenantId);
        if (active is true)
            query = query.Where(row =>
                row.ReleasedAt == null && row.ActivatedAt <= now && row.ExpiresAt > now);
        else if (active is false)
            query = query.Where(row =>
                row.ReleasedAt != null || row.ActivatedAt > now || row.ExpiresAt <= now);
        if (capability is not null)
            query = query.Where(row => row.Capability == null || row.Capability == capability.Value);
        if (position is not null)
        {
            var activatedAt = position.Value.ActivatedAt;
            var id = position.Value.Id;
            query = query.Where(row =>
                row.ActivatedAt < activatedAt ||
                row.ActivatedAt == activatedAt && row.Id.CompareTo(id) > 0);
        }

        var rows = await query
            .OrderByDescending(row => row.ActivatedAt)
            .ThenBy(row => row.Id)
            .Take(limit + 1)
            .ToArrayAsync(cancellationToken);
        var pageRows = rows.Take(limit).ToArray();
        var items = new List<ComplianceHoldAdministrationState>(pageRows.Length);
        foreach (var row in pageRows)
            items.Add(await MapAsync(row, cancellationToken));
        var nextCursor = rows.Length > limit && pageRows.Length > 0
            ? EncodeCursor(pageRows[^1].ActivatedAt, pageRows[^1].Id)
            : null;
        return new ComplianceHoldPage(items, nextCursor);
    }

    public async ValueTask<ComplianceHoldAdministrationState> CurrentAsync(
        Guid tenantId,
        Guid holdId,
        CancellationToken cancellationToken)
    {
        ValidateTenantHold(tenantId, holdId);
        var row = await _db.Set<EconomyComplianceHoldRow>().AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.TenantId == tenantId && item.Id == holdId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Compliance hold was not found.");
        return await MapAsync(row, cancellationToken);
    }

    public async ValueTask<IReadOnlyList<ComplianceHoldEvent>> EventsAsync(
        Guid tenantId,
        Guid holdId,
        CancellationToken cancellationToken)
    {
        _ = await CurrentAsync(tenantId, holdId, cancellationToken);
        return await _db.Set<EconomyComplianceHoldEventRow>().AsNoTracking()
            .Where(row => row.HoldId == holdId)
            .OrderBy(row => row.Sequence)
            .Select(row => new ComplianceHoldEvent(
                row.Sequence,
                row.HoldId,
                row.Kind,
                row.ActorId,
                row.EvidenceHash,
                row.OccurredAt))
            .ToArrayAsync(cancellationToken);
    }

    public async ValueTask<ComplianceHoldAdministrationState> ProposeReleaseAsync(
        Guid tenantId,
        Guid holdId,
        Guid actorId,
        string evidenceHash,
        DateTimeOffset proposedAt,
        CancellationToken cancellationToken)
    {
        ValidateReleaseInput(tenantId, holdId, actorId, evidenceHash);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db,
            IsolationLevel.Serializable,
            async token =>
            {
                var row = await ActiveAsync(tenantId, holdId, proposedAt, token);
                if (row.ActivatedBy == actorId)
                    throw new InvalidOperationException(
                        "The hold activator cannot propose its administrative release.");
                if (row.ReleaseProposedBy is not null)
                    throw new InvalidOperationException(
                        "Compliance-hold release has already been proposed.");

                var authorization = await _releasePolicy.ResolveAsync(
                    tenantId,
                    row.Capability,
                    proposedAt,
                    token);
                if (authorization.RequiredApprovals is < 1 or > 2 ||
                    string.IsNullOrWhiteSpace(authorization.EvidenceHash))
                    throw new InvalidOperationException(
                        "The signed hold-release policy returned invalid authorization.");

                row.ReleaseProposedBy = actorId;
                row.ReleaseProposedAt = proposedAt;
                row.RequiredReleaseApprovals = authorization.RequiredApprovals;
                row.ReleasePolicyEvidenceHash = authorization.EvidenceHash.Trim();
                await AppendEventAsync(
                    row.Id,
                    ComplianceHoldEventKinds.ReleaseProposed,
                    actorId,
                    evidenceHash,
                    proposedAt,
                    token);
                await _db.SaveChangesAsync(token);
                return await MapAsync(row, token);
            },
            cancellationToken);
    }

    public async ValueTask<ComplianceHoldAdministrationState> ApproveReleaseAsync(
        Guid tenantId,
        Guid holdId,
        Guid actorId,
        string evidenceHash,
        DateTimeOffset approvedAt,
        CancellationToken cancellationToken)
    {
        ValidateReleaseInput(tenantId, holdId, actorId, evidenceHash);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db,
            IsolationLevel.Serializable,
            async token =>
            {
                var row = await ActiveAsync(tenantId, holdId, approvedAt, token);
                if (row.ReleaseProposedBy is null ||
                    row.ReleaseProposedAt is null ||
                    row.RequiredReleaseApprovals is null ||
                    string.IsNullOrWhiteSpace(row.ReleasePolicyEvidenceHash))
                    throw new InvalidOperationException(
                        "Compliance-hold release must be proposed under a signed policy before approval.");
                if (row.ReleaseProposedBy == actorId || row.ActivatedBy == actorId)
                    throw new InvalidOperationException(
                        "Hold activator, release proposer, and reviewers must be distinct.");
                if (approvedAt < row.ReleaseProposedAt)
                    throw new ArgumentException(
                        "Release approval cannot predate its proposal.",
                        nameof(approvedAt));

                var approvalQuery = _db.Set<EconomyComplianceHoldEventRow>()
                    .Where(item =>
                        item.HoldId == holdId &&
                        item.Kind == ComplianceHoldEventKinds.ReleaseApproved);
                if (await approvalQuery.AnyAsync(item => item.ActorId == actorId, token))
                    throw new InvalidOperationException(
                        "A hold-release reviewer cannot approve twice.");
                var approvalCount = await approvalQuery.CountAsync(token);
                if (approvalCount >= row.RequiredReleaseApprovals)
                    throw new InvalidOperationException(
                        "Compliance-hold release already has the required approvals.");

                await AppendEventAsync(
                    row.Id,
                    ComplianceHoldEventKinds.ReleaseApproved,
                    actorId,
                    evidenceHash,
                    approvedAt,
                    token);
                approvalCount++;
                if (approvalCount == row.RequiredReleaseApprovals)
                {
                    row.ReleasedBy = actorId;
                    row.ReleasedAt = approvedAt;
                    await AppendEventAsync(
                        row.Id,
                        ComplianceHoldEventKinds.Released,
                        actorId,
                        evidenceHash,
                        approvedAt,
                        token);
                }

                await _db.SaveChangesAsync(token);
                return await MapAsync(row, token);
            },
            cancellationToken);
    }

    internal static string EncodeCursor(DateTimeOffset activatedAt, Guid id) =>
        $"{activatedAt.UtcTicks:X16}{id:N}";

    internal static (DateTimeOffset ActivatedAt, Guid Id)? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        if (cursor.Length != 48 ||
            !long.TryParse(
                cursor.AsSpan(0, 16),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var ticks) ||
            !Guid.TryParseExact(cursor[16..], "N", out var id) ||
            ticks < DateTimeOffset.MinValue.UtcTicks ||
            ticks > DateTimeOffset.MaxValue.UtcTicks)
            throw new ArgumentException("Compliance-hold cursor is invalid.", nameof(cursor));
        return (new DateTimeOffset(ticks, TimeSpan.Zero), id);
    }

    private async Task<EconomyComplianceHoldRow> ActiveAsync(
        Guid tenantId,
        Guid holdId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await _db.Set<EconomyComplianceHoldRow>()
            .SingleOrDefaultAsync(
                row =>
                    row.TenantId == tenantId &&
                    row.Id == holdId &&
                    row.ReleasedAt == null &&
                    row.ActivatedAt <= now &&
                    row.ExpiresAt > now,
                cancellationToken)
        ?? throw new KeyNotFoundException("Active compliance hold was not found.");

    private async Task AppendEventAsync(
        Guid holdId,
        string kind,
        Guid actorId,
        string evidenceHash,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var persistedSequence = await _db.Set<EconomyComplianceHoldEventRow>()
            .Where(item => item.HoldId == holdId)
            .MaxAsync(item => item.Sequence, cancellationToken) + 1;
        var localSequence = _db.Set<EconomyComplianceHoldEventRow>().Local
            .Where(item => item.HoldId == holdId)
            .Select(item => item.Sequence + 1)
            .DefaultIfEmpty(persistedSequence)
            .Max();
        var sequence = Math.Max(persistedSequence, localSequence);
        _db.Set<EconomyComplianceHoldEventRow>().Add(new EconomyComplianceHoldEventRow
        {
            Id = Guid.NewGuid(),
            HoldId = holdId,
            Sequence = sequence,
            Kind = kind,
            ActorId = actorId,
            EvidenceHash = evidenceHash.Trim(),
            OccurredAt = occurredAt
        });
    }

    private async ValueTask<ComplianceHoldAdministrationState> MapAsync(
        EconomyComplianceHoldRow row,
        CancellationToken cancellationToken)
    {
        var approvers = await _db.Set<EconomyComplianceHoldEventRow>().AsNoTracking()
            .Where(item =>
                item.HoldId == row.Id &&
                item.Kind == ComplianceHoldEventKinds.ReleaseApproved)
            .OrderBy(item => item.Sequence)
            .Select(item => item.ActorId)
            .ToArrayAsync(cancellationToken);
        var hold = new ComplianceHold(
            row.Id,
            new ComplianceHoldScope(row.TenantId, row.SubjectHash, row.Capability),
            row.CaseReferenceHash,
            row.ReasonCode,
            row.EvidenceHash,
            row.ActivatedBy,
            row.ActivatedAt,
            row.ExpiresAt,
            row.ReleasedBy,
            row.ReleasedAt);
        return new ComplianceHoldAdministrationState(
            hold,
            row.ReleaseProposedBy,
            row.ReleaseProposedAt,
            row.RequiredReleaseApprovals,
            row.ReleasePolicyEvidenceHash,
            approvers);
    }

    private static void ValidateTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
    }

    private static void ValidateTenantHold(Guid tenantId, Guid holdId)
    {
        ValidateTenant(tenantId);
        if (holdId == Guid.Empty)
            throw new ArgumentException("Hold ID cannot be empty.", nameof(holdId));
    }

    private static void ValidateReleaseInput(
        Guid tenantId,
        Guid holdId,
        Guid actorId,
        string evidenceHash)
    {
        ValidateTenantHold(tenantId, holdId);
        if (actorId == Guid.Empty)
            throw new ArgumentException("Actor ID cannot be empty.", nameof(actorId));
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceHash);
    }
}
