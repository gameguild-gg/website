using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Compliance.Audit;

public sealed record PreserveEvidenceRequest
{
    /// <summary>Source system kind: AuditLog, SecurityEvent, TamperEvidentAuditLog, MutationValueSnapshot, ComplianceSealedPackage, or RawContent.</summary>
    public required string SourceKind { get; init; }

    /// <summary>Identifier of the source record ("inline" for RawContent).</summary>
    public required string SourceId { get; init; }

    /// <summary>Raw content to preserve (required for RawContent, ignored for record sources).</summary>
    public string? RawContent { get; init; }

    public string ContentType { get; init; } = "application/json";

    public string? Purpose { get; init; }

    /// <summary>Legal hold to link the preserved evidence to (used by the automated preservation workflow).</summary>
    public Guid? LegalHoldId { get; init; }
}

public sealed record CustodyEventResponse(
    Guid Id,
    int SequenceNumber,
    string EventType,
    Guid? FromCustodianId,
    Guid? ToCustodianId,
    Guid ActorUserId,
    string? Purpose,
    DateTime OccurredAtUtc,
    Guid? TransferId,
    string? ProofJson);

public sealed record CustodyEvidenceResponse(
    Guid Id,
    string SourceKind,
    string SourceId,
    string ContentType,
    int ContentLength,
    string ContentSha256,
    Guid CollectedByUserId,
    DateTime CollectedAtUtc,
    string CustodyState,
    Guid CurrentCustodianId,
    DateTime? SealBrokenAtUtc,
    Guid? LegalHoldId,
    bool Isolated,
    bool IntegrityVerified,
    IReadOnlyList<CustodyEventResponse> Timeline);

/// <summary>
///     Chain-of-custody documentation and evidence isolation (issue #177): forensic-grade
///     preservation with content hashing, append-only custody event chains per evidence item, and
///     isolation-enforced access — every access attempt (allowed or denied) is logged, and access
///     requires an authorized custodian. Evidence with a broken seal additionally requires the
///     forensic scope.
/// </summary>
public interface IChainOfCustodyService
{
    Task<CustodyEvidenceResponse> PreserveAsync(PreserveEvidenceRequest request, CancellationToken cancellationToken);

    Task<CustodyEvidenceResponse?> GetAsync(Guid evidenceId, string? purpose, CancellationToken cancellationToken);

    Task<IReadOnlyList<CustodyEvidenceResponse>> ListAsync(int skip, int take, CancellationToken cancellationToken);

    Task<CustodyEvidenceResponse> BreakSealAsync(Guid evidenceId, string purpose, CancellationToken cancellationToken);

    Task<CustodyEvidenceResponse> ReleaseAsync(Guid evidenceId, string reason, CancellationToken cancellationToken);

    /// <summary>Verifies the preserved content against its recorded SHA-256 (cryptographic integrity verification).</summary>
    Task<bool> VerifyIntegrityAsync(Guid evidenceId, CancellationToken cancellationToken);
}

public sealed class ChainOfCustodyService(
    IActorContextAccessor actors,
    IApplicationDbContext context,
    IEvidenceCustodianService custodians,
    TimeProvider timeProvider,
    ILogger<ChainOfCustodyService> logger) : IChainOfCustodyService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CustodyEvidenceResponse> PreserveAsync(PreserveEvidenceRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = RequireAdministrator();
        await custodians.RequireScopeAsync(userId, CustodianScopes.Handle, cancellationToken).ConfigureAwait(false);
        Validate(request);

        var content = await CollectSourceContentAsync(tenantId, request, cancellationToken).ConfigureAwait(false);
        var hash = Convert.ToHexStringLower(SHA256.HashData(content));
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var evidence = new CustodyEvidenceItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceKind = request.SourceKind,
            SourceId = request.SourceId,
            ContentType = request.ContentType,
            Content = content,
            ContentSha256 = hash,
            CollectedByUserId = userId,
            CollectedAtUtc = now,
            CurrentCustodianId = userId,
            LegalHoldId = request.LegalHoldId
        };
        context.Set<CustodyEvidenceItem>().Add(evidence);
        await AppendEventAsync(tenantId, evidence, "Collected", null, null, userId, request.Purpose, null, null, now, cancellationToken)
            .ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await MaterializeAsync(tenantId, evidence, verifyIntegrity: true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CustodyEvidenceResponse?> GetAsync(Guid evidenceId, string? purpose, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = RequireAdministrator();
        var evidence = await FindEvidenceAsync(tenantId, evidenceId, cancellationToken).ConfigureAwait(false);
        if (evidence is null)
        {
            return null;
        }

        // Evidence isolation: reads flow only through this custody-gated path, which authorizes
        // the custodian, refuses broken-seal items without forensic scope, and logs every attempt.
        try
        {
            await custodians.RequireScopeAsync(userId, CustodianScopes.Access, cancellationToken).ConfigureAwait(false);
        }
        catch (CustodyUnauthorizedException)
        {
            LogAccess(tenantId, evidence.Id, userId, "Denied", "Not an active custodian with access scope", purpose);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        if (evidence.SealBrokenAtUtc is not null
            && !await custodians.HasScopeAsync(userId, CustodianScopes.Forensic, cancellationToken).ConfigureAwait(false))
        {
            const string reason = "Seal broken: forensic scope required";
            LogAccess(tenantId, evidence.Id, userId, "Denied", reason, purpose);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw new CustodyUnauthorizedException(reason);
        }

        LogAccess(tenantId, evidence.Id, userId, "Allowed", "Authorized custodian", purpose);
        await AppendEventAsync(tenantId, evidence, "Accessed", null, null, userId, purpose, null, null,
            timeProvider.GetUtcNow().UtcDateTime, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await MaterializeAsync(tenantId, evidence, verifyIntegrity: true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CustodyEvidenceResponse>> ListAsync(int skip, int take, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = RequireAdministrator();
        await custodians.RequireScopeAsync(userId, CustodianScopes.Access, cancellationToken).ConfigureAwait(false);

        var items = await context.Set<CustodyEvidenceItem>().AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .OrderByDescending(item => item.CollectedAtUtc)
            .ThenBy(item => item.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var results = new List<CustodyEvidenceResponse>();
        foreach (var item in items)
        {
            results.Add(await MaterializeAsync(tenantId, item, verifyIntegrity: false, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    public async Task<CustodyEvidenceResponse> BreakSealAsync(Guid evidenceId, string purpose, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = RequireAdministrator();
        await custodians.RequireScopeAsync(userId, CustodianScopes.Forensic, cancellationToken).ConfigureAwait(false);
        var evidence = await FindEvidenceAsync(tenantId, evidenceId, cancellationToken).ConfigureAwait(false)
            ?? throw new CustodyUnauthorizedException("Evidence not found.");
        if (evidence.SealBrokenAtUtc is not null)
        {
            throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["EvidenceId"] = ["The seal on this evidence item is already broken."]
            });
        }

        evidence.SealBrokenAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        evidence.Touch();
        await AppendEventAsync(tenantId, evidence, "SealBroken", null, null, userId, purpose, null, null,
            evidence.SealBrokenAtUtc.Value, cancellationToken).ConfigureAwait(false);
        LogAccess(tenantId, evidence.Id, userId, "Allowed", "Seal broken for forensic inspection", purpose);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await MaterializeAsync(tenantId, evidence, verifyIntegrity: true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CustodyEvidenceResponse> ReleaseAsync(Guid evidenceId, string reason, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = RequireAdministrator();
        var evidence = await FindEvidenceAsync(tenantId, evidenceId, cancellationToken).ConfigureAwait(false)
            ?? throw new CustodyUnauthorizedException("Evidence not found.");

        if (evidence.LegalHoldId is { } holdId)
        {
            var holdActive = await context.Set<LegalHold>().AsNoTracking()
                .AnyAsync(hold => hold.Id == holdId && hold.Status == "Active", cancellationToken)
                .ConfigureAwait(false);
            if (holdActive)
            {
                throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
                {
                    ["EvidenceId"] = ["Evidence under an active legal hold cannot be released; release the hold first."]
                });
            }
        }

        await custodians.RequireScopeAsync(userId, CustodianScopes.Release, cancellationToken).ConfigureAwait(false);
        if (evidence.CustodyState == "Released")
        {
            throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["EvidenceId"] = ["This evidence item has already been released."]
            });
        }

        evidence.CustodyState = "Released";
        evidence.Touch();
        await AppendEventAsync(tenantId, evidence, "Released", evidence.CurrentCustodianId, null, userId, reason, null, null,
            timeProvider.GetUtcNow().UtcDateTime, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await MaterializeAsync(tenantId, evidence, verifyIntegrity: true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> VerifyIntegrityAsync(Guid evidenceId, CancellationToken cancellationToken)
    {
        var tenantId = RequireAdministrator().TenantId;
        var evidence = await FindEvidenceAsync(tenantId, evidenceId, cancellationToken).ConfigureAwait(false);
        return evidence is not null && ComputeHash(evidence.Content) == evidence.ContentSha256;
    }

    internal static string ComputeHash(byte[] content) =>
        Convert.ToHexStringLower(SHA256.HashData(content));

    /// <summary>Collects the preserved copy of the source record with its integrity verifiable.</summary>
    private async Task<byte[]> CollectSourceContentAsync(Guid tenantId, PreserveEvidenceRequest request, CancellationToken cancellationToken)
    {
        switch (request.SourceKind)
        {
            case "RawContent":
                return Encoding.UTF8.GetBytes(request.RawContent ?? string.Empty);

            case "AuditLog":
            {
                var log = await context.Set<AuditLog>().AsNoTracking()
                    .FirstOrDefaultAsync(candidate => candidate.Id.ToString() == request.SourceId && candidate.TenantId == tenantId, cancellationToken)
                    .ConfigureAwait(false);
                return log is null ? throw UnknownSource() : Encoding.UTF8.GetBytes(JsonSerializer.Serialize(log, JsonOptions));
            }

            case "MutationValueSnapshot":
            {
                var snapshot = await context.Set<MutationValueSnapshot>().AsNoTracking()
                    .FirstOrDefaultAsync(candidate => candidate.Id.ToString() == request.SourceId && candidate.TenantId == tenantId, cancellationToken)
                    .ConfigureAwait(false);
                return snapshot is null ? throw UnknownSource() : Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    snapshot.Id,
                    snapshot.EntityName,
                    snapshot.KeyId,
                    snapshot.ChangeKind,
                    snapshot.BeforeIntegritySha256,
                    snapshot.AfterIntegritySha256,
                    snapshot.StructuredDiffJson,
                    snapshot.ActorId,
                    snapshot.CorrelationId,
                    snapshot.OccurredAtUtc
                }, JsonOptions));
            }

            case "ComplianceSealedPackage":
            {
                var package = await context.Set<ComplianceSealedPackage>().AsNoTracking()
                    .FirstOrDefaultAsync(candidate => candidate.Id.ToString() == request.SourceId && candidate.TenantId == tenantId, cancellationToken)
                    .ConfigureAwait(false);
                return package is null ? throw UnknownSource() : Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    package.Id,
                    package.Name,
                    package.TemplateId,
                    package.PeriodStartUtc,
                    package.PeriodEndUtc,
                    package.ArtifactSha256,
                    package.SigningKeyId,
                    package.ManifestJson,
                    package.SealJson
                }, JsonOptions));
            }

            default:
                throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
                {
                    ["SourceKind"] = [$"Unsupported evidence source kind '{request.SourceKind}'."]
                });
        }

        static ValueSnapshotValidationException UnknownSource() => new(new Dictionary<string, string[]>
        {
            ["SourceId"] = ["The referenced source record does not exist in this tenant."]
        });
    }

    private async Task AppendEventAsync(
        Guid tenantId,
        CustodyEvidenceItem evidence,
        string eventType,
        Guid? fromCustodianId,
        Guid? toCustodianId,
        Guid actorUserId,
        string? purpose,
        Guid? transferId,
        string? proofJson,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
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
            FromCustodianId = fromCustodianId,
            ToCustodianId = toCustodianId,
            ActorUserId = actorUserId,
            Purpose = purpose,
            TransferId = transferId,
            ProofJson = proofJson,
            OccurredAtUtc = occurredAtUtc
        });
    }

    private void LogAccess(Guid tenantId, Guid evidenceId, Guid userId, string decision, string reason, string? purpose)
    {
        context.Set<CustodyAccessLog>().Add(new CustodyAccessLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EvidenceId = evidenceId,
            UserId = userId,
            Decision = decision,
            Reason = reason,
            Purpose = purpose,
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime
        });
    }

    private async Task<CustodyEvidenceResponse> MaterializeAsync(
        Guid tenantId,
        CustodyEvidenceItem evidence,
        bool verifyIntegrity,
        CancellationToken cancellationToken)
    {
        var timeline = await context.Set<CustodyEvent>().AsNoTracking()
            .Where(custodyEvent => custodyEvent.TenantId == tenantId && custodyEvent.EvidenceId == evidence.Id)
            .OrderBy(custodyEvent => custodyEvent.SequenceNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var integrityVerified = !verifyIntegrity || ComputeHash(evidence.Content) == evidence.ContentSha256;
        return new CustodyEvidenceResponse(
            evidence.Id,
            evidence.SourceKind,
            evidence.SourceId,
            evidence.ContentType,
            evidence.Content.Length,
            evidence.ContentSha256,
            evidence.CollectedByUserId,
            evidence.CollectedAtUtc,
            evidence.CustodyState,
            evidence.CurrentCustodianId,
            evidence.SealBrokenAtUtc,
            evidence.LegalHoldId,
            evidence.Isolated,
            integrityVerified,
            timeline.Select(item => new CustodyEventResponse(
                item.Id, item.SequenceNumber, item.EventType, item.FromCustodianId, item.ToCustodianId,
                item.ActorUserId, item.Purpose, item.OccurredAtUtc, item.TransferId, item.ProofJson)).ToList());
    }

    private Task<CustodyEvidenceItem?> FindEvidenceAsync(Guid tenantId, Guid evidenceId, CancellationToken cancellationToken) =>
        context.Set<CustodyEvidenceItem>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == evidenceId, cancellationToken);

    private static void Validate(PreserveEvidenceRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.SourceKind))
        {
            errors["SourceKind"] = ["A source kind is required."];
        }

        if (string.IsNullOrWhiteSpace(request.SourceId) && request.SourceKind != "RawContent")
        {
            errors["SourceId"] = ["A source id is required for record sources."];
        }

        if (request.SourceKind == "RawContent" && request.RawContent is null)
        {
            errors["RawContent"] = ["RawContent preservation requires the content payload."];
        }

        if (errors.Count > 0)
        {
            throw new ValueSnapshotValidationException(errors);
        }
    }

    private (Guid TenantId, Guid UserId) RequireAdministrator()
    {
        var actor = actors.ActorContext;
        if (!actor.IsAuthenticated || !actor.IsTenantAdmin || actor.TenantId is not { } tenant || tenant == Guid.Empty
            || actor.SubjectIdAsGuid is not { } user || user == Guid.Empty)
        {
            logger.LogWarning("Chain of custody access denied for actor {Actor}", actor.SubjectIdAsGuid);
            throw new UnauthorizedAccessException("A tenant administrator and tenant context are required.");
        }

        return (tenant, user);
    }
}
