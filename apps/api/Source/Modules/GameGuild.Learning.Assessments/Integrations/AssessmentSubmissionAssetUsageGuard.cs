using System.Text.RegularExpressions;
using GameGuild.Assets;
using GameGuild.Learning.Courses;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Marks an asset reference as in-use while any non-deleted submission payload
/// (FilePayload GUID, asset:// URI, or /api/assets/{id}/content URL) or author-side
/// quiz question payload (ProgramContent.JsonBody asset:// URI) still names it.
/// This guard mirrors BlogAssetUsageGuard: it keys on the raw payload strings
/// without requiring a separate AssetReference table or FK mapping.
/// </summary>
public sealed class AssessmentSubmissionAssetUsageGuard(IApplicationDbContext db) : IAssetUsageGuard
{
    private static readonly Regex AssetUriPattern = new(
        @"asset://(?<id>[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AssetContentUrlPattern = new(
        @"/api/assets/(?<id>[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12})/content",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public async Task<bool> IsInUseAsync(Guid assetReferenceId, CancellationToken cancellationToken = default)
    {
        var id = assetReferenceId.ToString();
        var assetUri = $"asset://{id}";
        var contentUrl = $"/api/assets/{id}/content";

        var submissionsInUse = await db.Set<AssessmentSubmission>()
            .AsNoTracking()
            .AnyAsync(s =>
                s.DeletedAt == null &&
                (
                    s.FilePayload == id ||
                    s.MediaPayload != null && s.MediaPayload.Contains(assetUri) ||
                    s.MediaPayload != null && s.MediaPayload.Contains(contentUrl) ||
                    s.UrlPayload != null && s.UrlPayload.Contains(assetUri) ||
                    s.UrlPayload != null && s.UrlPayload.Contains(contentUrl) ||
                    s.TextPayload != null && s.TextPayload.Contains(assetUri) ||
                    s.TextPayload != null && s.TextPayload.Contains(contentUrl) ||
                    s.CodePayload != null && s.CodePayload.Contains(assetUri) ||
                    s.CodePayload != null && s.CodePayload.Contains(contentUrl) ||
                    s.ProjectPayload != null && s.ProjectPayload.Contains(assetUri) ||
                    s.ProjectPayload != null && s.ProjectPayload.Contains(contentUrl)
                ),
                cancellationToken)
            .ConfigureAwait(false);

        if (submissionsInUse)
        {
            return true;
        }

        var contentInUse = await db.Set<ProgramContent>()
            .AsNoTracking()
            .AnyAsync(c =>
                c.DeletedAt == null &&
                c.JsonBody != null &&
                (
                    c.JsonBody.Contains(assetUri) ||
                    c.JsonBody.Contains(contentUrl)
                ),
                cancellationToken)
            .ConfigureAwait(false);

        return contentInUse;
    }
}