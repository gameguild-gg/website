using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Compliance.Audit;

public interface IComplianceEvidenceDataSource
{
    Task<IReadOnlyList<ComplianceEvidenceDataset>> CaptureAsync(Guid tenantId, DateTime periodStartUtc,
        DateTime periodEndUtc, IReadOnlyList<ComplianceEvidenceKind> kinds, CancellationToken cancellationToken);
}

public interface IComplianceEvidencePackagingService
{
    Task<IReadOnlyList<ComplianceFrameworkTemplate>> GetTemplatesAsync(CancellationToken cancellationToken);
    Task<ComplianceDocumentResponse?> UploadDocumentAsync(UploadComplianceDocumentRequest request, CancellationToken cancellationToken);
    Task<ComplianceDocumentResponse?> GetDocumentAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ComplianceDocumentResponse>> ListDocumentsAsync(int skip, int take, CancellationToken cancellationToken);
    Task<ComplianceDocumentResponse?> ReviewDocumentAsync(Guid id, ReviewComplianceDocumentRequest request, CancellationToken cancellationToken);
    Task<CompliancePackageResponse?> CreatePackageAsync(CreateCompliancePackageRequest request, CancellationToken cancellationToken);
    Task<CompliancePackageResponse?> GetPackageAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompliancePackageSummary>> ListPackagesAsync(int skip, int take, CancellationToken cancellationToken);
    Task<CompliancePackageDownload?> DownloadPackageAsync(Guid id, CancellationToken cancellationToken);
    Task<ComplianceArtifactVerification?> VerifyPackageAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class ComplianceEvidencePackagingService(
    IActorContextAccessor actors, ICompliancePackagingRepository repository, IComplianceFrameworkCatalog catalog,
    IComplianceEvidenceDataSource source, ComplianceEvidenceValidationEngine validation, ComplianceArtifactBuilder artifacts,
    IAuditService audit, TimeProvider timeProvider) : IComplianceEvidencePackagingService
{
    public async Task<IReadOnlyList<ComplianceFrameworkTemplate>> GetTemplatesAsync(CancellationToken cancellationToken)
    {
        await RequireAdministratorAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return catalog.GetTemplates();
    }

    public async Task<ComplianceDocumentResponse?> UploadDocumentAsync(UploadComplianceDocumentRequest request, CancellationToken cancellationToken)
    {
        var (tenant, user) = await RequireAdministratorAsync().ConfigureAwait(false);
        var template = catalog.Find(request.TemplateId);
        if (template is null) { return null; }
        ValidateUpload(request);
        byte[] content;
        try { content = Convert.FromBase64String(request.ContentBase64); }
        catch (FormatException) { throw Invalid("ContentBase64", "The uploaded content must be valid base64."); }
        if (content.Length == 0 || content.Length > ComplianceEvidenceValidationEngine.MaximumDocumentBytes)
        {
            throw Invalid("ContentBase64", "An evidence document must contain between one byte and 1 MiB.");
        }
        ValidateContent(request.MediaType, content);
        var now = UtcPrecision(timeProvider.GetUtcNow().UtcDateTime);
        var document = new ComplianceEvidenceDocument
        {
            TenantId = tenant, CreatedAt = now, UpdatedAt = now, UploadedByUserId = user,
            TemplateId = request.TemplateId, Name = request.Name, Type = request.Type, MediaType = request.MediaType,
            Content = content, ContentSha256 = CompliancePackagingEncoding.Hash(content), SourceUri = request.SourceUri,
            ValidFromUtc = UtcPrecision(request.ValidFromUtc.UtcDateTime), ValidUntilUtc = UtcPrecision(request.ValidUntilUtc.UtcDateTime),
            ControlIdsJson = CompliancePackagingSerialization.Serialize(request.ControlIds),
            ValidationFieldsJson = CompliancePackagingSerialization.Serialize(request.ValidationFields),
            Review = ComplianceDocumentReview.Pending, Revision = 1
        };
        // Reuse all shared mapping and size checks; pending evidence is allowed to be uploaded for review.
        ComplianceEvidenceValidationEngine.ValidateInputs(template, DocumentRequest(document, now), [Snapshot(document)], [], now);
        await repository.AddDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        await AuditAsync("ComplianceEvidenceDocumentUploaded", tenant, user, document.Id, new { document.Revision, document.TemplateId }).ConfigureAwait(false);
        return Metadata(document);
    }

    public async Task<ComplianceDocumentResponse?> GetDocumentAsync(Guid id, CancellationToken cancellationToken)
    {
        var (tenant, user) = await RequireAdministratorAsync().ConfigureAwait(false);
        var document = await repository.GetDocumentAsync(tenant, id, cancellationToken).ConfigureAwait(false);
        if (document is null) { return null; }
        await AuditAsync("ComplianceEvidenceDocumentRead", tenant, user, id, new { document.Revision }).ConfigureAwait(false);
        return Metadata(document);
    }

    public async Task<IReadOnlyList<ComplianceDocumentResponse>> ListDocumentsAsync(int skip, int take, CancellationToken cancellationToken)
    {
        var (tenant, _) = await RequireAdministratorAsync().ConfigureAwait(false);
        ValidatePagination(skip, take);
        return await repository.ListDocumentsAsync(tenant, skip, take, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ComplianceDocumentResponse?> ReviewDocumentAsync(Guid id, ReviewComplianceDocumentRequest request, CancellationToken cancellationToken)
    {
        var (tenant, user) = await RequireAdministratorAsync().ConfigureAwait(false);
        if (request.ExpectedRevision < 1 || request.Decision is not (ComplianceDocumentReview.Approved or ComplianceDocumentReview.Rejected) ||
            string.IsNullOrWhiteSpace(request.Notes) || request.Notes.Length > 2000)
        {
            throw Invalid("Review", "Use a positive revision, an approval or rejection, and review notes of at most 2000 characters.");
        }
        var document = await repository.GetDocumentAsync(tenant, id, cancellationToken).ConfigureAwait(false);
        if (document is null) { return null; }
        if (document.Revision != request.ExpectedRevision || document.Revision == int.MaxValue) { throw new CompliancePackagingConcurrencyException(); }
        var now = UtcPrecision(timeProvider.GetUtcNow().UtcDateTime);
        if (request.Decision == ComplianceDocumentReview.Approved)
        {
            var template = catalog.Find(document.TemplateId) ?? throw Invalid("Template", "The uploaded document template is unavailable.");
            var candidate = Snapshot(document) with { Review = request.Decision, ReviewedByUserId = user, ReviewedAtUtc = now, Revision = document.Revision + 1 };
            // Approve document quality now; package creation separately checks the chosen audit period.
            var gaps = validation.InspectDocumentQuality(template, DocumentRequest(document, now), candidate, now, cancellationToken);
            var errors = gaps.Where(item => item.Code != "DocumentPeriodGap").Select(item => item.Detail).Distinct().ToArray();
            if (errors.Length != 0) { throw new CompliancePackagingValidationException(new Dictionary<string, string[]> { ["Evidence"] = errors }); }
        }
        var previous = document.Review;
        document.Review = request.Decision;
        document.ReviewedByUserId = user;
        document.ReviewedAtUtc = now;
        document.ReviewNotes = request.Notes;
        document.UpdatedAt = now;
        document.Revision++;
        await repository.SaveDocumentReviewAsync(document, cancellationToken).ConfigureAwait(false);
        await AuditAsync("ComplianceEvidenceDocumentReviewed", tenant, user, id, new { document.Revision, Previous = previous, Decision = document.Review }).ConfigureAwait(false);
        return Metadata(document);
    }

    public async Task<CompliancePackageResponse?> CreatePackageAsync(CreateCompliancePackageRequest request, CancellationToken cancellationToken)
    {
        var (tenant, user) = await RequireAdministratorAsync().ConfigureAwait(false);
        if (request.PeriodStartUtc.Offset != TimeSpan.Zero || request.PeriodEndUtc.Offset != TimeSpan.Zero)
        {
            throw Invalid("Period", "Audit periods must use UTC offsets.");
        }
        request = request with
        {
            PeriodStartUtc = UtcPrecision(request.PeriodStartUtc.UtcDateTime),
            PeriodEndUtc = UtcPrecision(request.PeriodEndUtc.UtcDateTime)
        };
        var template = catalog.Find(request.TemplateId);
        if (template is null) { return null; }
        if (template.PeriodMode == ComplianceEvidencePeriodMode.Period && request.PeriodStartUtc >= request.PeriodEndUtc)
        {
            throw Invalid("Period", "A period-based package requires a nonzero audit interval.");
        }
        if (request.DocumentIds is null || request.DocumentIds.Count > 100 || request.DocumentIds.Distinct().Count() != request.DocumentIds.Count)
        {
            throw Invalid("DocumentIds", "Use at most 100 unique document identifiers.");
        }
        var documents = await repository.GetDocumentsAsync(tenant, request.DocumentIds, cancellationToken).ConfigureAwait(false);
        if (documents.Count != request.DocumentIds.Count) { return null; }
        var now = UtcPrecision(timeProvider.GetUtcNow().UtcDateTime);
        var snapshots = documents.Select(Snapshot).ToArray();
        ComplianceEvidenceValidationEngine.ValidateInputs(template, request, snapshots, [], now);
        var kinds = template.Controls.SelectMany(item => item.AutomaticEvidence).Distinct().Order().ToArray();
        var datasets = await source.CaptureAsync(tenant, request.PeriodStartUtc.UtcDateTime, request.PeriodEndUtc.UtcDateTime, kinds, cancellationToken).ConfigureAwait(false);
        var package = new ComplianceSealedPackage
        {
            TenantId = tenant, PreparedByUserId = user, CreatedAt = now, UpdatedAt = now, Name = request.Name,
            TemplateId = request.TemplateId, PeriodStartUtc = request.PeriodStartUtc.UtcDateTime, PeriodEndUtc = request.PeriodEndUtc.UtcDateTime
        };
        CompliancePackageArtifact artifact;
        try { artifact = artifacts.Build(package.Id, tenant, user, now, template, request, snapshots, datasets, cancellationToken); }
        catch (Exception exception) when (exception is CryptographicException or InvalidOperationException or ArgumentException)
        {
            throw new CompliancePackagingSigningUnavailableException(exception);
        }
        package.ArtifactContent = artifact.ZipContent;
        package.ArtifactLength = artifact.ZipContent.Length;
        package.ArtifactSha256 = artifact.ArtifactSha256;
        package.ManifestJson = CompliancePackagingSerialization.Serialize(artifact.Manifest);
        package.SealJson = CompliancePackagingSerialization.Serialize(artifact.Seal);
        package.SigningKeyId = artifact.Seal.KeyId;
        package.ReadyForAuditorReview = artifact.Manifest.Validation.ReadyForAuditorReview;
        package.GapCount = artifact.Manifest.Validation.Gaps.Count;
        await repository.AddPackageAsync(package, cancellationToken).ConfigureAwait(false);
        await AuditAsync("ComplianceEvidencePackagePrepared", tenant, user, package.Id,
            new { package.TemplateId, package.ReadyForAuditorReview, package.GapCount, package.ArtifactSha256 }).ConfigureAwait(false);
        return new(Summary(package), artifact.Manifest, artifact.Seal);
    }

    public async Task<CompliancePackageResponse?> GetPackageAsync(Guid id, CancellationToken cancellationToken)
    {
        var (tenant, user) = await RequireAdministratorAsync().ConfigureAwait(false);
        var package = await repository.GetPackageAsync(tenant, id, cancellationToken).ConfigureAwait(false);
        if (package is null) { return null; }
        var result = VerifyStored(package, tenant, cancellationToken);
        if (!result.IsValid) { throw new CompliancePackagingIntegrityException(); }
        await AuditAsync("ComplianceEvidencePackageRead", tenant, user, id, new { package.ArtifactSha256 }).ConfigureAwait(false);
        return new(Summary(package), CompliancePackagingSerialization.Deserialize<ComplianceArtifactManifest>(package.ManifestJson),
            CompliancePackagingSerialization.Deserialize<ComplianceArtifactSeal>(package.SealJson));
    }

    public async Task<IReadOnlyList<CompliancePackageSummary>> ListPackagesAsync(int skip, int take, CancellationToken cancellationToken)
    {
        var (tenant, _) = await RequireAdministratorAsync().ConfigureAwait(false);
        ValidatePagination(skip, take);
        return await repository.ListPackagesAsync(tenant, skip, take, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CompliancePackageDownload?> DownloadPackageAsync(Guid id, CancellationToken cancellationToken)
    {
        var (tenant, user) = await RequireAdministratorAsync().ConfigureAwait(false);
        var package = await repository.GetPackageAsync(tenant, id, cancellationToken).ConfigureAwait(false);
        if (package is null) { return null; }
        if (!VerifyStored(package, tenant, cancellationToken).IsValid) { throw new CompliancePackagingIntegrityException(); }
        await AuditAsync("ComplianceEvidencePackageDownloaded", tenant, user, id, new { package.ArtifactSha256 }).ConfigureAwait(false);
        return new(package.ArtifactContent, $"compliance-evidence-{id:D}.zip", package.ArtifactSha256);
    }

    public async Task<ComplianceArtifactVerification?> VerifyPackageAsync(Guid id, CancellationToken cancellationToken)
    {
        var (tenant, user) = await RequireAdministratorAsync().ConfigureAwait(false);
        var package = await repository.GetPackageAsync(tenant, id, cancellationToken).ConfigureAwait(false);
        if (package is null) { return null; }
        var result = VerifyStored(package, tenant, cancellationToken);
        await AuditAsync("ComplianceEvidencePackageVerified", tenant, user, id, new { result.IsValid }, result.IsValid).ConfigureAwait(false);
        return result;
    }

    private ComplianceArtifactVerification VerifyStored(ComplianceSealedPackage package, Guid tenant, CancellationToken cancellationToken)
    {
        if (package.ArtifactLength != package.ArtifactContent.Length || package.ArtifactSha256 != CompliancePackagingEncoding.Hash(package.ArtifactContent))
        {
            return new(false, ["The stored artifact length or checksum changed."]);
        }
        var verification = artifacts.Verify(package.ArtifactContent, tenant, package.Id, cancellationToken);
        if (!verification.IsValid) { return verification; }
        using var archive = new ZipArchive(new MemoryStream(package.ArtifactContent, false), ZipArchiveMode.Read);
        foreach (var (path, stored) in new[] { ("manifest.json", package.ManifestJson), ("seal.json", package.SealJson) })
        {
            using var stream = archive.GetEntry(path)!.Open();
            using var content = new MemoryStream();
            stream.CopyTo(content);
            if (!content.ToArray().AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(stored)))
            {
                return new(false, ["Stored metadata differs from the sealed artifact."]);
            }
        }
        var manifest = CompliancePackagingSerialization.Deserialize<ComplianceArtifactManifest>(package.ManifestJson);
        var seal = CompliancePackagingSerialization.Deserialize<ComplianceArtifactSeal>(package.SealJson);
        if (package.Name != manifest.Name || package.TemplateId != manifest.Template.Id || package.PreparedByUserId != manifest.PreparedByUserId ||
            package.CreatedAt != manifest.CapturedAtUtc || package.PeriodStartUtc != manifest.PeriodStartUtc || package.PeriodEndUtc != manifest.PeriodEndUtc ||
            package.ReadyForAuditorReview != manifest.Validation.ReadyForAuditorReview || package.GapCount != manifest.Validation.Gaps.Count || package.SigningKeyId != seal.KeyId)
        {
            return new(false, ["Stored package summary differs from the signed manifest."]);
        }
        return verification;
    }

    private async Task<(Guid Tenant, Guid User)> RequireAdministratorAsync()
    {
        var actor = actors.ActorContext;
        if (!actor.IsAuthenticated || !actor.IsTenantAdmin || actor.TenantId is null || actor.TenantId == Guid.Empty ||
            actor.SubjectIdAsGuid is null || actor.SubjectIdAsGuid == Guid.Empty)
        {
            await audit.LogPermissionDenyAsync(actor.SubjectIdAsGuid, "audit:compliance-package", "ComplianceEvidencePackage", null,
                "An authenticated tenant administrator and tenant context are required.", actor.TenantId).ConfigureAwait(false);
            throw new UnauthorizedAccessException("A tenant administrator and tenant context are required.");
        }
        return (actor.TenantId.Value, actor.SubjectIdAsGuid.Value);
    }

    private Task AuditAsync(string action, Guid tenant, Guid user, Guid resource, object metadata, bool success = true) =>
        audit.LogAsync(new CreateAuditLogRequest
        {
            TenantId = tenant, UserId = user, ActionType = action, ResourceType = "ComplianceEvidencePackage", ResourceId = resource.ToString(),
            Category = AuditCategory.Admin, RiskLevel = success ? AuditRiskLevel.Medium : AuditRiskLevel.High,
            Success = success, Description = action, Metadata = metadata
        });

    private static ComplianceDocumentSnapshot Snapshot(ComplianceEvidenceDocument document) => new(
        document.Id, document.Name, document.Type, document.MediaType, document.ContentSha256, document.Content,
        document.SourceUri, document.ValidFromUtc, document.ValidUntilUtc,
        CompliancePackagingSerialization.Deserialize<List<string>>(document.ControlIdsJson),
        CompliancePackagingSerialization.Deserialize<Dictionary<string, string>>(document.ValidationFieldsJson), document.Review,
        document.UploadedByUserId, document.ReviewedByUserId, document.ReviewedAtUtc, document.Revision, document.TemplateId);

    private static ComplianceDocumentResponse Metadata(ComplianceEvidenceDocument document) => new(
        document.Id, document.TemplateId, document.Name, document.Type, document.MediaType, document.Content.Length, document.ContentSha256,
        document.SourceUri, document.ValidFromUtc, document.ValidUntilUtc, CompliancePackagingSerialization.Deserialize<List<string>>(document.ControlIdsJson),
        document.Review, document.UploadedByUserId, document.ReviewedByUserId, document.ReviewedAtUtc, document.ReviewNotes, document.Revision);

    private static CompliancePackageSummary Summary(ComplianceSealedPackage package) => new(package.Id, package.Name, package.TemplateId,
        package.PreparedByUserId, package.CreatedAt, package.PeriodStartUtc, package.PeriodEndUtc,
        package.ReadyForAuditorReview, package.GapCount, package.ArtifactLength, package.ArtifactSha256, package.SigningKeyId);

    private static CreateCompliancePackageRequest DocumentRequest(ComplianceEvidenceDocument document, DateTime now) => new()
    {
        Name = document.Name, TemplateId = document.TemplateId, DocumentIds = [document.Id],
        PeriodStartUtc = now, PeriodEndUtc = now
    };

    private static DateTime UtcPrecision(DateTime value) => new(value.Ticks - value.Ticks % 10, DateTimeKind.Utc);

    private static void ValidatePagination(int skip, int take)
    {
        if (skip < 0 || take is < 1 or > 100) { throw Invalid("Pagination", "Use skip >= 0 and take between 1 and 100."); }
    }
    private static CompliancePackagingValidationException Invalid(string field, string message) => new(new Dictionary<string, string[]> { [field] = [message] });

    private static void ValidateUpload(UploadComplianceDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200 || string.IsNullOrWhiteSpace(request.Type) || request.Type.Length > 80 ||
            request.ContentBase64 is not { Length: > 0 and <= 1500000 } || request.ControlIds is null || request.ValidationFields is null ||
            request.ValidFromUtc.Offset != TimeSpan.Zero || request.ValidUntilUtc.Offset != TimeSpan.Zero ||
            request.ValidFromUtc > request.ValidUntilUtc || request.ValidFromUtc.Year < 1980 || request.ValidUntilUtc.Year > 2107)
        {
            throw Invalid("Document", "Document names, type, content, control mapping, fields and UTC validity dates are required.");
        }
        if (request.ControlIds.Count > 1500 || request.ControlIds.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 100) ||
            request.ValidationFields.Count > 50 || request.ValidationFields.Any(field => field.Key.Length > 80 || field.Value is null || field.Value.Length > 4000))
        {
            throw Invalid("Document", "Control identifiers and declared validation fields exceed the supported limits.");
        }
    }
    private static void ValidateContent(string mediaType, byte[] content)
    {
        try
        {
            if (mediaType == "application/json")
            {
                using var json = CompliancePackagingEncoding.Parse(content);
                if (json.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) { throw Invalid("Content", "An assessment must be a JSON object."); }
            }
            else if (mediaType == "text/plain") { _ = new UTF8Encoding(false, true).GetString(content); }
            else if (mediaType != "application/pdf" || !content.AsSpan().StartsWith("%PDF-"u8))
            {
                throw Invalid("Content", "Use a JSON object, UTF-8 text or a PDF document.");
            }
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or DecoderFallbackException)
        {
            throw Invalid("Content", "The uploaded document encoding or JSON structure is invalid.");
        }
    }
}
