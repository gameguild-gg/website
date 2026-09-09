using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.TrustSafety;

public enum TrustSafetyEventKind
{
    Moderation = 1, Report = 2, MarketplaceAbuse = 3, WashTrading = 4,
    BountyCycling = 5, AdFraud = 6, AccountRestriction = 7, Appeal = 8
}

public enum TrustSafetyOutcome { Approved = 1, Rejected = 2, NeedsReview = 3, Unavailable = 4 }
public enum TrustSafetyAppealState { Submitted = 1, Assigned = 2, Upheld = 3, Overturned = 4 }

public sealed record TrustSafetyEvent(
    Guid Id, string EventId, Guid TenantId, string SubjectHash, TrustSafetyEventKind Kind,
    long Version, TrustSafetyOutcome Outcome, long PolicyVersion, string PayloadHash,
    string EvidenceHash, string RawObjectReference, string KeyId, string Signature,
    DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt, DateTimeOffset ReceivedAt);

public sealed record TrustSafetyIngestionResult(ComplianceEvidenceIngestionStatus Status, Guid? EvidenceId);

public sealed record TrustSafetyAppeal(
    Guid Id, Guid TenantId, string SubjectHash, string RestrictionReferenceHash,
    TrustSafetyAppealState State, Guid SubmittedBy, Guid? AssignedTo, Guid? DecidedBy,
    string SubmissionEvidenceHash, string? DecisionEvidenceHash, string? ReasonCode,
    DateTimeOffset SubmittedAt, DateTimeOffset? DecidedAt, long Version);

public interface ITrustSafetyEventSignatureVerifier
{
    ValueTask<bool> VerifyAsync(string canonicalPayload, string keyId, string signature, CancellationToken cancellationToken);
}

public sealed class EconomyTrustSafetyEventSignatureVerifier : ITrustSafetyEventSignatureVerifier
{
    private readonly ICapabilityPolicySignatureVerifier _verifier;

    public EconomyTrustSafetyEventSignatureVerifier(ICapabilityPolicySignatureVerifier verifier) =>
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));

    public ValueTask<bool> VerifyAsync(string canonicalPayload, string keyId, string signature, CancellationToken cancellationToken) =>
        _verifier.VerifyAsync(canonicalPayload, keyId, signature, cancellationToken);
}

public interface ITrustSafetyControlPlane
{
    ValueTask<TrustSafetyIngestionResult> IngestAsync(TrustSafetyEvent evidence, CancellationToken cancellationToken);
    ValueTask<TrustSafetyAppeal> SubmitAppealAsync(TrustSafetyAppeal appeal, CancellationToken cancellationToken);
    ValueTask<TrustSafetyAppeal> AssignAppealAsync(Guid tenantId, Guid appealId, Guid actorId, long expectedVersion, DateTimeOffset assignedAt, CancellationToken cancellationToken);
    ValueTask<TrustSafetyAppeal> DecideAppealAsync(Guid tenantId, Guid appealId, Guid actorId, long expectedVersion, bool overturn, string reasonCode, string evidenceHash, DateTimeOffset decidedAt, CancellationToken cancellationToken);
    ValueTask<IReadOnlyList<TrustSafetyAppeal>> ReadAppealsAsync(Guid tenantId, TrustSafetyAppealState? state, int limit, CancellationToken cancellationToken);
}

public sealed class PostgreSqlTrustSafetyControlPlane : ITrustSafetyControlPlane
{
    private static readonly Guid SystemActorId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private readonly DbContext _db;
    private readonly ITrustSafetyEventSignatureVerifier _signatureVerifier;
    private readonly IComplianceEvidenceStore _evidence;
    private readonly IComplianceHoldStore _holds;

    public PostgreSqlTrustSafetyControlPlane(
        IApplicationDbContext context,
        ITrustSafetyEventSignatureVerifier signatureVerifier,
        IComplianceEvidenceStore evidence,
        IComplianceHoldStore holds)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext ?? throw new InvalidOperationException("Trust/Safety requires the relational application DbContext.");
        _signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        _holds = holds ?? throw new ArgumentNullException(nameof(holds));
    }

    public async ValueTask<TrustSafetyIngestionResult> IngestAsync(
        TrustSafetyEvent evidence,
        CancellationToken cancellationToken)
    {
        ValidateEvent(evidence);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var replay = await _db.Set<TrustSafetyEventInboxRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.EventId == evidence.EventId, cancellationToken);
        if (replay is not null)
        {
            if (replay.PayloadHash != evidence.PayloadHash)
                throw new TrustSafetyConflictException("The internal event was replayed with a different payload.");
            return new TrustSafetyIngestionResult(
                replay.ProcessedAt is not null
                    ? ComplianceEvidenceIngestionStatus.Duplicate
                    : string.Equals(replay.ProcessingError, "out-of-order-event", StringComparison.Ordinal)
                        ? ComplianceEvidenceIngestionStatus.Deferred
                        : ComplianceEvidenceIngestionStatus.Rejected,
                null);
        }

        var versionReplay = await _db.Set<TrustSafetyEventInboxRow>().AsNoTracking()
            .SingleOrDefaultAsync(
                row => row.TenantId == evidence.TenantId &&
                       row.SubjectHash == evidence.SubjectHash &&
                       row.Version == evidence.Version,
                cancellationToken);
        if (versionReplay is not null)
            throw new TrustSafetyConflictException(
                "The Trust/Safety subject version is already bound to another internal event.");

        var canonical = Canonicalize(evidence);
        var signatureVerified = await _signatureVerifier.VerifyAsync(
            canonical, evidence.KeyId, evidence.Signature, cancellationToken);
        var inbox = ToRow(evidence, signatureVerified);
        _db.Set<TrustSafetyEventInboxRow>().Add(inbox);
        if (!signatureVerified)
        {
            inbox.ProcessingError = "invalid-signature";
            await _db.SaveChangesAsync(cancellationToken);
            return new TrustSafetyIngestionResult(ComplianceEvidenceIngestionStatus.Rejected, null);
        }

        var state = await _db.Set<TrustSafetySubjectStateRow>()
            .SingleOrDefaultAsync(row => row.TenantId == evidence.TenantId && row.SubjectHash == evidence.SubjectHash, cancellationToken);
        if (evidence.Version != (state?.Version ?? 0) + 1 || state is not null && evidence.IssuedAt <= state.IssuedAt)
        {
            inbox.ProcessingError = "out-of-order-event";
            await _db.SaveChangesAsync(cancellationToken);
            return new TrustSafetyIngestionResult(ComplianceEvidenceIngestionStatus.Deferred, null);
        }

        Guid? holdId = state?.HoldId;
        if (evidence.Outcome == TrustSafetyOutcome.Approved && holdId is not null)
        {
            await _holds.ReleaseAsync(holdId.Value, SystemActorId, evidence.EvidenceHash, evidence.ReceivedAt, cancellationToken);
            holdId = null;
        }
        else if (evidence.Outcome != TrustSafetyOutcome.Approved &&
                 (holdId is null || !await _holds.IsActiveAsync(
                     new ComplianceHoldScope(evidence.TenantId, evidence.SubjectHash, null), evidence.ReceivedAt, cancellationToken)))
        {
            holdId = Guid.NewGuid();
            await _holds.ActivateAsync(new ComplianceHoldActivation(
                holdId.Value, new ComplianceHoldScope(evidence.TenantId, evidence.SubjectHash, null),
                Hash(evidence.EventId), evidence.Kind.ToString(), evidence.EvidenceHash,
                $"trust-safety:{evidence.EventId}", SystemActorId, evidence.ReceivedAt,
                evidence.ExpiresAt), cancellationToken);
        }

        if (state is null)
        {
            state = new TrustSafetySubjectStateRow { TenantId = evidence.TenantId, SubjectHash = evidence.SubjectHash };
            _db.Set<TrustSafetySubjectStateRow>().Add(state);
        }
        state.Version = evidence.Version;
        state.Outcome = evidence.Outcome;
        state.LastEventId = evidence.EventId;
        state.EvidenceHash = evidence.EvidenceHash;
        state.HoldId = holdId;
        state.IssuedAt = evidence.IssuedAt;
        state.ExpiresAt = evidence.ExpiresAt;
        state.UpdatedAt = evidence.ReceivedAt;

        var envelope = ComplianceEvidenceEnvelope.Create(
            "internal-trust-safety", "production", evidence.EventId, evidence.TenantId,
            evidence.SubjectHash, evidence.Version, Map(evidence.Outcome), evidence.IssuedAt,
            evidence.ExpiresAt, evidence.PolicyVersion, evidence.PayloadHash, true,
            evidence.RawObjectReference, evidence.ReceivedAt) with
        { EvidenceKind = ComplianceEvidenceKinds.TrustSafety };
        var ingestion = await _evidence.IngestAsync(envelope, cancellationToken);
        if (ingestion.Status is not (ComplianceEvidenceIngestionStatus.Published or ComplianceEvidenceIngestionStatus.Duplicate))
            throw new TrustSafetyConflictException("Trust/Safety evidence could not be published in sequence.");
        inbox.ProcessedAt = evidence.ReceivedAt;
        await _db.SaveChangesAsync(cancellationToken);
        return new TrustSafetyIngestionResult(ingestion.Status, ingestion.EvidenceId);
        }, cancellationToken);
    }

    public async ValueTask<TrustSafetyAppeal> SubmitAppealAsync(
        TrustSafetyAppeal appeal,
        CancellationToken cancellationToken)
    {
        ValidateAppeal(appeal);
        var replay = await _db.Set<TrustSafetyAppealRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == appeal.Id, cancellationToken);
        if (replay is not null)
        {
            if (replay.SubmissionEvidenceHash != appeal.SubmissionEvidenceHash || replay.SubmittedBy != appeal.SubmittedBy)
                throw new TrustSafetyConflictException("The appeal was replayed with different inputs.");
            return Map(replay);
        }
        var row = ToRow(appeal);
        _db.Set<TrustSafetyAppealRow>().Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
    }

    public async ValueTask<TrustSafetyAppeal> AssignAppealAsync(
        Guid tenantId, Guid appealId, Guid actorId, long expectedVersion, DateTimeOffset assignedAt,
        CancellationToken cancellationToken)
    {
        ValidateTenantActor(tenantId, actorId);
        var row = await ReadAppealAsync(tenantId, appealId, cancellationToken);
        if (row.State != TrustSafetyAppealState.Submitted || row.Version != expectedVersion)
            throw new TrustSafetyConflictException("The appeal cannot be assigned at this version.");
        row.State = TrustSafetyAppealState.Assigned;
        row.AssignedTo = actorId;
        row.Version++;
        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
    }

    public async ValueTask<TrustSafetyAppeal> DecideAppealAsync(
        Guid tenantId, Guid appealId, Guid actorId, long expectedVersion, bool overturn,
        string reasonCode, string evidenceHash, DateTimeOffset decidedAt, CancellationToken cancellationToken)
    {
        ValidateTenantActor(tenantId, actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceHash);
        var row = await ReadAppealAsync(tenantId, appealId, cancellationToken);
        if (row.State != TrustSafetyAppealState.Assigned || row.AssignedTo != actorId || row.Version != expectedVersion)
            throw new TrustSafetyConflictException("The appeal is not assigned to this reviewer at this version.");
        row.State = overturn ? TrustSafetyAppealState.Overturned : TrustSafetyAppealState.Upheld;
        row.DecidedBy = actorId;
        row.DecisionEvidenceHash = evidenceHash.Trim();
        row.ReasonCode = reasonCode.Trim();
        row.DecidedAt = decidedAt;
        row.Version++;
        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
    }

    public async ValueTask<IReadOnlyList<TrustSafetyAppeal>> ReadAppealsAsync(
        Guid tenantId,
        TrustSafetyAppealState? state,
        int limit,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        if (state is not null && !Enum.IsDefined(state.Value)) throw new ArgumentOutOfRangeException(nameof(state));
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        var query = _db.Set<TrustSafetyAppealRow>().AsNoTracking()
            .Where(row => row.TenantId == tenantId);
        if (state is not null) query = query.Where(row => row.State == state.Value);
        var rows = await query.OrderByDescending(row => row.SubmittedAt).ThenBy(row => row.Id)
            .Take(limit).ToArrayAsync(cancellationToken);
        return Array.AsReadOnly(rows.Select(Map).ToArray());
    }

    public static string Canonicalize(TrustSafetyEvent value) => string.Join('\n',
        value.EventId, value.TenantId.ToString("N"), value.SubjectHash,
        ((int)value.Kind).ToString(CultureInfo.InvariantCulture), value.Version.ToString(CultureInfo.InvariantCulture),
        ((int)value.Outcome).ToString(CultureInfo.InvariantCulture), value.PolicyVersion.ToString(CultureInfo.InvariantCulture),
        value.PayloadHash, value.EvidenceHash, value.RawObjectReference,
        value.IssuedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        value.ExpiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    private async ValueTask<TrustSafetyAppealRow> ReadAppealAsync(Guid tenantId, Guid appealId, CancellationToken cancellationToken) =>
        await _db.Set<TrustSafetyAppealRow>().SingleOrDefaultAsync(row => row.Id == appealId && row.TenantId == tenantId, cancellationToken)
        ?? throw new KeyNotFoundException("Trust/Safety appeal was not found.");

    private static ComplianceEvidenceResult Map(TrustSafetyOutcome value) => value switch
    {
        TrustSafetyOutcome.Approved => ComplianceEvidenceResult.Approved,
        TrustSafetyOutcome.Rejected => ComplianceEvidenceResult.Rejected,
        TrustSafetyOutcome.NeedsReview => ComplianceEvidenceResult.NeedsReview,
        _ => ComplianceEvidenceResult.Unavailable
    };

    private static TrustSafetyEventInboxRow ToRow(TrustSafetyEvent value, bool signatureVerified) => new()
    {
        Id = value.Id, EventId = value.EventId, TenantId = value.TenantId, SubjectHash = value.SubjectHash,
        Kind = value.Kind, Version = value.Version, Outcome = value.Outcome, PolicyVersion = value.PolicyVersion,
        PayloadHash = value.PayloadHash, EvidenceHash = value.EvidenceHash, RawObjectReference = value.RawObjectReference,
        KeyId = value.KeyId, Signature = value.Signature, SignatureVerified = signatureVerified,
        IssuedAt = value.IssuedAt, ExpiresAt = value.ExpiresAt, ReceivedAt = value.ReceivedAt
    };

    private static TrustSafetyAppealRow ToRow(TrustSafetyAppeal value) => new()
    {
        Id = value.Id, TenantId = value.TenantId, SubjectHash = value.SubjectHash,
        RestrictionReferenceHash = value.RestrictionReferenceHash, State = value.State,
        SubmittedBy = value.SubmittedBy, AssignedTo = value.AssignedTo, DecidedBy = value.DecidedBy,
        SubmissionEvidenceHash = value.SubmissionEvidenceHash, DecisionEvidenceHash = value.DecisionEvidenceHash,
        ReasonCode = value.ReasonCode, SubmittedAt = value.SubmittedAt, DecidedAt = value.DecidedAt, Version = value.Version
    };

    private static TrustSafetyAppeal Map(TrustSafetyAppealRow value) => new(
        value.Id, value.TenantId, value.SubjectHash, value.RestrictionReferenceHash, value.State,
        value.SubmittedBy, value.AssignedTo, value.DecidedBy, value.SubmissionEvidenceHash,
        value.DecisionEvidenceHash, value.ReasonCode, value.SubmittedAt, value.DecidedAt, value.Version);

    private static void ValidateEvent(TrustSafetyEvent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Id == Guid.Empty || value.TenantId == Guid.Empty) throw new ArgumentException("Event IDs are required.", nameof(value));
        ArgumentException.ThrowIfNullOrWhiteSpace(value.EventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.SubjectHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.PayloadHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.EvidenceHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.RawObjectReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.KeyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.Signature);
        if (!Enum.IsDefined(value.Kind) || !Enum.IsDefined(value.Outcome) || value.Version <= 0 || value.PolicyVersion <= 0 ||
            value.ExpiresAt <= value.IssuedAt || value.ReceivedAt < value.IssuedAt)
            throw new ArgumentException("Trust/Safety event metadata is invalid.", nameof(value));
    }

    private static void ValidateAppeal(TrustSafetyAppeal value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Id == Guid.Empty || value.TenantId == Guid.Empty || value.SubmittedBy == Guid.Empty)
            throw new ArgumentException("Appeal IDs are required.", nameof(value));
        if (value.State != TrustSafetyAppealState.Submitted || value.Version != 1 || value.AssignedTo is not null ||
            value.DecidedBy is not null || value.DecidedAt is not null)
            throw new ArgumentException("A new appeal must be unassigned and submitted at version one.", nameof(value));
        ArgumentException.ThrowIfNullOrWhiteSpace(value.SubjectHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.RestrictionReferenceHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.SubmissionEvidenceHash);
    }

    private static void ValidateTenantActor(Guid tenantId, Guid actorId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        if (actorId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorId));
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed class TrustSafetyConflictException(string message) : InvalidOperationException(message);
