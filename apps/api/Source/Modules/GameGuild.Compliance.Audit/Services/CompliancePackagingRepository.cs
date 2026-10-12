using Microsoft.EntityFrameworkCore;

namespace GameGuild.Compliance.Audit;

public interface ICompliancePackagingRepository
{
    Task AddDocumentAsync(ComplianceEvidenceDocument document, CancellationToken cancellationToken);
    Task<ComplianceEvidenceDocument?> GetDocumentAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ComplianceEvidenceDocument>> GetDocumentsAsync(Guid tenantId, IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
    Task<IReadOnlyList<ComplianceDocumentResponse>> ListDocumentsAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken);
    Task SaveDocumentReviewAsync(ComplianceEvidenceDocument document, CancellationToken cancellationToken);
    Task AddPackageAsync(ComplianceSealedPackage package, CancellationToken cancellationToken);
    Task<ComplianceSealedPackage?> GetPackageAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompliancePackageSummary>> ListPackagesAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken);
}

public sealed class CompliancePackagingRepository(IApplicationDbContext context) : ICompliancePackagingRepository
{
    public async Task AddDocumentAsync(ComplianceEvidenceDocument document, CancellationToken cancellationToken)
    {
        context.Set<ComplianceEvidenceDocument>().Add(document);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<ComplianceEvidenceDocument?> GetDocumentAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        context.Set<ComplianceEvidenceDocument>().SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ComplianceEvidenceDocument>> GetDocumentsAsync(Guid tenantId, IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        await context.Set<ComplianceEvidenceDocument>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && ids.Contains(item.Id)).OrderBy(item => item.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ComplianceDocumentResponse>> ListDocumentsAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken)
    {
        // Project metadata explicitly: listing must not fetch or return uploaded evidence bytes.
        var rows = await context.Set<ComplianceEvidenceDocument>().AsNoTracking().Where(item => item.TenantId == tenantId)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Skip(skip).Take(take)
            .Select(item => new
            {
                item.Id, item.TemplateId, item.Name, item.Type, item.MediaType, ContentLength = item.Content.Length,
                item.ContentSha256, item.SourceUri, item.ValidFromUtc, item.ValidUntilUtc, item.ControlIdsJson,
                item.Review, item.UploadedByUserId, item.ReviewedByUserId, item.ReviewedAtUtc, item.ReviewNotes, item.Revision
            }).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(item => new ComplianceDocumentResponse(item.Id, item.TemplateId, item.Name, item.Type, item.MediaType,
            item.ContentLength, item.ContentSha256, item.SourceUri, item.ValidFromUtc, item.ValidUntilUtc,
            CompliancePackagingSerialization.Deserialize<List<string>>(item.ControlIdsJson), item.Review, item.UploadedByUserId,
            item.ReviewedByUserId, item.ReviewedAtUtc, item.ReviewNotes, item.Revision)).ToArray();
    }

    public async Task SaveDocumentReviewAsync(ComplianceEvidenceDocument document, CancellationToken cancellationToken)
    {
        try { await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false); }
        catch (DbUpdateConcurrencyException) { throw new CompliancePackagingConcurrencyException(); }
    }

    public async Task AddPackageAsync(ComplianceSealedPackage package, CancellationToken cancellationToken)
    {
        context.Set<ComplianceSealedPackage>().Add(package);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<ComplianceSealedPackage?> GetPackageAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        context.Set<ComplianceSealedPackage>().AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CompliancePackageSummary>> ListPackagesAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken) =>
        await context.Set<ComplianceSealedPackage>().AsNoTracking().Where(item => item.TenantId == tenantId)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Skip(skip).Take(take)
            .Select(item => new CompliancePackageSummary(item.Id, item.Name, item.TemplateId, item.PreparedByUserId,
                item.CreatedAt, item.PeriodStartUtc, item.PeriodEndUtc, item.ReadyForAuditorReview,
                item.GapCount, item.ArtifactLength, item.ArtifactSha256, item.SigningKeyId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
}

internal static class CompliancePackagingSerialization
{
    internal static string Serialize<T>(T value) => System.Text.Encoding.UTF8.GetString(CompliancePackagingEncoding.Serialize(value));
    internal static T Deserialize<T>(string value) => System.Text.Json.JsonSerializer.Deserialize<T>(value, CompliancePackagingEncoding.JsonOptions)
        ?? throw new CompliancePackagingIntegrityException();
}
