using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GameGuild.Finance.Economy.Risk;

public enum EconomyCapabilityReadinessStatus
{
    Disabled = 1,
    Ready = 2,
    InvalidPolicy = 3,
    JurisdictionBlocked = 4,
    ComplianceUnavailable = 5,
    ComplianceStale = 6,
    ReviewRequired = 7,
    LedgerUnhealthy = 8,
    ProjectionMismatch = 9,
    ReserveInsufficient = 10,
    CustodyUnreconciled = 11,
    AnchorInvalid = 12,
    ProviderNotReady = 13,
    KillSwitchActive = 14
}

public sealed record EconomyCapabilityEvaluationContext(
    Guid TenantId,
    Guid ActorId,
    string SubjectReference,
    string JurisdictionCode,
    EconomyValueMovementCapability Capability,
    Guid RiskDecisionId,
    string OperationFingerprint,
    string ProviderHash,
    string DestinationHash,
    IReadOnlyList<string> SourceRootHashes,
    DateTimeOffset EvaluatedAt);

public sealed record EconomyCapabilityControlPlaneSnapshot(
    bool HasActivePolicy,
    bool PolicySignatureValid,
    long PolicyVersion,
    DateTimeOffset PolicyExpiresAt,
    bool JurisdictionAllowed,
    bool ComplianceAvailable,
    DateTimeOffset ComplianceExpiresAt,
    bool ManualReviewRequired,
    Guid RiskDecisionId,
    bool LedgerHealthy,
    bool ProjectionMatches,
    bool ReserveSufficient,
    long ReserveVersion,
    DateTimeOffset ReserveExpiresAt,
    bool CustodyReconciled,
    bool AnchorValid,
    DateTimeOffset AnchorExpiresAt,
    bool ProviderReady,
    bool KillSwitchActive,
    long KillSwitchEpoch)
{
    public IReadOnlyList<string> EvidenceHashes { get; init; } = [];
}

public sealed record CapabilityReceiptSignature(string KeyId, string Signature);

public sealed record CapabilityAuthorizationReceipt(
    Guid Id,
    Guid TenantId,
    Guid ActorId,
    string SubjectReference,
    string JurisdictionCode,
    EconomyValueMovementCapability Capability,
    string OperationFingerprint,
    long PolicyVersion,
    long ReserveVersion,
    Guid RiskDecisionId,
    long KillSwitchEpoch,
    string ProviderHash,
    string DestinationHash,
    IReadOnlyList<string> SourceRootHashes,
    IReadOnlyList<string> EvidenceHashes,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    string ReceiptHash,
    string KeyId,
    string Signature);

public sealed record EconomyCapabilityEvaluationResult(
    EconomyCapabilityReadinessStatus State,
    IReadOnlyList<string> Diagnostics,
    CapabilityAuthorizationReceipt? Receipt)
{
    public bool IsReady => State == EconomyCapabilityReadinessStatus.Ready;
}

public interface IEconomyCapabilityEvaluator
{
    ValueTask<EconomyCapabilityEvaluationResult> EvaluateAsync(
        EconomyCapabilityEvaluationContext context,
        CancellationToken cancellationToken);
}

public interface IEconomyCapabilityReadinessInspector
{
    ValueTask<EconomyCapabilityEvaluationResult> InspectAsync(
        EconomyCapabilityEvaluationContext context,
        CancellationToken cancellationToken);
}

public interface IEconomyCapabilityAuthorizationService
{
    ValueTask<CapabilityAuthorizationReceipt> AuthorizeAndConsumeAsync(
        EconomyCapabilityEvaluationContext context,
        CancellationToken cancellationToken);
}

public sealed class EconomyCapabilityAuthorizationException : InvalidOperationException
{
    public EconomyCapabilityAuthorizationException(
        EconomyCapabilityReadinessStatus state,
        IReadOnlyList<string> diagnostics)
        : base(diagnostics.Count == 0
            ? $"Economy capability authorization failed with state {state}."
            : diagnostics[0])
    {
        State = state;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public EconomyCapabilityReadinessStatus State { get; }
    public IReadOnlyList<string> Diagnostics { get; }
}

public sealed class EconomyCapabilityAuthorizationService(
    IEconomyCapabilityEvaluator evaluator,
    IEconomyCapabilityControlPlaneStore store) : IEconomyCapabilityAuthorizationService
{
    public async ValueTask<CapabilityAuthorizationReceipt> AuthorizeAndConsumeAsync(
        EconomyCapabilityEvaluationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evaluation = await evaluator.EvaluateAsync(context, cancellationToken);
        if (!evaluation.IsReady || evaluation.Receipt is null)
            throw new EconomyCapabilityAuthorizationException(evaluation.State, evaluation.Diagnostics);

        var receipt = evaluation.Receipt;
        await store.ConsumeAsync(
            receipt.Id,
            receipt.OperationFingerprint,
            receipt.TenantId,
            receipt.ActorId,
            receipt.KillSwitchEpoch,
            context.EvaluatedAt,
            cancellationToken);
        return receipt;
    }
}

public interface IEconomyCapabilityControlPlaneStore
{
    ValueTask<EconomyCapabilityControlPlaneSnapshot> ReadSnapshotAsync(
        EconomyCapabilityEvaluationContext context,
        CancellationToken cancellationToken);

    ValueTask PersistReceiptAsync(
        CapabilityAuthorizationReceipt receipt,
        CancellationToken cancellationToken);

    ValueTask ConsumeAsync(
        Guid receiptId,
        string operationFingerprint,
        Guid tenantId,
        Guid actorId,
        long currentKillSwitchEpoch,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken);
}

public interface ICapabilityReceiptSigner
{
    ValueTask<CapabilityReceiptSignature> SignAsync(
        string canonicalPayload,
        CancellationToken cancellationToken);
}

public sealed class EconomyCapabilityReadinessInspector : IEconomyCapabilityReadinessInspector
{
    private readonly IEconomyCapabilityControlPlaneStore _store;

    public EconomyCapabilityReadinessInspector(IEconomyCapabilityControlPlaneStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async ValueTask<EconomyCapabilityEvaluationResult> InspectAsync(
        EconomyCapabilityEvaluationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var snapshot = await _store.ReadSnapshotAsync(context, cancellationToken);
        var denial = EconomyCapabilityEvaluator.EvaluateDenial(snapshot, context.EvaluatedAt);
        if (denial is not null)
            return denial;

        if (snapshot.RiskDecisionId == Guid.Empty || snapshot.RiskDecisionId != context.RiskDecisionId)
            return new EconomyCapabilityEvaluationResult(
                EconomyCapabilityReadinessStatus.ReviewRequired,
                ["The readiness snapshot does not reference the requested durable risk decision."],
                null);

        return new EconomyCapabilityEvaluationResult(EconomyCapabilityReadinessStatus.Ready, [], null);
    }
}

public sealed class EconomyCapabilityEvaluator : IEconomyCapabilityEvaluator
{
    private static readonly TimeSpan MaximumReceiptLifetime = TimeSpan.FromMinutes(5);
    private readonly IEconomyCapabilityControlPlaneStore _store;
    private readonly ICapabilityReceiptSigner _signer;

    public EconomyCapabilityEvaluator(
        IEconomyCapabilityControlPlaneStore store,
        ICapabilityReceiptSigner signer)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(signer);
        _store = store;
        _signer = signer;
    }

    public async ValueTask<EconomyCapabilityEvaluationResult> EvaluateAsync(
        EconomyCapabilityEvaluationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var snapshot = await _store.ReadSnapshotAsync(context, cancellationToken);
        var denial = EvaluateDenial(snapshot, context.EvaluatedAt);
        if (denial is not null)
            return denial;

        if (snapshot.RiskDecisionId == Guid.Empty || snapshot.RiskDecisionId != context.RiskDecisionId)
            throw new InvalidOperationException(
                "A ready Economy capability snapshot must reference a durable risk decision.");

        var receiptId = Guid.NewGuid();
        var expiresAt = new[]
        {
            context.EvaluatedAt.Add(MaximumReceiptLifetime),
            snapshot.PolicyExpiresAt,
            snapshot.ComplianceExpiresAt,
            snapshot.ReserveExpiresAt,
            snapshot.AnchorExpiresAt
        }.Min();
        var sourceRootHashes = SnapshotArray(context.SourceRootHashes);
        var evidenceHashes = SnapshotArray(snapshot.EvidenceHashes);
        var unsignedReceipt = new CapabilityAuthorizationReceipt(
            receiptId,
            context.TenantId,
            context.ActorId,
            context.SubjectReference,
            context.JurisdictionCode,
            context.Capability,
            context.OperationFingerprint,
            snapshot.PolicyVersion,
            snapshot.ReserveVersion,
            snapshot.RiskDecisionId,
            snapshot.KillSwitchEpoch,
            context.ProviderHash,
            context.DestinationHash,
            sourceRootHashes,
            evidenceHashes,
            context.EvaluatedAt,
            expiresAt,
            string.Empty,
            string.Empty,
            string.Empty);
        var canonicalPayload = Canonicalize(unsignedReceipt);
        var receiptHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload))).ToLowerInvariant();
        var signature = await _signer.SignAsync(canonicalPayload, cancellationToken);
        var receipt = unsignedReceipt with
        {
            ReceiptHash = receiptHash,
            KeyId = signature.KeyId,
            Signature = signature.Signature
        };

        await _store.PersistReceiptAsync(receipt, cancellationToken);
        return new EconomyCapabilityEvaluationResult(EconomyCapabilityReadinessStatus.Ready, [], receipt);
    }

    internal static EconomyCapabilityEvaluationResult? EvaluateDenial(
        EconomyCapabilityControlPlaneSnapshot snapshot,
        DateTimeOffset now)
    {
        if (snapshot.KillSwitchActive)
            return Denied(EconomyCapabilityReadinessStatus.KillSwitchActive, "A kill switch is active.");
        if (!snapshot.HasActivePolicy)
            return Denied(EconomyCapabilityReadinessStatus.Disabled, "No active signed capability policy exists.");
        if (!snapshot.PolicySignatureValid || snapshot.PolicyExpiresAt <= now)
            return Denied(EconomyCapabilityReadinessStatus.InvalidPolicy, "The active capability policy is invalid or expired.");
        if (!snapshot.JurisdictionAllowed)
            return Denied(EconomyCapabilityReadinessStatus.JurisdictionBlocked, "The jurisdiction is not explicitly allowed.");
        if (!snapshot.ComplianceAvailable)
            return Denied(EconomyCapabilityReadinessStatus.ComplianceUnavailable, "Compliance evidence is unavailable.");
        if (snapshot.ComplianceExpiresAt <= now)
            return Denied(EconomyCapabilityReadinessStatus.ComplianceStale, "Compliance evidence is stale.");
        if (snapshot.ManualReviewRequired)
            return Denied(EconomyCapabilityReadinessStatus.ReviewRequired, "A manual review is required.");
        if (!snapshot.LedgerHealthy)
            return Denied(EconomyCapabilityReadinessStatus.LedgerUnhealthy, "The journal integrity state is unhealthy.");
        if (!snapshot.ProjectionMatches)
            return Denied(EconomyCapabilityReadinessStatus.ProjectionMismatch, "The active projection does not match the journal.");
        if (!snapshot.ReserveSufficient || snapshot.ReserveExpiresAt <= now)
            return Denied(EconomyCapabilityReadinessStatus.ReserveInsufficient, "The reserve snapshot is insufficient or stale.");
        if (!snapshot.CustodyReconciled)
            return Denied(EconomyCapabilityReadinessStatus.CustodyUnreconciled, "Custody observations are not reconciled.");
        if (!snapshot.AnchorValid || snapshot.AnchorExpiresAt <= now)
            return Denied(EconomyCapabilityReadinessStatus.AnchorInvalid, "The external journal anchor is invalid or stale.");
        if (!snapshot.ProviderReady)
            return Denied(EconomyCapabilityReadinessStatus.ProviderNotReady, "The required provider is not ready.");

        return null;
    }

    private static EconomyCapabilityEvaluationResult Denied(
        EconomyCapabilityReadinessStatus state,
        string diagnostic) => new(state, [diagnostic], null);

    private static IReadOnlyList<string> SnapshotArray(IReadOnlyList<string> values) =>
        Array.AsReadOnly(values.ToArray());

    internal static string Canonicalize(CapabilityAuthorizationReceipt receipt) => string.Join(
        '\n',
        receipt.Id.ToString("N"),
        receipt.TenantId.ToString("N"),
        receipt.ActorId.ToString("N"),
        receipt.SubjectReference,
        receipt.JurisdictionCode,
        ((int)receipt.Capability).ToString(CultureInfo.InvariantCulture),
        receipt.OperationFingerprint,
        receipt.PolicyVersion.ToString(CultureInfo.InvariantCulture),
        receipt.ReserveVersion.ToString(CultureInfo.InvariantCulture),
        receipt.RiskDecisionId.ToString("N"),
        receipt.KillSwitchEpoch.ToString(CultureInfo.InvariantCulture),
        receipt.ProviderHash,
        receipt.DestinationHash,
        string.Join(',', receipt.SourceRootHashes),
        string.Join(',', receipt.EvidenceHashes),
        receipt.IssuedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        receipt.ExpiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
}
