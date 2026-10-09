using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using GameGuild.Assets;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Learning.Courses;

public interface ILearningAssetManifestService
{
    Task ValidateAndReconcileAsync(
        ProgramContent content,
        AuthoringContentPayload payload,
        bool publishing,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Treats the asset URIs in the draft/published payload as the authoritative manifest.
/// This avoids a second mutable list that could drift from the actual lesson document.
/// </summary>
public sealed class LearningAssetManifestService(IApplicationDbContext db)
    : ILearningAssetManifestService, IAssetUsageGuard
{
    private static readonly Regex AssetUriPattern = new(
        @"asset://(?<id>[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public async Task ValidateAndReconcileAsync(
        ProgramContent content,
        AuthoringContentPayload payload,
        bool publishing,
        CancellationToken cancellationToken = default)
    {
        var assetIds = ExtractAssetIds(JsonSerializer.Serialize(payload));
        if (assetIds.Count == 0 && !publishing)
        {
            return;
        }

        var referenceQuery = db.Set<AssetReference>()
            .Include(reference => reference.Content)
            .Where(reference => reference.DeletedAt == null);
        referenceQuery = publishing
            ? referenceQuery.Where(reference =>
                reference.TenantId == content.TenantId &&
                reference.ParentResourceId == content.Id &&
                reference.ParentResourceType == nameof(ProgramContent))
            : referenceQuery.Where(reference => assetIds.Contains(reference.Id));

        var references = await referenceQuery
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var referencedAssets = references.Where(reference => assetIds.Contains(reference.Id)).ToArray();
        if (referencedAssets.Length != assetIds.Count)
        {
            throw new ValidationException("One or more assets are not available in this lesson.");
        }

        foreach (var reference in referencedAssets)
        {
            if (reference.TenantId != content.TenantId ||
                reference.ParentResourceId != content.Id ||
                !string.Equals(reference.ParentResourceType, nameof(ProgramContent), StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("One or more assets are not available in this lesson.");
            }

            if (!publishing)
            {
                continue;
            }

            if (reference.Content.VirusScanStatus != VirusScanStatus.Clean ||
                reference.Content.ModerationStatus is not (ModerationStatus.Approved or ModerationStatus.ApprovedWithWarning))
            {
                throw new ValidationException("Every lesson asset must finish security review before publishing.");
            }
        }

        if (!publishing)
        {
            return;
        }

        foreach (var reference in references)
        {
            var intendedPolicy = assetIds.Contains(reference.Id)
                ? AssetAccessPolicy.Inherited
                : AssetAccessPolicy.Private;
            if (reference.AccessPolicy == intendedPolicy)
            {
                continue;
            }

            reference.AccessPolicy = intendedPolicy;
            reference.Touch();
        }
    }

    public async Task<bool> IsInUseAsync(
        Guid assetReferenceId,
        CancellationToken cancellationToken = default)
    {
        var uri = $"asset://{assetReferenceId}";
        if (db is DbContext efContext &&
            efContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            var likePattern = $"%{uri}%";
            var referencedByPublishedContent = await db.Set<ProgramContent>()
                .FromSqlInterpolated($$"""
                    SELECT * FROM "program_contents"
                    WHERE "DeletedAt" IS NULL
                      AND (("Body" IS NOT NULL AND "Body" LIKE {{likePattern}})
                        OR ("JsonBody" IS NOT NULL AND "JsonBody"::text LIKE {{likePattern}}))
                    """)
                .AsNoTracking()
                .AnyAsync(cancellationToken)
                .ConfigureAwait(false);
            if (referencedByPublishedContent)
            {
                return true;
            }

            return await db.Set<ProgramContentDraft>()
                .FromSqlInterpolated($$"""
                    SELECT * FROM "program_content_drafts"
                    WHERE "DeletedAt" IS NULL
                      AND "PayloadJson"::text LIKE {{likePattern}}
                    """)
                .AsNoTracking()
                .AnyAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var publishedPayloads = await db.Set<ProgramContent>()
            .AsNoTracking()
            .Where(content => content.DeletedAt == null &&
                              ((content.Body != null && content.Body.Contains(uri)) ||
                               (content.JsonBody != null && content.JsonBody.Contains(uri))))
            .Select(content => new { content.Body, content.JsonBody })
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
        if (publishedPayloads)
        {
            return true;
        }

        return await db.Set<ProgramContentDraft>()
            .AsNoTracking()
            .AnyAsync(draft => draft.DeletedAt == null && draft.PayloadJson.Contains(uri), cancellationToken)
            .ConfigureAwait(false);
    }

    internal static IReadOnlySet<Guid> ExtractAssetIds(string payload)
    {
        var ids = new HashSet<Guid>();
        foreach (Match match in AssetUriPattern.Matches(payload))
        {
            if (Guid.TryParse(match.Groups["id"].Value, out var id))
            {
                ids.Add(id);
            }
        }
        return ids;
    }
}
