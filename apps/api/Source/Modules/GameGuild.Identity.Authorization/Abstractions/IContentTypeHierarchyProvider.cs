namespace GameGuild.Identity.Authorization;

/// <summary>
///     Supplies the registered content-type hierarchy for effective-permission resolution
///     (issue #314): the ancestor chain of a content type, nearest parent first.
/// </summary>
/// <remarks>
///     <para>
///         The Authorization module defines the contract; the Authentication module
///         implements it over the registered <c>ContentTypePermission</c> rows
///         (<c>ParentContentTypeName</c> + <c>IsRegistered</c>). Only registered types are
///         traversed as parents, and chains are cycle-guarded: a looping chain contributes
///         no ancestors beyond the first revisit (fail-closed — an unresolvable hierarchy
///         never widens permissions).
///     </para>
///     <para>
///         Inheritance flows downward like the rest of the engine: a grant on an ancestor
///         content type applies to its descendants, minted under the descendant's
///         content-type qualified name by the resolver. Nothing ever flows upward.
///     </para>
/// </remarks>
public interface IContentTypeHierarchyProvider
{
    /// <summary>
    ///     Returns the ancestor chain of <paramref name="contentTypeName"/> (excluding the
    ///     type itself), nearest parent first. Empty when the type has no registered
    ///     parents or is unknown.
    /// </summary>
    /// <param name="contentTypeName">The content type whose ancestors are requested.</param>
    /// <param name="tenantId">Optional tenant scope for the hierarchy lookup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<string>> GetAncestorsAsync(
        string contentTypeName,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Fallback used when no hierarchy provider is registered: no ancestors, exact-name
///     semantics only (the pre-#314 behavior).
/// </summary>
public sealed class NoContentTypeHierarchyProvider : IContentTypeHierarchyProvider
{
    public Task<IReadOnlyList<string>> GetAncestorsAsync(
        string contentTypeName,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>([]);
}
