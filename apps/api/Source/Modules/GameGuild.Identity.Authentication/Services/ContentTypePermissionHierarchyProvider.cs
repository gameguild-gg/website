using GameGuild.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Authorization-module hierarchy contract implemented over the registered
///     <see cref="ContentTypePermission"/> rows (issue #314).
/// </summary>
/// <remarks>
///     <para>
///         Only <c>IsRegistered</c> rows act as hierarchy nodes. The child→parent map is
///         built per lookup from the stored <c>ParentContentTypeName</c> values and walked
///         upward with cycle protection: the first revisit terminates the chain, and the
///         traversal depth is capped so a long (or maliciously cyclic) hierarchy cannot
///         stall resolution. Fail-closed: any ambiguity contributes no ancestors.
///     </para>
///     <para>
///         Lookup order mirrors the grant store's scoping: the tenant's own registered
///         types first, then tenant-independent (global) registrations, so a type
///         registered for the tenant overrides a global registration of the same name.
///     </para>
/// </remarks>
public sealed class ContentTypePermissionHierarchyProvider(
    IApplicationDbContext dbContext,
    ILogger<ContentTypePermissionHierarchyProvider> logger
) : IContentTypeHierarchyProvider
{
    private const int MaxDepth = 16;

    public async Task<IReadOnlyList<string>> GetAncestorsAsync(
        string contentTypeName,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentTypeName))
        {
            return [];
        }

        // Registered hierarchy nodes: tenant-scoped rows first (they win the dictionary),
        // then tenant-independent rows. Unregistered rows never participate.
        var nodes = await dbContext.Set<ContentTypePermission>()
            .AsNoTracking()
            .Where(row => row.IsRegistered && row.ParentContentTypeName != null)
            .Select(row => new { row.TenantId, row.ContentTypeName, row.ParentContentTypeName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var childToParent = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in nodes.Where(n => n.TenantId is null))
        {
            // Tenant-independent registrations first; tenant-scoped rows below override them.
            childToParent.TryAdd(node.ContentTypeName, node.ParentContentTypeName!);
        }

        foreach (var node in nodes.Where(n => tenantId.HasValue && n.TenantId == tenantId.Value))
        {
            childToParent[node.ContentTypeName] = node.ParentContentTypeName!;
        }

        var ancestors = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { contentTypeName };
        var current = contentTypeName;

        for (var depth = 0; depth < MaxDepth; depth++)
        {
            if (!childToParent.TryGetValue(current, out var parent) || string.IsNullOrWhiteSpace(parent))
            {
                break;
            }

            if (!visited.Add(parent))
            {
                logger.LogWarning(
                    "Content-type hierarchy cycle detected at {ContentType} -> {Parent}; traversal stopped (fail-closed).",
                    current, parent);
                break;
            }

            ancestors.Add(parent);
            current = parent;
        }

        return ancestors;
    }
}
