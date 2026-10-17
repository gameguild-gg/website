using System.Text.RegularExpressions;
using GameGuild.Assets;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Social.Blog.Services;

/// <summary>
/// Treats the asset:// URIs in Content/JsonBody as the authoritative manifest
/// (Learning precedent) and enforces the safe-to-serve gate at publish time.
/// </summary>
public sealed class BlogAssetManifestService(IApplicationDbContext db)
{
    private static readonly Regex AssetUriPattern = new(
        @"asset://(?<id>[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Fails closed (missing asset or not safe-to-serve throws naming the asset id);
    /// returns the resolved OG image URL when <paramref name="ogImageAssetReferenceId"/> is set.
    /// </summary>
    public async Task<string?> ValidatePublishAssetsAsync(
        BlogPost post,
        Guid actorUserId,
        Guid? ogImageAssetReferenceId,
        CancellationToken ct = default)
    {
        var assetIds = ExtractAssetIds(post.Content, post.JsonBody);
        if (ogImageAssetReferenceId is { } ogId)
        {
            assetIds.Add(ogId);
        }

        if (assetIds.Count == 0)
        {
            return null;
        }

        var references = await db.Set<AssetReference>()
            .Include(reference => reference.Content)
            .Where(reference => reference.DeletedAt == null && assetIds.Contains(reference.Id))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var id in assetIds)
        {
            var reference = references.FirstOrDefault(reference => reference.Id == id);
            if (reference is null)
            {
                throw new InvalidOperationException($"Blog post references asset {id} which is not available.");
            }

            if (reference.Content?.IsSafeToServe != true)
            {
                throw new InvalidOperationException(
                    $"Blog post asset {id} has not completed security review and cannot be published.");
            }
        }

        if (ogImageAssetReferenceId is not { } ogRefId)
        {
            return null;
        }

        if (references.Single(reference => reference.Id == ogRefId).CreatedByUserId != actorUserId)
        {
            throw new InvalidOperationException($"Blog post OG image asset {ogRefId} is not owned by the publisher.");
        }

        return $"/api/assets/{ogRefId}/content";
    }

    internal static HashSet<Guid> ExtractAssetIds(string? content, string? jsonBody)
    {
        var ids = new HashSet<Guid>();
        Collect(content, ids);
        Collect(jsonBody, ids);
        return ids;
    }

    private static void Collect(string? payload, HashSet<Guid> ids)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return;
        }

        foreach (Match match in AssetUriPattern.Matches(payload))
        {
            if (Guid.TryParse(match.Groups["id"].Value, out var id))
            {
                ids.Add(id);
            }
        }
    }
}
