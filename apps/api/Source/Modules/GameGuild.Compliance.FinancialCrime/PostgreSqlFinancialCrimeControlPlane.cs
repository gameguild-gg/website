using System.Data;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Compliance.FinancialCrime;

public enum FinancialCrimeOutcome { Approved = 1, Rejected = 2, NeedsReview = 3, Unavailable = 4 }
public enum FinancialCrimeCaseState { Open = 1, Assigned = 2, NeedsReview = 3, Closed = 4 }

public sealed record FinancialCrimeScreening(
    Guid Id, string Provider, string Environment, string ProviderEventId, Guid TenantId,
    string SubjectHash, long Version, FinancialCrimeOutcome Outcome, bool SanctionsMatch,
    bool PepMatch, bool AdverseMediaMatch, long PolicyVersion, string PayloadHash,
    string EvidenceHash, string RawObjectReference, bool SignatureVerified,
    DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt, DateTimeOffset NextScreenAt,
    DateTimeOffset ReceivedAt);

public sealed record FinancialCrimeCase(
    Guid Id, Guid TenantId, string SubjectHash, FinancialCrimeCaseState State,
    Guid? AssignedTo, Guid HoldId, string ReasonCode, long Version,
    DateTimeOffset OpenedAt, DateTimeOffset UpdatedAt, DateTimeOffset? ClosedAt);

public sealed record FinancialCrimeScreeningResult(FinancialCrimeScreening Screening, FinancialCrimeCase? Case);

public sealed record FinancialCrimeTransactionSignal(
    Guid Id, Guid TenantId, string SubjectHash, string OperationFingerprint,
    string SignalType, int Score, string EvidenceHash, string IdempotencyKey,
    DateTimeOffset ObservedAt, DateTimeOffset HoldExpiresAt);

public sealed record FinancialCrimeCaseDecision(
    Guid Id, Guid CaseId, Guid TenantId, string SubjectHash, long Version,
    FinancialCrimeOutcome Outcome, long PolicyVersion, string ReasonCode,
    string EvidenceHash, string RawObjectReference, Guid DecidedBy,
    DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);

public sealed record FinancialCrimeCaseEvent(
    Guid Id, Guid CaseId, int Sequence, string Kind, Guid? ActorId,
    string ReasonCode, string EvidenceHash, DateTimeOffset OccurredAt);

public sealed record FinancialCrimeRegulatoryReference(
    Guid Id, Guid CaseId, string Kind, string JurisdictionCode,
    string ReferenceHash, Guid RecordedBy, DateTimeOffset RecordedAt);

public sealed record FinancialCrimeCaseDetails(
    FinancialCrimeCase Case,
    IReadOnlyList<FinancialCrimeCaseEvent> Events,
    IReadOnlyList<FinancialCrimeCaseDecision> Decisions,
    IReadOnlyList<FinancialCrimeRegulatoryReference> RegulatoryReferences);

public interface IFinancialCrimeControlPlane
{
    ValueTask<FinancialCrimeScreeningResult> IngestScreeningAsync(FinancialCrimeScreening screening, CancellationToken cancellationToken);
    ValueTask<FinancialCrimeCase> RecordSignalAsync(FinancialCrimeTransactionSignal signal, CancellationToken cancellationToken);
    ValueTask<FinancialCrimeCase> AssignCaseAsync(Guid tenantId, Guid caseId, Guid actorId, long expectedVersion, DateTimeOffset assignedAt, CancellationToken cancellationToken);
    ValueTask<FinancialCrimeCaseDecision> DecideCaseAsync(FinancialCrimeCaseDecision decision, long expectedCaseVersion, CancellationToken cancellationToken);
    ValueTask ConsumeDecisionAsync(Guid tenantId, Guid decisionId, string operationFingerprint, DateTimeOffset consumedAt, CancellationToken cancellationToken);
    ValueTask RecordRegulatoryReferenceAsync(Guid tenantId, Guid caseId, string kind, string jurisdictionCode, string referenceHash, Guid actorId, DateTimeOffset recordedAt, CancellationToken cancellationToken);
    ValueTask<IReadOnlyList<FinancialCrimeScreening>> ReadDueRescreeningsAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);
    ValueTask<IReadOnlyList<FinancialCrimeCase>> ReadCasesAsync(Guid tenantId, FinancialCrimeCaseState? state, int limit, CancellationToken cancellationToken);
    ValueTask<FinancialCrimeCaseDetails> ReadCaseDetailsAsync(Guid tenantId, Guid caseId, CancellationToken cancellationToken);
}

public sealed class PostgreSqlFinancialCrimeControlPlane : IFinancialCrimeControlPlane
{
    private readonly DbContext _db;
    private readonly IComplianceEvidenceStore _evidence;
    private readonly IComplianceEvidenceReader _evidenceReader;
    private readonly IComplianceHoldStore _holds;

    public PostgreSqlFinancialCrimeControlPlane(
        IApplicationDbContext context,
        IComplianceEvidenceStore evidence,
        IComplianceEvidenceReader evidenceReader,
        IComplianceHoldStore holds)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext ?? throw new InvalidOperationException("Financial Crime requires the relational application DbContext.");
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        _evidenceReader = evidenceReader ?? throw new ArgumentNullException(nameof(evidenceReader));
        _holds = holds ?? throw new ArgumentNullException(nameof(holds));
    }

    public async ValueTask<FinancialCrimeScreeningResult> IngestScreeningAsync(
        FinancialCrimeScreening screening,
        CancellationToken cancellationToken)
    {
        ValidateScreening(screening);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var replay = await _db.Set<FinancialCrimeScreeningRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Provider == screening.Provider && row.Environment == screening.Environment &&
                                         row.ProviderEventId == screening.ProviderEventId, cancellationToken);
        if (replay is not null)
        {
            if (replay.PayloadHash != screening.PayloadHash)
                throw new FinancialCrimeConflictException("The screening event was replayed with different evidence.");
            var replayCase = await FindCaseForScreeningAsync(replay, cancellationToken);
            return new FinancialCrimeScreeningResult(Map(replay), replayCase is null ? null : Map(replayCase));
        }
        var latestVersion = await _db.Set<FinancialCrimeScreeningRow>()
            .Where(row => row.TenantId == screening.TenantId && row.SubjectHash == screening.SubjectHash)
            .Select(row => (long?)row.Version).MaxAsync(cancellationToken) ?? 0;
        if (screening.Version != latestVersion + 1)
            throw new FinancialCrimeConflictException("Screening versions must be contiguous.");

        var screeningRow = ToRow(screening);
        _db.Set<FinancialCrimeScreeningRow>().Add(screeningRow);
        FinancialCrimeCaseRow? caseRow = null;
        if (screening.Outcome != FinancialCrimeOutcome.Approved)
            caseRow = await OpenCaseAndHoldAsync(
                screening.TenantId, screening.SubjectHash, screening.Outcome.ToString(), screening.EvidenceHash,
                screening.ReceivedAt, screening.ExpiresAt, $"screening:{screening.ProviderEventId}", cancellationToken);
        screeningRow.CaseId = caseRow?.Id;
        var ingestion = await _evidence.IngestAsync(ToEvidence(screening), cancellationToken);
        if (ingestion.Status is not (ComplianceEvidenceIngestionStatus.Published or ComplianceEvidenceIngestionStatus.Duplicate))
            throw new FinancialCrimeConflictException("Screening evidence could not be published in sequence.");
        await _db.SaveChangesAsync(cancellationToken);
        return new FinancialCrimeScreeningResult(screening, caseRow is null ? null : Map(caseRow));
        }, cancellationToken);
    }

    public async ValueTask<FinancialCrimeCase> RecordSignalAsync(
        FinancialCrimeTransactionSignal signal,
        CancellationToken cancellationToken)
    {
        ValidateSignal(signal);
        var requestHash = Hash(signal.IdempotencyKey.Trim());
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var replay = await _db.Set<FinancialCrimeTransactionSignalRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.RequestHash == requestHash, cancellationToken);
        if (replay is not null)
        {
            if (replay.OperationFingerprint != signal.OperationFingerprint || replay.EvidenceHash != signal.EvidenceHash)
                throw new FinancialCrimeConflictException("The signal idempotency key was replayed with different inputs.");
            var existingCase = await _db.Set<FinancialCrimeCaseRow>().AsNoTracking()
                .SingleAsync(row => row.Id == replay.CaseId && row.TenantId == replay.TenantId, cancellationToken);
            return Map(existingCase);
        }
        var signalRow = new FinancialCrimeTransactionSignalRow
        {
            Id = signal.Id, TenantId = signal.TenantId, SubjectHash = signal.SubjectHash,
            OperationFingerprint = signal.OperationFingerprint, SignalType = signal.SignalType,
            Score = signal.Score, EvidenceHash = signal.EvidenceHash, RequestHash = requestHash,
            ObservedAt = signal.ObservedAt
        };
        var caseRow = await OpenCaseAndHoldAsync(
            signal.TenantId, signal.SubjectHash, signal.SignalType, signal.EvidenceHash,
            signal.ObservedAt, signal.HoldExpiresAt, $"signal:{signal.IdempotencyKey}", cancellationToken);
        signalRow.CaseId = caseRow.Id;
        _db.Set<FinancialCrimeTransactionSignalRow>().Add(signalRow);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(caseRow);
        }, cancellationToken);
    }

    public async ValueTask<FinancialCrimeCase> AssignCaseAsync(
        Guid tenantId, Guid caseId, Guid actorId, long expectedVersion, DateTimeOffset assignedAt,
        CancellationToken cancellationToken)
    {
        ValidateTenantActor(tenantId, actorId);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var row = await ReadCaseAsync(tenantId, caseId, cancellationToken);
        if (row.State == FinancialCrimeCaseState.Closed || row.Version != expectedVersion)
            throw new FinancialCrimeConflictException("The case cannot be assigned at the supplied version.");
        row.AssignedTo = actorId;
        row.State = FinancialCrimeCaseState.Assigned;
        row.Version++;
        row.UpdatedAt = assignedAt;
        AppendCaseEvent(row, "Assigned", actorId, "manual-assignment", Hash(actorId.ToString("N")), assignedAt);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
        }, cancellationToken);
    }

    public async ValueTask<FinancialCrimeCaseDecision> DecideCaseAsync(
        FinancialCrimeCaseDecision decision,
        long expectedCaseVersion,
        CancellationToken cancellationToken)
    {
        ValidateDecision(decision);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var caseRow = await ReadCaseAsync(decision.TenantId, decision.CaseId, cancellationToken);
        if (caseRow.State == FinancialCrimeCaseState.Closed || caseRow.Version != expectedCaseVersion || caseRow.AssignedTo != decision.DecidedBy)
            throw new FinancialCrimeConflictException("The case is not assigned to this reviewer at the supplied version.");
        var latestVersion = await _db.Set<FinancialCrimeDecisionRow>().Where(row => row.CaseId == decision.CaseId)
            .Select(row => (long?)row.Version).MaxAsync(cancellationToken) ?? 0;
        if (decision.Version != latestVersion + 1)
            throw new FinancialCrimeConflictException("Decision versions must be contiguous.");
        _db.Set<FinancialCrimeDecisionRow>().Add(ToRow(decision));
        caseRow.Version++;
        caseRow.UpdatedAt = decision.IssuedAt;
        if (decision.Outcome == FinancialCrimeOutcome.Approved)
        {
            caseRow.State = FinancialCrimeCaseState.Closed;
            caseRow.ClosedAt = decision.IssuedAt;
            await _holds.ReleaseAsync(caseRow.HoldId, decision.DecidedBy, decision.EvidenceHash, decision.IssuedAt, cancellationToken);
        }
        else
        {
            caseRow.State = FinancialCrimeCaseState.NeedsReview;
        }
        AppendCaseEvent(caseRow, "Decision", decision.DecidedBy, decision.ReasonCode, decision.EvidenceHash, decision.IssuedAt);
        var latestEvidence = await _evidenceReader.ReadLatestAsync(
            decision.TenantId, decision.SubjectHash, ComplianceEvidenceKinds.FinancialCrime, cancellationToken);
        var evidenceVersion = (latestEvidence?.Version ?? 0) + 1;
        var envelope = ComplianceEvidenceEnvelope.Create(
            "internal-financial-crime", "production", $"case-decision:{decision.Id:N}", decision.TenantId,
            decision.SubjectHash, evidenceVersion, Map(decision.Outcome), decision.IssuedAt, decision.ExpiresAt,
            decision.PolicyVersion, decision.EvidenceHash, true, decision.RawObjectReference, decision.IssuedAt) with
        { EvidenceKind = ComplianceEvidenceKinds.FinancialCrime };
        var ingestion = await _evidence.IngestAsync(envelope, cancellationToken);
        if (ingestion.Status is not (ComplianceEvidenceIngestionStatus.Published or ComplianceEvidenceIngestionStatus.Duplicate))
            throw new FinancialCrimeConflictException("Case decision evidence could not be published in sequence.");
        await _db.SaveChangesAsync(cancellationToken);
        return decision;
        }, cancellationToken);
    }

    public async ValueTask ConsumeDecisionAsync(
        Guid tenantId, Guid decisionId, string operationFingerprint, DateTimeOffset consumedAt,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(operationFingerprint);
        await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var decision = await _db.Set<FinancialCrimeDecisionRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == decisionId && row.TenantId == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Financial Crime decision was not found.");
        if (decision.Outcome != FinancialCrimeOutcome.Approved || consumedAt < decision.IssuedAt || consumedAt >= decision.ExpiresAt ||
            await _db.Set<FinancialCrimeDecisionConsumptionRow>().AnyAsync(row => row.DecisionId == decisionId, cancellationToken))
            throw new FinancialCrimeConflictException("The decision is not consumable.");
        _db.Set<FinancialCrimeDecisionConsumptionRow>().Add(new FinancialCrimeDecisionConsumptionRow
        {
            Id = Guid.NewGuid(), DecisionId = decisionId, TenantId = tenantId,
            OperationFingerprint = operationFingerprint.Trim(), ConsumedAt = consumedAt
        });
        await _db.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }

    public async ValueTask RecordRegulatoryReferenceAsync(
        Guid tenantId, Guid caseId, string kind, string jurisdictionCode, string referenceHash,
        Guid actorId, DateTimeOffset recordedAt, CancellationToken cancellationToken)
    {
        ValidateTenantActor(tenantId, actorId);
        if (kind is not ("SAR" or "STR")) throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentException.ThrowIfNullOrWhiteSpace(jurisdictionCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceHash);
        _ = await ReadCaseAsync(tenantId, caseId, cancellationToken);
        _db.Set<FinancialCrimeRegulatoryReferenceRow>().Add(new FinancialCrimeRegulatoryReferenceRow
        {
            Id = Guid.NewGuid(), CaseId = caseId, Kind = kind, JurisdictionCode = jurisdictionCode.Trim().ToUpperInvariant(),
            ReferenceHash = referenceHash.Trim(), RecordedBy = actorId, RecordedAt = recordedAt
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<FinancialCrimeScreening>> ReadDueRescreeningsAsync(
        DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(limit));
        var rows = await _db.Set<FinancialCrimeScreeningRow>().AsNoTracking()
            .Where(row => row.NextScreenAt <= now && !_db.Set<FinancialCrimeScreeningRow>().Any(
                newer => newer.TenantId == row.TenantId && newer.SubjectHash == row.SubjectHash && newer.Version > row.Version))
            .OrderBy(row => row.NextScreenAt).ThenBy(row => row.Id).Take(limit).ToArrayAsync(cancellationToken);
        return rows.Select(Map).ToArray();
    }

    public async ValueTask<IReadOnlyList<FinancialCrimeCase>> ReadCasesAsync(
        Guid tenantId,
        FinancialCrimeCaseState? state,
        int limit,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        if (state is not null && !Enum.IsDefined(state.Value)) throw new ArgumentOutOfRangeException(nameof(state));
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        var query = _db.Set<FinancialCrimeCaseRow>().AsNoTracking()
            .Where(row => row.TenantId == tenantId);
        if (state is not null) query = query.Where(row => row.State == state.Value);
        var rows = await query.OrderByDescending(row => row.UpdatedAt).ThenBy(row => row.Id)
            .Take(limit).ToArrayAsync(cancellationToken);
        return Array.AsReadOnly(rows.Select(Map).ToArray());
    }

    public async ValueTask<FinancialCrimeCaseDetails> ReadCaseDetailsAsync(
        Guid tenantId,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        if (caseId == Guid.Empty) throw new ArgumentException("Case ID is required.", nameof(caseId));
        var caseRow = await _db.Set<FinancialCrimeCaseRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == caseId && row.TenantId == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Financial Crime case was not found.");
        var events = await _db.Set<FinancialCrimeCaseEventRow>().AsNoTracking()
            .Where(row => row.CaseId == caseId)
            .OrderBy(row => row.Sequence)
            .Select(row => new FinancialCrimeCaseEvent(
                row.Id, row.CaseId, row.Sequence, row.Kind, row.ActorId,
                row.ReasonCode, row.EvidenceHash, row.OccurredAt))
            .ToArrayAsync(cancellationToken);
        var decisionRows = await _db.Set<FinancialCrimeDecisionRow>().AsNoTracking()
            .Where(row => row.CaseId == caseId && row.TenantId == tenantId)
            .OrderBy(row => row.Version)
            .ToArrayAsync(cancellationToken);
        var references = await _db.Set<FinancialCrimeRegulatoryReferenceRow>().AsNoTracking()
            .Where(row => row.CaseId == caseId)
            .OrderBy(row => row.RecordedAt)
            .Select(row => new FinancialCrimeRegulatoryReference(
                row.Id, row.CaseId, row.Kind, row.JurisdictionCode,
                row.ReferenceHash, row.RecordedBy, row.RecordedAt))
            .ToArrayAsync(cancellationToken);
        return new FinancialCrimeCaseDetails(
            Map(caseRow),
            Array.AsReadOnly(events),
            Array.AsReadOnly(decisionRows.Select(Map).ToArray()),
            Array.AsReadOnly(references));
    }

    private async ValueTask<FinancialCrimeCaseRow> OpenCaseAndHoldAsync(
        Guid tenantId, string subjectHash, string reasonCode, string evidenceHash,
        DateTimeOffset openedAt, DateTimeOffset expiresAt, string idempotencyKey, CancellationToken cancellationToken)
    {
        var active = await _db.Set<FinancialCrimeCaseRow>()
            .Where(row => row.TenantId == tenantId && row.SubjectHash == subjectHash && row.State != FinancialCrimeCaseState.Closed)
            .OrderByDescending(row => row.OpenedAt).FirstOrDefaultAsync(cancellationToken);
        if (active is not null) return active;
        var holdId = Guid.NewGuid();
        var row = new FinancialCrimeCaseRow
        {
            Id = Guid.NewGuid(), TenantId = tenantId, SubjectHash = subjectHash, State = FinancialCrimeCaseState.Open,
            HoldId = holdId, ReasonCode = reasonCode, Version = 1, OpenedAt = openedAt, UpdatedAt = openedAt
        };
        _db.Set<FinancialCrimeCaseRow>().Add(row);
        AppendCaseEvent(row, "Opened", null, reasonCode, evidenceHash, openedAt);
        await _holds.ActivateAsync(new ComplianceHoldActivation(
            holdId, new ComplianceHoldScope(tenantId, subjectHash, null), Hash(row.Id.ToString("N")),
            reasonCode, evidenceHash, idempotencyKey, Guid.Parse("00000000-0000-0000-0000-000000000001"),
            openedAt, expiresAt), cancellationToken);
        return row;
    }

    private async ValueTask<FinancialCrimeCaseRow> ReadCaseAsync(Guid tenantId, Guid caseId, CancellationToken cancellationToken) =>
        await _db.Set<FinancialCrimeCaseRow>().SingleOrDefaultAsync(row => row.Id == caseId && row.TenantId == tenantId, cancellationToken)
        ?? throw new KeyNotFoundException("Financial Crime case was not found.");

    private async ValueTask<FinancialCrimeCaseRow?> FindCaseForScreeningAsync(FinancialCrimeScreeningRow screening, CancellationToken cancellationToken) =>
        screening.CaseId is null
            ? null
            : await _db.Set<FinancialCrimeCaseRow>().AsNoTracking()
                .SingleAsync(row => row.Id == screening.CaseId && row.TenantId == screening.TenantId, cancellationToken);

    private void AppendCaseEvent(FinancialCrimeCaseRow row, string kind, Guid? actorId, string reasonCode, string evidenceHash, DateTimeOffset occurredAt)
    {
        _db.Set<FinancialCrimeCaseEventRow>().Add(new FinancialCrimeCaseEventRow
        {
            Id = Guid.NewGuid(), CaseId = row.Id, Sequence = checked((int)row.Version), Kind = kind,
            ActorId = actorId, ReasonCode = reasonCode, EvidenceHash = evidenceHash, OccurredAt = occurredAt
        });
    }

    private static ComplianceEvidenceEnvelope ToEvidence(FinancialCrimeScreening screening) =>
        ComplianceEvidenceEnvelope.Create(
            screening.Provider, screening.Environment, screening.ProviderEventId, screening.TenantId,
            screening.SubjectHash, screening.Version, Map(screening.Outcome), screening.IssuedAt,
            screening.ExpiresAt, screening.PolicyVersion, screening.PayloadHash, screening.SignatureVerified,
            screening.RawObjectReference, screening.ReceivedAt) with
        { EvidenceKind = ComplianceEvidenceKinds.FinancialCrime };

    private static ComplianceEvidenceResult Map(FinancialCrimeOutcome outcome) => outcome switch
    {
        FinancialCrimeOutcome.Approved => ComplianceEvidenceResult.Approved,
        FinancialCrimeOutcome.Rejected => ComplianceEvidenceResult.Rejected,
        FinancialCrimeOutcome.NeedsReview => ComplianceEvidenceResult.NeedsReview,
        _ => ComplianceEvidenceResult.Unavailable
    };

    private static FinancialCrimeScreeningRow ToRow(FinancialCrimeScreening value) => new()
    {
        Id = value.Id, Provider = value.Provider, Environment = value.Environment, ProviderEventId = value.ProviderEventId,
        TenantId = value.TenantId, SubjectHash = value.SubjectHash, Version = value.Version, Outcome = value.Outcome,
        SanctionsMatch = value.SanctionsMatch, PepMatch = value.PepMatch, AdverseMediaMatch = value.AdverseMediaMatch,
        PolicyVersion = value.PolicyVersion, PayloadHash = value.PayloadHash, EvidenceHash = value.EvidenceHash,
        RawObjectReference = value.RawObjectReference, SignatureVerified = value.SignatureVerified,
        IssuedAt = value.IssuedAt, ExpiresAt = value.ExpiresAt, NextScreenAt = value.NextScreenAt, ReceivedAt = value.ReceivedAt
    };

    private static FinancialCrimeDecisionRow ToRow(FinancialCrimeCaseDecision value) => new()
    {
        Id = value.Id, CaseId = value.CaseId, TenantId = value.TenantId, SubjectHash = value.SubjectHash,
        Version = value.Version, Outcome = value.Outcome, PolicyVersion = value.PolicyVersion,
        ReasonCode = value.ReasonCode, EvidenceHash = value.EvidenceHash, RawObjectReference = value.RawObjectReference,
        DecidedBy = value.DecidedBy, IssuedAt = value.IssuedAt, ExpiresAt = value.ExpiresAt
    };

    private static FinancialCrimeScreening Map(FinancialCrimeScreeningRow value) => new(
        value.Id, value.Provider, value.Environment, value.ProviderEventId, value.TenantId, value.SubjectHash,
        value.Version, value.Outcome, value.SanctionsMatch, value.PepMatch, value.AdverseMediaMatch,
        value.PolicyVersion, value.PayloadHash, value.EvidenceHash, value.RawObjectReference,
        value.SignatureVerified, value.IssuedAt, value.ExpiresAt, value.NextScreenAt, value.ReceivedAt);

    private static FinancialCrimeCase Map(FinancialCrimeCaseRow value) => new(
        value.Id, value.TenantId, value.SubjectHash, value.State, value.AssignedTo, value.HoldId,
        value.ReasonCode, value.Version, value.OpenedAt, value.UpdatedAt, value.ClosedAt);

    private static FinancialCrimeCaseDecision Map(FinancialCrimeDecisionRow value) => new(
        value.Id, value.CaseId, value.TenantId, value.SubjectHash, value.Version,
        value.Outcome, value.PolicyVersion, value.ReasonCode, value.EvidenceHash,
        value.RawObjectReference, value.DecidedBy, value.IssuedAt, value.ExpiresAt);

    private static void ValidateScreening(FinancialCrimeScreening value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Id == Guid.Empty || value.TenantId == Guid.Empty) throw new ArgumentException("Screening IDs are required.", nameof(value));
        ArgumentException.ThrowIfNullOrWhiteSpace(value.Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.Environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.ProviderEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.SubjectHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.PayloadHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.EvidenceHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.RawObjectReference);
        if (!value.SignatureVerified || value.Version <= 0 || value.PolicyVersion <= 0 ||
            value.ExpiresAt <= value.IssuedAt || value.NextScreenAt <= value.IssuedAt || value.ReceivedAt < value.IssuedAt)
            throw new ArgumentException("Screening evidence is invalid or not current.", nameof(value));
    }

    private static void ValidateSignal(FinancialCrimeTransactionSignal value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Id == Guid.Empty || value.TenantId == Guid.Empty) throw new ArgumentException("Signal IDs are required.", nameof(value));
        ArgumentException.ThrowIfNullOrWhiteSpace(value.SubjectHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.OperationFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.SignalType);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.EvidenceHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.IdempotencyKey);
        if (value.Score is < 0 or > 1_000_000 || value.HoldExpiresAt <= value.ObservedAt)
            throw new ArgumentException("Signal score or lifetime is invalid.", nameof(value));
    }

    private static void ValidateDecision(FinancialCrimeCaseDecision value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Id == Guid.Empty || value.CaseId == Guid.Empty || value.TenantId == Guid.Empty || value.DecidedBy == Guid.Empty)
            throw new ArgumentException("Decision IDs are required.", nameof(value));
        ArgumentException.ThrowIfNullOrWhiteSpace(value.SubjectHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.ReasonCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.EvidenceHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.RawObjectReference);
        if (value.Version <= 0 || value.PolicyVersion <= 0 || value.ExpiresAt <= value.IssuedAt)
            throw new ArgumentException("Decision version or lifetime is invalid.", nameof(value));
    }

    private static void ValidateTenantActor(Guid tenantId, Guid actorId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        if (actorId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorId));
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed class FinancialCrimeConflictException(string message) : InvalidOperationException(message);
