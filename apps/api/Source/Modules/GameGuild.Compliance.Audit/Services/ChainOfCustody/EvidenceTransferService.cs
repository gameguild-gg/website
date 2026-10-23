using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Compliance.Audit;

public sealed record InitiateTransferRequest
{
    public Guid EvidenceId { get; init; }
    public Guid ToCustodianId { get; init; }
    public string? Purpose { get; init; }
}

public sealed record AcknowledgeTransferRequest
{
    public string? Notes { get; init; }
}

public sealed record TransferProofDocument(
    Guid TransferId,
    Guid EvidenceId,
    string EvidenceHash,
    Guid FromCustodianId,
    Guid ToCustodianId,
    string ChallengeNonce,
    DateTime InitiatedAtUtc,
    string PayloadSha256);

public sealed record EvidenceTransferResponse(
    Guid Id,
    Guid EvidenceId,
    Guid FromCustodianId,
    Guid ToCustodianId,
    string State,
    DateTime InitiatedAtUtc,
    DateTime? AcknowledgedAtUtc,
    Guid? AcknowledgedByUserId,
    string EvidenceHash,
    string ProofJson,
    string Signature,
    string SigningKeyId,
    bool ProofVerified,
    string? Purpose);

/// <summary>
///     Digital evidence transfer protocol (issue #177): a transfer is initiated by the current
///     custodian, binds the evidence hash and both custodians into a challenge-nonce proof
///     document signed with ECDSA, and completes only when the receiving custodian acknowledges
///     with a counter-signature. The proof is the cryptographic record of proper handoff.
/// </summary>
public interface IEvidenceTransferService
{
    Task<EvidenceTransferResponse> InitiateAsync(InitiateTransferRequest request, CancellationToken cancellationToken);

    Task<EvidenceTransferResponse> AcknowledgeAsync(Guid transferId, AcknowledgeTransferRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<EvidenceTransferResponse>> ListAsync(Guid? evidenceId, int skip, int take, CancellationToken cancellationToken);

    /// <summary>Recomputes the proof hash and verifies the ECDSA signature.</summary>
    Task<bool> VerifyProofAsync(Guid transferId, CancellationToken cancellationToken);
}

public sealed class EvidenceTransferService(
    IActorContextAccessor actors,
    IApplicationDbContext context,
    IEvidenceCustodianService custodians,
    ICryptographicSigningService signing,
    TimeProvider timeProvider,
    ILogger<EvidenceTransferService> logger) : IEvidenceTransferService
{
    private static readonly JsonSerializerOptions ProofJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<EvidenceTransferResponse> InitiateAsync(InitiateTransferRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = RequireAdministrator();
        await custodians.RequireScopeAsync(userId, CustodianScopes.Transfer, cancellationToken).ConfigureAwait(false);

        var evidence = await context.Set<CustodyEvidenceItem>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == request.EvidenceId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new CustodyUnauthorizedException("Evidence not found.");

        if (evidence.CustodyState == "Released")
        {
            throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["EvidenceId"] = ["Released evidence cannot be transferred."]
            });
        }

        if (evidence.CurrentCustodianId != userId)
        {
            throw new CustodyUnauthorizedException("Only the current custodian may initiate a transfer.");
        }

        var toCustodian = await custodians.RequireScopeAsync(request.ToCustodianId, CustodianScopes.Acknowledge, cancellationToken)
            .ConfigureAwait(false);
        if (toCustodian.UserId == userId)
        {
            throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["ToCustodianId"] = ["The receiving custodian must differ from the transferring custodian."]
            });
        }

        var pending = await context.Set<EvidenceTransfer>().AsNoTracking()
            .AnyAsync(transfer => transfer.TenantId == tenantId
                && transfer.EvidenceId == evidence.Id
                && transfer.State == "PendingAcknowledgment", cancellationToken)
            .ConfigureAwait(false);
        if (pending)
        {
            throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["EvidenceId"] = ["Another transfer is pending acknowledgment for this evidence."]
            });
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var transfer = new EvidenceTransfer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EvidenceId = evidence.Id,
            FromCustodianId = evidence.CurrentCustodianId,
            ToCustodianId = request.ToCustodianId,
            InitiatedByUserId = userId,
            InitiatedAtUtc = now,
            ChallengeNonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)),
            EvidenceHash = evidence.ContentSha256,
            ProofJson = string.Empty,
            Signature = string.Empty,
            SigningKeyId = signing.GetActiveKeyId(),
            Purpose = request.Purpose
        };

        var payloadSha256 = BuildProofPayload(transfer, out var canonical);
        transfer.ProofJson = JsonSerializer.Serialize(
            new TransferProofDocument(
                transfer.Id, transfer.EvidenceId, transfer.EvidenceHash, transfer.FromCustodianId,
                transfer.ToCustodianId, transfer.ChallengeNonce, transfer.InitiatedAtUtc, payloadSha256),
            ProofJsonOptions);
        transfer.Signature = signing.SignData(canonical, transfer.SigningKeyId);

        context.Set<EvidenceTransfer>().Add(transfer);
        evidence.CustodyState = "TransferPending";
        evidence.Touch();
        await AppendTransferEventAsync(tenantId, evidence, "TransferInitiated", transfer, userId, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(transfer, await VerifySignature(transfer, canonical).ConfigureAwait(false));
    }

    public async Task<EvidenceTransferResponse> AcknowledgeAsync(
        Guid transferId,
        AcknowledgeTransferRequest request,
        CancellationToken cancellationToken)
    {
        var (tenantId, userId) = RequireAdministrator();
        await custodians.RequireScopeAsync(userId, CustodianScopes.Acknowledge, cancellationToken).ConfigureAwait(false);

        var transfer = await context.Set<EvidenceTransfer>()
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.Id == transferId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new CustodyUnauthorizedException("Transfer not found.");

        if (transfer.State != "PendingAcknowledgment")
        {
            throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["TransferId"] = [$"This transfer is already {transfer.State.ToLowerInvariant()}."]
            });
        }

        if (transfer.ToCustodianId != userId)
        {
            throw new CustodyUnauthorizedException("Only the designated receiving custodian may acknowledge this transfer.");
        }

        var evidence = await context.Set<CustodyEvidenceItem>()
            .FirstAsync(item => item.TenantId == tenantId && item.Id == transfer.EvidenceId, cancellationToken)
            .ConfigureAwait(false);

        // Verification and acknowledgment are inseparable: the receiving custodian confirms the
        // evidence hash still matches the proof before taking custody.
        if (evidence.ContentSha256 != transfer.EvidenceHash)
        {
            throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["EvidenceId"] = ["Evidence content hash no longer matches the transfer proof; refusing acknowledgment."]
            });
        }

        _ = BuildProofPayload(transfer, out var canonical);
        if (!await VerifySignature(transfer, canonical).ConfigureAwait(false))
        {
            throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["TransferId"] = ["Transfer proof signature verification failed; refusing acknowledgment."]
            });
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        transfer.State = "Acknowledged";
        transfer.AcknowledgedAtUtc = now;
        transfer.AcknowledgedByUserId = userId;
        transfer.AcknowledgmentSignature = signing.SignData(
            JsonSerializer.Serialize(new { transferId = transfer.Id, acknowledgedByUserId = userId, acknowledgedAtUtc = now, notes = request?.Notes }, ProofJsonOptions),
            transfer.SigningKeyId);
        transfer.Touch();

        evidence.CustodyState = "InCustody";
        evidence.CurrentCustodianId = transfer.ToCustodianId;
        evidence.Touch();
        await AppendTransferEventAsync(tenantId, evidence, "TransferAcknowledged", transfer, userId, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(transfer, true);
    }

    public async Task<IReadOnlyList<EvidenceTransferResponse>> ListAsync(
        Guid? evidenceId, int skip, int take, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = RequireAdministrator();
        await custodians.RequireScopeAsync(userId, CustodianScopes.Access, cancellationToken).ConfigureAwait(false);

        var query = context.Set<EvidenceTransfer>().AsNoTracking()
            .Where(transfer => transfer.TenantId == tenantId);
        if (evidenceId is { } id)
        {
            query = query.Where(transfer => transfer.EvidenceId == id);
        }

        var transfers = await query
            .OrderByDescending(transfer => transfer.InitiatedAtUtc)
            .ThenBy(transfer => transfer.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var results = new List<EvidenceTransferResponse>();
        foreach (var transfer in transfers)
        {
            _ = BuildProofPayload(transfer, out var canonical);
            results.Add(Map(transfer, await VerifySignature(transfer, canonical).ConfigureAwait(false)));
        }

        return results;
    }

    public async Task<bool> VerifyProofAsync(Guid transferId, CancellationToken cancellationToken)
    {
        var tenantId = RequireAdministrator().TenantId;
        var transfer = await context.Set<EvidenceTransfer>().AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.Id == transferId, cancellationToken)
            .ConfigureAwait(false);
        if (transfer is null)
        {
            return false;
        }

        _ = BuildProofPayload(transfer, out var canonical);
        return await VerifySignature(transfer, canonical).ConfigureAwait(false);
    }

    /// <summary>Rebuilds the canonical proof payload and its SHA-256 exactly as at initiation.</summary>
    internal static string BuildProofPayload(EvidenceTransfer transfer, out string canonical)
    {
        canonical = JsonSerializer.Serialize(new
        {
            transferId = transfer.Id,
            evidenceId = transfer.EvidenceId,
            evidenceHash = transfer.EvidenceHash,
            fromCustodianId = transfer.FromCustodianId,
            toCustodianId = transfer.ToCustodianId,
            challengeNonce = transfer.ChallengeNonce,
            initiatedAtUtc = transfer.InitiatedAtUtc
        }, ProofJsonOptions);
        return ValueSnapshotDiffEngine.ComputeSha256(canonical);
    }

    private async Task<bool> VerifySignature(EvidenceTransfer transfer, string canonical)
    {
        try
        {
            return await Task.FromResult(signing.VerifySignature(canonical, transfer.Signature, transfer.SigningKeyId)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Transfer proof signature verification errored for transfer {TransferId}", transfer.Id);
            return false;
        }
    }

    private async Task AppendTransferEventAsync(
        Guid tenantId, CustodyEvidenceItem evidence, string eventType, EvidenceTransfer transfer, Guid userId, CancellationToken cancellationToken)
    {
        var lastSequence = await context.Set<CustodyEvent>().AsNoTracking()
            .Where(custodyEvent => custodyEvent.TenantId == tenantId && custodyEvent.EvidenceId == evidence.Id)
            .MaxAsync(custodyEvent => (int?)custodyEvent.SequenceNumber, cancellationToken)
            .ConfigureAwait(false) ?? 0;

        context.Set<CustodyEvent>().Add(new CustodyEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EvidenceId = evidence.Id,
            SequenceNumber = lastSequence + 1,
            EventType = eventType,
            FromCustodianId = transfer.FromCustodianId,
            ToCustodianId = transfer.ToCustodianId,
            ActorUserId = userId,
            Purpose = transfer.Purpose,
            TransferId = transfer.Id,
            ProofJson = transfer.ProofJson,
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime
        });
    }

    private static EvidenceTransferResponse Map(EvidenceTransfer transfer, bool proofVerified) =>
        new(transfer.Id, transfer.EvidenceId, transfer.FromCustodianId, transfer.ToCustodianId, transfer.State,
            transfer.InitiatedAtUtc, transfer.AcknowledgedAtUtc, transfer.AcknowledgedByUserId, transfer.EvidenceHash,
            transfer.ProofJson, transfer.Signature, transfer.SigningKeyId, proofVerified, transfer.Purpose);

    private (Guid TenantId, Guid UserId) RequireAdministrator()
    {
        var actor = actors.ActorContext;
        if (!actor.IsAuthenticated || !actor.IsTenantAdmin || actor.TenantId is not { } tenant || tenant == Guid.Empty
            || actor.SubjectIdAsGuid is not { } user || user == Guid.Empty)
        {
            logger.LogWarning("Evidence transfer access denied for actor {Actor}", actor.SubjectIdAsGuid);
            throw new UnauthorizedAccessException("A tenant administrator and tenant context are required.");
        }

        return (tenant, user);
    }
}
