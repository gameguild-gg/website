using System.Text;
using System.Text.Json;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Compliance.Audit;

public sealed record BuildCustodyPackageRequest
{
    /// <summary>Evidence item to package (exactly one of EvidenceId / LegalHoldId).</summary>
    public Guid? EvidenceId { get; init; }

    /// <summary>Legal hold whose preserved evidence set should be packaged.</summary>
    public Guid? LegalHoldId { get; init; }

    public string? CertificationStatement { get; init; }
}

public sealed record CustodyPackageVerification(
    bool ContentHashVerified,
    bool ChainSequenceGapless,
    bool TransferProofsVerified,
    int TransferCount,
    int VerifiedTransferCount);

public sealed record CourtAdmissiblePackageResponse(
    Guid PackageId,
    Guid TenantId,
    string ManifestJson,
    string ManifestSha256,
    string Signature,
    string SigningKeyId,
    DateTime PreparedAtUtc,
    Guid PreparedByUserId,
    string? CertificationStatement,
    CustodyPackageVerification Verification,
    IReadOnlyList<Guid> EvidenceIds);

/// <summary>
///     Court-admissible evidence packaging (issue #177): assembles the complete custody
///     documentation for one evidence item or a whole legal hold — preserved content hashes, the
///     append-only custody timeline with transfer proofs, custodian authorization records, legal
///     hold references, an integrity verification summary, and an expert certification block —
///     into a signed manifest suitable for litigation and regulatory proceedings.
/// </summary>
public interface ICourtAdmissiblePackagingService
{
    Task<CourtAdmissiblePackageResponse> BuildAsync(BuildCustodyPackageRequest request, CancellationToken cancellationToken);
}

public sealed class CourtAdmissiblePackagingService(
    IActorContextAccessor actors,
    IApplicationDbContext context,
    IEvidenceCustodianService custodians,
    ICryptographicSigningService signing,
    TimeProvider timeProvider,
    ILogger<CourtAdmissiblePackagingService> logger) : ICourtAdmissiblePackagingService
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CourtAdmissiblePackageResponse> BuildAsync(BuildCustodyPackageRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = RequireAdministrator();
        await custodians.RequireScopeAsync(userId, CustodianScopes.Certify, cancellationToken).ConfigureAwait(false);

        var evidenceItems = await ResolveEvidenceAsync(tenantId, request, cancellationToken).ConfigureAwait(false);
        if (evidenceItems.Count == 0)
        {
            throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["EvidenceId"] = ["No preserved evidence matched the packaging request."]
            });
        }

        var evidenceIds = evidenceItems.Select(item => item.Id).ToList();
        var timelines = await context.Set<CustodyEvent>().AsNoTracking()
            .Where(custodyEvent => custodyEvent.TenantId == tenantId && evidenceIds.Contains(custodyEvent.EvidenceId))
            .OrderBy(custodyEvent => custodyEvent.EvidenceId)
            .ThenBy(custodyEvent => custodyEvent.SequenceNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var transferIds = timelines.Where(item => item.TransferId is not null).Select(item => item.TransferId!.Value).Distinct().ToList();
        var transfers = transferIds.Count == 0
            ? []
            : await context.Set<EvidenceTransfer>().AsNoTracking()
                .Where(transfer => transfer.TenantId == tenantId && transferIds.Contains(transfer.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var custodianIds = evidenceItems.Select(item => item.CurrentCustodianId)
            .Concat(timelines.Select(item => item.FromCustodianId).Where(id => id is not null).Cast<Guid>())
            .Concat(timelines.Select(item => item.ToCustodianId).Where(id => id is not null).Cast<Guid>())
            .Distinct()
            .ToList();
        var custodianRecords = await context.Set<EvidenceCustodian>().AsNoTracking()
            .Where(custodian => custodian.TenantId == tenantId && custodianIds.Contains(custodian.UserId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var holdIds = evidenceItems.Where(item => item.LegalHoldId is not null).Select(item => item.LegalHoldId!.Value).Distinct().ToList();
        var holds = holdIds.Count == 0
            ? []
            : await context.Set<LegalHold>().AsNoTracking()
                .Where(hold => hold.TenantId == tenantId && holdIds.Contains(hold.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        // Verification: preserved content hashes, gapless custody sequences, and every transfer proof signature.
        var contentVerified = evidenceItems.All(item => ChainOfCustodyService.ComputeHash(item.Content) == item.ContentSha256);
        var chainGapless = evidenceIds.All(evidenceId =>
        {
            var sequence = timelines.Where(item => item.EvidenceId == evidenceId).Select(item => item.SequenceNumber).OrderBy(number => number).ToList();
            return sequence.Count > 0 && sequence[0] == 1 && sequence[^1] == sequence.Count && sequence.Distinct().Count() == sequence.Count;
        });

        var verifiedTransfers = 0;
        foreach (var transfer in transfers)
        {
            _ = EvidenceTransferService.BuildProofPayload(transfer, out var canonical);
            try
            {
                if (signing.VerifySignature(canonical, transfer.Signature, transfer.SigningKeyId))
                {
                    verifiedTransfers++;
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Signature verification errored for transfer {TransferId} in package build", transfer.Id);
            }
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var certification = new
        {
            certifierUserId = userId,
            certifiedAtUtc = now,
            statement = request.CertificationStatement
                ?? "The undersigned certifies that the preserved evidence documented in this package was collected and handled under the recorded chain of custody, with integrity verified by SHA-256 content hashing, append-only custody sequencing, and ECDSA-signed transfer proofs.",
            methodology = "SHA-256 content hashing; append-only per-evidence custody sequencing; ECDSA transfer proof signatures; legal hold preservation records.",
            signatureKeyId = signing.GetActiveKeyId()
        };

        var manifest = new
        {
            packageId = Guid.NewGuid(),
            preparedAtUtc = now,
            preparedByUserId = userId,
            tenantId,
            evidence = evidenceItems.Select(item => new
            {
                item.Id,
                item.SourceKind,
                item.SourceId,
                item.ContentType,
                item.ContentSha256,
                item.CollectedAtUtc,
                item.CollectedByUserId,
                item.CustodyState,
                item.CurrentCustodianId,
                item.SealBrokenAtUtc,
                item.LegalHoldId,
                timeline = timelines.Where(item2 => item2.EvidenceId == item.Id).Select(item2 => new
                {
                    item2.SequenceNumber,
                    item2.EventType,
                    item2.FromCustodianId,
                    item2.ToCustodianId,
                    item2.ActorUserId,
                    item2.OccurredAtUtc,
                    item2.Purpose,
                    item2.TransferId,
                    item2.ProofJson
                })
            }),
            transfers = transfers.Select(transfer => new
            {
                transfer.Id,
                transfer.EvidenceId,
                transfer.FromCustodianId,
                transfer.ToCustodianId,
                transfer.State,
                transfer.InitiatedAtUtc,
                transfer.AcknowledgedAtUtc,
                transfer.EvidenceHash,
                transfer.ProofJson,
                transfer.Signature,
                transfer.SigningKeyId,
                transfer.AcknowledgmentSignature
            }),
            custodians = custodianRecords.Select(custodian => new
            {
                custodian.Id,
                custodian.UserId,
                custodian.DisplayName,
                scopes = EvidenceCustodianService.DeserializeScopes(custodian.ScopesJson),
                custodian.AuthorizedAtUtc,
                custodian.RevokedAtUtc
            }),
            legalHolds = holds.Select(hold => new
            {
                hold.Id,
                hold.CaseNumber,
                hold.Description,
                hold.Status,
                hold.CreatedAtUtc,
                hold.ReleasedAtUtc
            }),
            verification = new
            {
                contentHashVerified = contentVerified,
                chainSequenceGapless = chainGapless,
                transferProofsVerified = verifiedTransfers == transfers.Count,
                transferCount = transfers.Count,
                verifiedTransferCount = verifiedTransfers
            },
            certification
        };

        var manifestJson = JsonSerializer.Serialize(manifest, ManifestJsonOptions);
        var manifestSha = ValueSnapshotDiffEngine.ComputeSha256(manifestJson);
        var signature = signing.SignData(manifestJson, signing.GetActiveKeyId());

        // The sealed package doubles as the immutable container for the manifest (reuse of the
        // tamper-evident container store), while the manifest itself is the court document.
        var sealedPackage = new ComplianceSealedPackage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Chain of custody package {manifest.packageId}",
            TemplateId = "chain-of-custody",
            PreparedByUserId = userId,
            PeriodStartUtc = evidenceItems.Min(item => item.CollectedAtUtc),
            PeriodEndUtc = now,
            ReadyForAuditorReview = contentVerified && chainGapless,
            GapCount = transfers.Count - verifiedTransfers,
            ArtifactLength = Encoding.UTF8.GetByteCount(manifestJson),
            ArtifactSha256 = manifestSha,
            SigningKeyId = signing.GetActiveKeyId(),
            ArtifactContent = Encoding.UTF8.GetBytes(manifestJson),
            ManifestJson = JsonSerializer.Serialize(new { manifest.packageId, evidenceIds, verification = manifest.verification }, ManifestJsonOptions),
            SealJson = JsonSerializer.Serialize(new { manifestSha256 = manifestSha, signature, keyId = signing.GetActiveKeyId(), certifiedAtUtc = now }, ManifestJsonOptions)
        };
        context.Set<ComplianceSealedPackage>().Add(sealedPackage);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CourtAdmissiblePackageResponse(
            sealedPackage.Id,
            tenantId,
            manifestJson,
            manifestSha,
            signature,
            signing.GetActiveKeyId(),
            now,
            userId,
            certification.statement,
            new CustodyPackageVerification(contentVerified, chainGapless, verifiedTransfers == transfers.Count, transfers.Count, verifiedTransfers),
            evidenceIds);
    }

    private async Task<List<CustodyEvidenceItem>> ResolveEvidenceAsync(
        Guid tenantId, BuildCustodyPackageRequest request, CancellationToken cancellationToken)
    {
        if (request.EvidenceId is { } evidenceId)
        {
            var single = await context.Set<CustodyEvidenceItem>().AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.Id == evidenceId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return single;
        }

        if (request.LegalHoldId is { } holdId)
        {
            return await context.Set<CustodyEvidenceItem>().AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.LegalHoldId == holdId)
                .OrderBy(item => item.CollectedAtUtc)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return [];
    }

    private (Guid TenantId, Guid UserId) RequireAdministrator()
    {
        var actor = actors.ActorContext;
        if (!actor.IsAuthenticated || !actor.IsTenantAdmin || actor.TenantId is not { } tenant || tenant == Guid.Empty
            || actor.SubjectIdAsGuid is not { } user || user == Guid.Empty)
        {
            logger.LogWarning("Custody packaging access denied for actor {Actor}", actor.SubjectIdAsGuid);
            throw new UnauthorizedAccessException("A tenant administrator and tenant context are required.");
        }

        return (tenant, user);
    }
}
