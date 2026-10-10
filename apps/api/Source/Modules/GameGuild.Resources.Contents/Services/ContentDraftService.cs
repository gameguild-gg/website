using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Resources.Contents;

/// <summary>
/// Handles draft lifecycle: creation, updates, rollback, and archival.
/// </summary>
public class ContentDraftService(
    IApplicationDbContext db,
    IActorContextAccessor actorContextAccessor,
    ILogger<ContentDraftService> logger) : IContentDraftService
{
    private Guid GetCurrentUserId() => actorContextAccessor.ActorContext.SubjectIdAsGuid ?? Guid.Empty;

    public async Task<Result<ContentVersion>> CreateDraftAsync(
        Guid entityId,
        string entityType,
        string title,
        Guid createdBy,
        string? summary = null,
        string? body = null,
        string? metadata = null,
        string? changeNotes = null,
        CancellationToken ct = default)
    {
        var maxVersion = await db.Set<ContentVersion>()
            .Where(v => v.EntityId == entityId && v.EntityType == entityType && !v.IsDeleted)
            .MaxAsync(v => (int?)v.VersionNumber, ct).ConfigureAwait(false) ?? 0;

        var version = ContentVersion.Create(
            entityId,
            entityType,
            maxVersion + 1,
            title,
            createdBy,
            summary,
            body,
            metadata,
            changeNotes);

        db.Set<ContentVersion>().Add(version);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Created draft version {VersionNumber} for {EntityType}:{EntityId}",
            version.VersionNumber, entityType, entityId);

        return Result.Success(version);
    }

    public async Task<Result<ContentVersion>> UpdateDraftAsync(
        Guid versionId,
        string? title = null,
        string? summary = null,
        string? body = null,
        string? metadata = null,
        string? changeNotes = null,
        CancellationToken ct = default)
    {
        var version = await db.Set<ContentVersion>()
            .FirstOrDefaultAsync(v => v.Id == versionId && !v.IsDeleted, ct).ConfigureAwait(false);

        if (version == null)
        {
            return Result.Failure<ContentVersion>(ContentVersioningErrors.NotFound);
        }

        if (version.Status != ContentVersionStatus.Draft)
        {
            return Result.Failure<ContentVersion>(ContentVersioningErrors.CanOnlyUpdateDrafts);
        }

        version.UpdateDraft(title, summary, body, metadata, changeNotes);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Updated draft version {VersionId}", versionId);

        return Result.Success(version);
    }

    public async Task<Result<ContentVersion>> GetDraftAsync(Guid entityId, string entityType, CancellationToken ct = default)
    {
        var draft = await db.Set<ContentVersion>()
            .Where(v => v.EntityId == entityId && v.EntityType == entityType && !v.IsDeleted)
            .Where(v => v.Status == ContentVersionStatus.Draft)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        if (draft == null)
        {
            return Result.Failure<ContentVersion>(ContentVersioningErrors.NotFound);
        }

        return Result.Success(draft);
    }

    public async Task<Result<ContentVersion>> RollbackAsync(
        Guid entityId,
        string entityType,
        int targetVersionNumber,
        string? reason = null,
        CancellationToken ct = default)
    {
        var targetVersion = await db.Set<ContentVersion>()
            .FirstOrDefaultAsync(v =>
                v.EntityId == entityId &&
                v.EntityType == entityType &&
                v.VersionNumber == targetVersionNumber &&
                !v.IsDeleted, ct).ConfigureAwait(false);

        if (targetVersion == null)
        {
            return Result.Failure<ContentVersion>(ContentVersioningErrors.NotFound);
        }

        var rollbackResult = await CreateDraftAsync(
            entityId,
            entityType,
            targetVersion.Title,
            GetCurrentUserId(),
            targetVersion.Summary,
            targetVersion.Body,
            targetVersion.Metadata,
            $"Rollback to v{targetVersionNumber}" + (reason != null ? $": {reason}" : ""),
            ct).ConfigureAwait(false);

        if (!rollbackResult.IsSuccess)
        {
            return rollbackResult;
        }

        logger.LogInformation("Created rollback version from v{TargetVersion} for {EntityType}:{EntityId}",
            targetVersionNumber, entityType, entityId);

        return rollbackResult;
    }

    public async Task<Result<int>> ArchiveOldVersionsAsync(
        Guid entityId,
        string entityType,
        int keepCount = 10,
        CancellationToken ct = default)
    {
        var versionsToArchive = await db.Set<ContentVersion>()
            .Where(v => v.EntityId == entityId && v.EntityType == entityType && !v.IsDeleted)
            .Where(v => v.Status == ContentVersionStatus.Published || v.Status == ContentVersionStatus.Archived)
            .Where(v => !v.IsCurrentVersion)
            .OrderByDescending(v => v.VersionNumber)
            .Skip(keepCount)
            .ToListAsync(ct).ConfigureAwait(false);

        foreach (var version in versionsToArchive)
        {
            version.Archive();
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Archived {Count} old versions for {EntityType}:{EntityId}",
            versionsToArchive.Count, entityType, entityId);

        return Result.Success(versionsToArchive.Count);
    }
}
