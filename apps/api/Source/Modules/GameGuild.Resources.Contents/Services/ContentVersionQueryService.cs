using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Resources.Contents;

/// <summary>
/// Handles version history queries and version comparison.
/// </summary>
public class ContentVersionQueryService(
    IApplicationDbContext db,
    ILogger<ContentVersionQueryService> logger) : IContentVersionQueryService
{
    public async Task<Result<IEnumerable<ContentVersion>>> GetVersionHistoryAsync(
        Guid entityId,
        string entityType,
        CancellationToken ct = default)
    {
        var versions = await db.Set<ContentVersion>()
            .Where(v => v.EntityId == entityId && v.EntityType == entityType && !v.IsDeleted)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(ct).ConfigureAwait(false);

        return Result.Success<IEnumerable<ContentVersion>>(versions);
    }

    public async Task<Result<ContentVersion>> GetVersionAsync(Guid versionId, CancellationToken ct = default)
    {
        var version = await db.Set<ContentVersion>()
            .FirstOrDefaultAsync(v => v.Id == versionId && !v.IsDeleted, ct).ConfigureAwait(false);

        if (version == null)
        {
            return Result.Failure<ContentVersion>(ContentVersioningErrors.NotFound);
        }

        return Result.Success(version);
    }

    public async Task<Result<ContentVersion>> GetVersionByNumberAsync(
        Guid entityId,
        string entityType,
        int versionNumber,
        CancellationToken ct = default)
    {
        var version = await db.Set<ContentVersion>()
            .FirstOrDefaultAsync(v =>
                v.EntityId == entityId &&
                v.EntityType == entityType &&
                v.VersionNumber == versionNumber &&
                !v.IsDeleted, ct).ConfigureAwait(false);

        if (version == null)
        {
            return Result.Failure<ContentVersion>(ContentVersioningErrors.NotFound);
        }

        return Result.Success(version);
    }

    public async Task<Result<ContentVersion>> GetCurrentVersionAsync(Guid entityId, string entityType, CancellationToken ct = default)
    {
        var version = await db.Set<ContentVersion>()
            .FirstOrDefaultAsync(v =>
                v.EntityId == entityId &&
                v.EntityType == entityType &&
                v.IsCurrentVersion &&
                !v.IsDeleted, ct).ConfigureAwait(false);

        if (version == null)
        {
            return Result.Failure<ContentVersion>(ContentVersioningErrors.NotFound);
        }

        return Result.Success(version);
    }

    public async Task<Result<ContentVersionDiff>> CompareVersionsAsync(
        Guid versionId1,
        Guid versionId2,
        CancellationToken ct = default)
    {
        var v1 = await db.Set<ContentVersion>()
            .FirstOrDefaultAsync(v => v.Id == versionId1 && !v.IsDeleted, ct).ConfigureAwait(false);
        var v2 = await db.Set<ContentVersion>()
            .FirstOrDefaultAsync(v => v.Id == versionId2 && !v.IsDeleted, ct).ConfigureAwait(false);

        if (v1 == null || v2 == null)
        {
            return Result.Failure<ContentVersionDiff>(ContentVersioningErrors.NotFound);
        }

        if (v1.EntityId != v2.EntityId || v1.EntityType != v2.EntityType)
        {
            return Result.Failure<ContentVersionDiff>(ContentVersioningErrors.VersionsMustBeSameEntity);
        }

        var diff = new ContentVersionDiff(
            versionId1,
            versionId2,
            v1.VersionNumber,
            v2.VersionNumber,
            v1.Title != v2.Title,
            v1.Summary != v2.Summary,
            v1.Body != v2.Body,
            v1.Metadata != v2.Metadata,
            v1.Title != v2.Title ? $"'{v1.Title}' -> '{v2.Title}'" : null,
            v1.Summary != v2.Summary ? $"Summary changed" : null,
            v1.Body != v2.Body ? "Body changed" : null
        );

        logger.LogDebug("Compared versions {V1} and {V2} for {EntityType}:{EntityId}",
            v1.VersionNumber, v2.VersionNumber, v1.EntityType, v1.EntityId);

        return Result.Success(diff);
    }
}
