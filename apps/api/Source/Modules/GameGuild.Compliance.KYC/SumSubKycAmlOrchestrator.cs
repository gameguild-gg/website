using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GameGuild.Compliance.KYC;

public sealed record StartKycAmlRequest(
    Guid TenantId,
    string SubjectHash,
    string IdempotencyKey,
    DateTimeOffset RequestedAt);

public sealed record KycAmlOnboarding(
    string ApplicantId,
    string SubjectHash,
    KycAmlState State,
    DateTimeOffset UpdatedAt)
{
    public string? JurisdictionCode { get; init; }
}

public sealed record SumSubWebhookIngestionResult(
    KycEvidenceIngestionStatus Status,
    string ProviderEventId,
    KycAmlState State,
    Guid? EvidenceId);

public interface IKycAmlOrchestrator
{
    Task<KycAmlOnboarding> StartAsync(StartKycAmlRequest request, CancellationToken cancellationToken);

    Task<KycAmlAccessToken> CreateAccessTokenAsync(
        Guid tenantId,
        string subjectHash,
        int lifetimeSeconds,
        CancellationToken cancellationToken);

    Task<SumSubWebhookIngestionResult> IngestWebhookAsync(
        ReadOnlyMemory<byte> rawPayload,
        string suppliedDigest,
        string digestAlgorithm,
        DateTimeOffset issuedAt,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken);

    Task<KycEvidenceIngestionResult> ReconcileAsync(
        Guid tenantId,
        string subjectHash,
        DateTimeOffset reconciledAt,
        CancellationToken cancellationToken);
}

public sealed class SumSubKycAmlOrchestrator : IKycAmlOrchestrator
{
    private const string ProviderName = "sumsub";
    private readonly DbContext _db;
    private readonly IKycAmlProvider _provider;
    private readonly IKycEvidenceStore _evidence;
    private readonly IComplianceRawObjectStore _rawObjects;
    private readonly KycPolicyOptions _policy;

    public SumSubKycAmlOrchestrator(
        IApplicationDbContext context,
        IKycAmlProvider provider,
        IKycEvidenceStore evidence,
        IComplianceRawObjectStore rawObjects,
        IOptions<KycPolicyOptions> policy)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext ?? throw new InvalidOperationException(
            "SumSub orchestration requires the application's relational DbContext.");
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        _rawObjects = rawObjects ?? throw new ArgumentNullException(nameof(rawObjects));
        _policy = (policy ?? throw new ArgumentNullException(nameof(policy))).Value;
    }

    public async Task<KycAmlOnboarding> StartAsync(
        StartKycAmlRequest request,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(request.TenantId, request.SubjectHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IdempotencyKey);
        EnsurePolicyConfigured();
        var idempotencyHash = Hash(request.IdempotencyKey.Trim());
        var replay = await _db.Set<SumSubApplicantBindingRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.IdempotencyKeyHash == idempotencyHash, cancellationToken);
        if (replay is not null)
        {
            if (replay.TenantId != request.TenantId || replay.SubjectHash != request.SubjectHash)
                throw new InvalidOperationException("KYC onboarding idempotency is bound to another subject.");
            return Map(replay);
        }

        var existing = await _db.Set<SumSubApplicantBindingRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.TenantId == request.TenantId &&
                                         row.SubjectHash == request.SubjectHash, cancellationToken);
        if (existing is not null) return Map(existing);

        var externalUserId = Hash($"{request.TenantId:N}:{request.SubjectHash}");
        var applicant = await _provider.CreateApplicantAsync(
            new KycAmlApplicantRequest(externalUserId, _policy.LevelName.Trim()), cancellationToken);
        var row = new SumSubApplicantBindingRow
        {
            Id = Guid.NewGuid(), TenantId = request.TenantId, SubjectHash = request.SubjectHash.Trim(),
            ApplicantId = applicant.ApplicantId, ExternalUserIdHash = Hash(externalUserId),
            IdempotencyKeyHash = idempotencyHash, State = applicant.State, EvidenceVersion = 0,
            CreatedAt = request.RequestedAt, UpdatedAt = request.RequestedAt
        };
        _db.Set<SumSubApplicantBindingRow>().Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
    }

    public async Task<KycAmlAccessToken> CreateAccessTokenAsync(
        Guid tenantId,
        string subjectHash,
        int lifetimeSeconds,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(tenantId, subjectHash);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lifetimeSeconds);
        EnsurePolicyConfigured();
        var binding = await BindingAsync(tenantId, subjectHash, cancellationToken);
        var externalUserId = Hash($"{binding.TenantId:N}:{binding.SubjectHash}");
        return await _provider.CreateAccessTokenAsync(
            externalUserId, _policy.LevelName.Trim(), lifetimeSeconds, cancellationToken);
    }

    public async Task<SumSubWebhookIngestionResult> IngestWebhookAsync(
        ReadOnlyMemory<byte> rawPayload,
        string suppliedDigest,
        string digestAlgorithm,
        DateTimeOffset issuedAt,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken)
    {
        if (rawPayload.IsEmpty) throw new ArgumentException("SumSub webhook payload cannot be empty.", nameof(rawPayload));
        EnsurePolicyConfigured();
        using var document = JsonDocument.Parse(rawPayload);
        var root = document.RootElement;
        var applicantId = RequiredString(root, "applicantId");
        var providerEventId = EventId(root, applicantId, issuedAt);
        var signatureVerified = _provider.VerifyWebhook(
            rawPayload.Span, suppliedDigest, digestAlgorithm, issuedAt, receivedAt);
        var stored = await _rawObjects.PutAsync(
            ProviderName, _policy.Environment, providerEventId, rawPayload, cancellationToken);

        var replay = await _db.Set<SumSubWebhookInboxRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.ProviderEventId == providerEventId, cancellationToken);
        if (replay is not null)
        {
            if (replay.PayloadHash != stored.PayloadHash)
                throw new KycEvidenceConflictException(
                    "The SumSub event was replayed with a different payload hash.");
            var replayBinding = await _db.Set<SumSubApplicantBindingRow>().AsNoTracking()
                .SingleOrDefaultAsync(row => row.ApplicantId == applicantId, cancellationToken);
            return new SumSubWebhookIngestionResult(
                KycEvidenceIngestionStatus.Duplicate,
                providerEventId,
                replayBinding?.State ?? KycAmlState.NeedsReview,
                null);
        }

        var inbox = new SumSubWebhookInboxRow
        {
            Id = Guid.NewGuid(), ProviderEventId = providerEventId, ApplicantId = applicantId,
            PayloadHash = stored.PayloadHash, RawObjectReference = stored.Reference,
            SignatureVerified = signatureVerified, IssuedAt = issuedAt, ReceivedAt = receivedAt
        };
        _db.Set<SumSubWebhookInboxRow>().Add(inbox);
        var binding = await _db.Set<SumSubApplicantBindingRow>()
            .SingleOrDefaultAsync(row => row.ApplicantId == applicantId, cancellationToken);
        if (!signatureVerified || binding is null || issuedAt > receivedAt)
        {
            inbox.ProcessingError = !signatureVerified
                ? "invalid-signature"
                : binding is null ? "unknown-applicant" : "invalid-timestamp";
            await _db.SaveChangesAsync(cancellationToken);
            return new SumSubWebhookIngestionResult(
                KycEvidenceIngestionStatus.Rejected, providerEventId,
                binding?.State ?? KycAmlState.NeedsReview, null);
        }
        if (binding.LastProviderIssuedAt is not null && issuedAt <= binding.LastProviderIssuedAt)
        {
            inbox.ProcessingError = "out-of-order-event";
            await _db.SaveChangesAsync(cancellationToken);
            return new SumSubWebhookIngestionResult(
                KycEvidenceIngestionStatus.Deferred, providerEventId, binding.State, null);
        }

        var state = MapWebhookState(root);
        var jurisdiction = await ResolveApprovedJurisdictionAsync(
            binding.ApplicantId, state, cancellationToken);
        var ingestion = await PublishAsync(
            binding, providerEventId, state, issuedAt, receivedAt,
            stored.PayloadHash, stored.Reference, jurisdiction, cancellationToken);
        if (ingestion.Status == KycEvidenceIngestionStatus.Published)
        {
            binding.State = state;
            binding.JurisdictionCode = jurisdiction;
            binding.EvidenceVersion++;
            binding.LastProviderIssuedAt = issuedAt;
            binding.LastProviderEventId = providerEventId;
            binding.UpdatedAt = receivedAt;
            inbox.ProcessedAt = receivedAt;
        }
        else
        {
            inbox.ProcessingError = ingestion.Status.ToString().ToLowerInvariant();
        }
        await _db.SaveChangesAsync(cancellationToken);
        return new SumSubWebhookIngestionResult(ingestion.Status, providerEventId, state, ingestion.EvidenceId);
    }

    public async Task<KycEvidenceIngestionResult> ReconcileAsync(
        Guid tenantId,
        string subjectHash,
        DateTimeOffset reconciledAt,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(tenantId, subjectHash);
        EnsurePolicyConfigured();
        var binding = await BindingAsync(tenantId, subjectHash, cancellationToken);
        var status = await _provider.GetStatusAsync(binding.ApplicantId, cancellationToken);
        var normalized = JsonSerializer.SerializeToUtf8Bytes(new
        {
            status.ApplicantId,
            ExternalUserIdHash = Hash(status.ExternalUserId),
            State = status.State.ToString(),
            ReconciledAt = reconciledAt
        });
        var eventId = $"poll:{binding.ApplicantId}:{binding.EvidenceVersion + 1}:{(int)status.State}";
        var stored = await _rawObjects.PutAsync(
            ProviderName, _policy.Environment, eventId, normalized, cancellationToken);
        var result = await PublishAsync(
            binding, eventId, status.State, reconciledAt, reconciledAt,
            stored.PayloadHash, stored.Reference,
            RequireApprovedJurisdiction(status), cancellationToken);
        if (result.Status == KycEvidenceIngestionStatus.Published)
        {
            binding.State = status.State;
            binding.JurisdictionCode = RequireApprovedJurisdiction(status);
            binding.EvidenceVersion++;
            binding.LastProviderIssuedAt = reconciledAt;
            binding.LastProviderEventId = eventId;
            binding.UpdatedAt = reconciledAt;
            await _db.SaveChangesAsync(cancellationToken);
        }
        return result;
    }

    private ValueTask<KycEvidenceIngestionResult> PublishAsync(
        SumSubApplicantBindingRow binding,
        string providerEventId,
        KycAmlState state,
        DateTimeOffset issuedAt,
        DateTimeOffset receivedAt,
        string payloadHash,
        string rawReference,
        string? jurisdictionCode,
        CancellationToken cancellationToken)
    {
        var lifetime = state == KycAmlState.Approved
            ? _policy.ApprovedEvidenceLifetime
            : _policy.ReviewEvidenceLifetime;
        if (lifetime <= TimeSpan.Zero)
            throw new SumSubNotConfiguredException("KYC evidence lifetimes must be explicitly configured.");
        var envelope = new KycEvidenceSubmission(
            ProviderName, _policy.Environment.Trim(), providerEventId, binding.TenantId,
            binding.SubjectHash, binding.EvidenceVersion + 1, MapResult(state), issuedAt,
            issuedAt.Add(lifetime), _policy.PolicyVersion, payloadHash, true, rawReference, receivedAt,
            jurisdictionCode);
        return _evidence.IngestAsync(envelope, cancellationToken);
    }

    private async Task<string?> ResolveApprovedJurisdictionAsync(
        string applicantId,
        KycAmlState state,
        CancellationToken cancellationToken)
    {
        if (state != KycAmlState.Approved)
            return null;

        var status = await _provider.GetStatusAsync(applicantId, cancellationToken);
        if (!string.Equals(status.ApplicantId, applicantId, StringComparison.Ordinal))
            throw new SumSubProtocolException("SumSub status was returned for another applicant.");
        return RequireApprovedJurisdiction(status);
    }

    private static string? RequireApprovedJurisdiction(KycAmlStatus status)
    {
        if (status.State != KycAmlState.Approved)
            return null;

        return SumSubApplicantJurisdiction.Normalize(status.JurisdictionCode)
            ?? throw new SumSubProtocolException(
                "Approved SumSub evidence does not contain a verified ISO alpha-3 jurisdiction.");
    }

    private async Task<SumSubApplicantBindingRow> BindingAsync(
        Guid tenantId,
        string subjectHash,
        CancellationToken cancellationToken) =>
        await _db.Set<SumSubApplicantBindingRow>().SingleOrDefaultAsync(
            row => row.TenantId == tenantId && row.SubjectHash == subjectHash.Trim(), cancellationToken)
        ?? throw new KeyNotFoundException("KYC onboarding was not found for the subject.");

    private void EnsurePolicyConfigured()
    {
        if (_policy.PolicyVersion <= 0 || string.IsNullOrWhiteSpace(_policy.Environment) ||
            string.IsNullOrWhiteSpace(_policy.LevelName))
            throw new SumSubNotConfiguredException(
                "SumSub KYC/AML remains disabled until a signed policy version, environment and level are configured.");
    }

    private static void ValidateIdentity(Guid tenantId, string subjectHash)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectHash);
    }

    private static string RequiredString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new SumSubProtocolException($"SumSub webhook is missing {propertyName}.");

    private static string EventId(JsonElement root, string applicantId, DateTimeOffset issuedAt)
    {
        if (root.TryGetProperty("inspectionId", out var inspection) &&
            inspection.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(inspection.GetString()))
            return inspection.GetString()!;
        var type = root.TryGetProperty("type", out var eventType) ? eventType.GetString() : "unknown";
        return Hash($"{applicantId}:{type}:{issuedAt.UtcTicks}");
    }

    private static KycAmlState MapWebhookState(JsonElement root)
    {
        var reviewStatus = root.TryGetProperty("reviewStatus", out var status) ? status.GetString() : null;
        var answer = root.TryGetProperty("reviewResult", out var result) &&
                     result.TryGetProperty("reviewAnswer", out var reviewAnswer)
            ? reviewAnswer.GetString()
            : null;
        if (string.Equals(answer, "GREEN", StringComparison.Ordinal)) return KycAmlState.Approved;
        if (string.Equals(answer, "RED", StringComparison.Ordinal)) return KycAmlState.Rejected;
        return string.Equals(reviewStatus, "pending", StringComparison.Ordinal)
            ? KycAmlState.ApplicantPending
            : string.Equals(reviewStatus, "completed", StringComparison.Ordinal)
                ? KycAmlState.NeedsReview
                : KycAmlState.InReview;
    }

    private static KycEvidenceResult MapResult(KycAmlState state) => state switch
    {
        KycAmlState.Approved => KycEvidenceResult.Approved,
        KycAmlState.Rejected => KycEvidenceResult.Rejected,
        KycAmlState.Created or KycAmlState.ApplicantPending or KycAmlState.InReview or KycAmlState.NeedsReview =>
            KycEvidenceResult.NeedsReview,
        _ => KycEvidenceResult.Unavailable
    };

    private static KycAmlOnboarding Map(SumSubApplicantBindingRow row) =>
        new(row.ApplicantId, row.SubjectHash, row.State, row.UpdatedAt)
        {
            JurisdictionCode = row.JurisdictionCode
        };

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
