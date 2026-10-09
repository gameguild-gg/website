using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Resources.Contents;

/// <summary>
/// Handles review workflow and publishing operations.
/// </summary>
public class ContentReviewPublishingService(
    IApplicationDbContext db,
    IActorContextAccessor actorContextAccessor,
    ILogger<ContentReviewPublishingService> logger) : IContentReviewPublishingService
{
    private Guid GetCurrentUserId() => actorContextAccessor.ActorContext.SubjectIdAsGuid ?? Guid.Empty;

    public async Task<Result<ContentVersion>> SubmitForReviewAsync(Guid versionId, CancellationToken ct = default)
    {
        var version = await db.Set<ContentVersion>()
            .FirstOrDefaultAsync(v => v.Id == versionId && !v.IsDeleted, ct).ConfigureAwait(false);

        if (version == null)
        {
            return Result.Failure<ContentVersion>(ContentVersioningErrors.NotFound);
        }

        try
        {
            version.SubmitForReview(GetCurrentUserId());
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            logger.LogInformation("Version {VersionId} submitted for review", versionId);
            return Result.Success(version);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<ContentVersion>(Error.Failure("ContentVersioning.InvalidOperation", ex.Message));
        }
    }

    public async Task<Result<ContentVersion>> ApproveAsync(Guid versionId, string? reviewNotes = null, CancellationToken ct = default)
    {
        var version = await db.Set<ContentVersion>()
            .FirstOrDefaultAsync(v => v.Id == versionId && !v.IsDeleted, ct).ConfigureAwait(false);

        if (version == null)
        {
            return Result.Failure<ContentVersion>(ContentVersioningErrors.NotFound);
        }

        try
        {
            version.Approve(GetCurrentUserId(), reviewNotes);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            logger.LogInformation("Version {VersionId} approved", versionId);
            return Result.Success(version);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<ContentVersion>(Error.Failure("ContentVersioning.InvalidOperation", ex.Message));
        }
    }

    public async Task<Result<ContentVersion>> RejectAsync(Guid versionId, string? reviewNotes = null, CancellationToken ct = default)
    {
        var version = await db.Set<ContentVersion>()
            .FirstOrDefaultAsync(v => v.Id == versionId && !v.IsDeleted, ct).ConfigureAwait(false);

        if (version == null)
        {
            return Result.Failure<ContentVersion>(ContentVersioningErrors.NotFound);
        }

        try
        {
            version.Reject(GetCurrentUserId(), reviewNotes);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            logger.LogInformation("Version {VersionId} rejected", versionId);
            return Result.Success(version);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<ContentVersion>(Error.Failure("ContentVersioning.InvalidOperation", ex.Message));
        }
    }

    public async Task<Result<IEnumerable<ContentVersion>>> GetPendingReviewAsync(
        string? entityType = null,
        int skip = 0,
        int take = 20,
        CancellationToken ct = default)
    {
        var query = db.Set<ContentVersion>()
            .Where(v => !v.IsDeleted)
            .Where(v => v.Status == ContentVersionStatus.PendingReview);

        if (!string.IsNullOrEmpty(entityType))
        {
            query = query.Where(v => v.EntityType == entityType);
        }

        var versions = await query
            .OrderBy(v => v.SubmittedForReviewAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct).ConfigureAwait(false);

        return Result.Success<IEnumerable<ContentVersion>>(versions);
    }

    public async Task<Result<ContentVersionReview>> AddReviewAsync(
        Guid versionId,
        ContentReviewDecision decision,
        string? feedback = null,
        string? suggestions = null,
        CancellationToken ct = default)
    {
        var version = await db.Set<ContentVersion>()
            .FirstOrDefaultAsync(v => v.Id == versionId && !v.IsDeleted, ct).ConfigureAwait(false);

        if (version == null)
        {
            return Result.Failure<ContentVersionReview>(ContentVersioningErrors.NotFound);
        }

        var review = ContentVersionReview.Create(
            versionId,
            GetCurrentUserId(),
            decision,
            feedback,
            suggestions);

        db.Set<ContentVersionReview>().Add(review);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success(review);
    }

    public async Task<Result<ContentVersion>> PublishAsync(Guid versionId, CancellationToken ct = default)
    {
        var version = await db.Set<ContentVersion>()
            .FirstOrDefaultAsync(v => v.Id == versionId && !v.IsDeleted, ct).ConfigureAwait(false);

        if (version == null)
        {
            return Result.Failure<ContentVersion>(ContentVersioningErrors.NotFound);
        }

        try
        {
            var currentVersions = await db.Set<ContentVersion>()
                .Where(v => v.EntityId == version.EntityId && v.EntityType == version.EntityType && !v.IsDeleted)
                .Where(v => v.IsCurrentVersion)
                .ToListAsync(ct).ConfigureAwait(false);

            foreach (var cv in currentVersions)
            {
                cv.SetAsCurrent(false);
            }

            version.Publish(GetCurrentUserId());
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            logger.LogInformation("Version {VersionId} published for {EntityType}:{EntityId}",
                versionId, version.EntityType, version.EntityId);

            return Result.Success(version);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<ContentVersion>(Error.Failure("ContentVersioning.InvalidOperation", ex.Message));
        }
    }

    public async Task<Result<ContentVersion>> SchedulePublishAsync(Guid versionId, DateTime scheduledAt, CancellationToken ct = default)
    {
        var version = await db.Set<ContentVersion>()
            .FirstOrDefaultAsync(v => v.Id == versionId && !v.IsDeleted, ct).ConfigureAwait(false);

        if (version == null)
        {
            return Result.Failure<ContentVersion>(ContentVersioningErrors.NotFound);
        }

        if (scheduledAt <= SystemClock.UtcNow)
        {
            return Result.Failure<ContentVersion>(ContentVersioningErrors.ScheduleDateMustBeFuture);
        }

        try
        {
            version.SchedulePublish(scheduledAt, GetCurrentUserId());
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            logger.LogInformation("Version {VersionId} scheduled for publishing at {ScheduledAt}", versionId, scheduledAt);

            return Result.Success(version);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<ContentVersion>(Error.Failure("ContentVersioning.InvalidOperation", ex.Message));
        }
    }

    public async Task<Result<ContentVersion>> CancelScheduledPublishAsync(Guid versionId, CancellationToken ct = default)
    {
        var version = await db.Set<ContentVersion>()
            .FirstOrDefaultAsync(v => v.Id == versionId && !v.IsDeleted, ct).ConfigureAwait(false);

        if (version == null)
        {
            return Result.Failure<ContentVersion>(ContentVersioningErrors.NotFound);
        }

        if (version.Status != ContentVersionStatus.Scheduled)
        {
            return Result.Failure<ContentVersion>(ContentVersioningErrors.NotScheduled);
        }

        version.Approve(GetCurrentUserId(), "Scheduled publishing cancelled");
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Scheduled publishing cancelled for version {VersionId}", versionId);

        return Result.Success(version);
    }

    public async Task<Result<int>> ProcessScheduledPublishingAsync(CancellationToken ct = default)
    {
        var now = SystemClock.UtcNow;

        var scheduledVersions = await db.Set<ContentVersion>()
            .Where(v => !v.IsDeleted)
            .Where(v => v.Status == ContentVersionStatus.Scheduled)
            .Where(v => v.ScheduledPublishAt <= now)
            .ToListAsync(ct).ConfigureAwait(false);

        var publishedCount = 0;

        foreach (var version in scheduledVersions)
        {
            try
            {
                var currentVersions = await db.Set<ContentVersion>()
                    .Where(v => v.EntityId == version.EntityId && v.EntityType == version.EntityType && !v.IsDeleted)
                    .Where(v => v.IsCurrentVersion)
                    .ToListAsync(ct).ConfigureAwait(false);

                foreach (var cv in currentVersions)
                {
                    cv.SetAsCurrent(false);
                }

                version.Publish(version.PublishedBy ?? Guid.Empty);
                publishedCount++;

                logger.LogInformation("Auto-published scheduled version {VersionId} for {EntityType}:{EntityId}",
                    version.Id, version.EntityType, version.EntityId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to auto-publish version {VersionId}", version.Id);
                throw;
            }
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success(publishedCount);
    }
}
