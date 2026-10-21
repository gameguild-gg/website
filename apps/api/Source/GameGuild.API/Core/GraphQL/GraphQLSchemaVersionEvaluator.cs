using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using HotChocolate;
using HotChocolate.Resolvers;
using HotChocolate.Types;

namespace GameGuild.API.Core.GraphQL;

/// <summary>
///     The caller's effective schema surface tier (issue #335). HotChocolate compiles one
///     static schema per process, so tiers describe the caller's effective surface rather
///     than distinct compiled schemas.
/// </summary>
public enum GraphQLSchemaVisibilityTier
{
    /// <summary>
    ///     The caller can see every <c>@authorize</c>-protected field (schema
    ///     administrators, or callers whose permission set satisfies every
    ///     planning-decidable rule).
    /// </summary>
    Full = 0,

    /// <summary>
    ///     At least one protected field is not provably visible to the caller (their
    ///     effective surface is restricted: protected operations are hidden at planning
    ///     time and introspection is denied).
    /// </summary>
    Restricted = 1,
}

/// <summary>
///     The permission-level schema version returned by the <c>schemaVersion</c> query
///     field (issue #335): a deterministic revision of the caller's effective schema
///     surface plus the visibility tier and introspection eligibility. Clients use the
///     revision to detect when a permission change altered their effective surface.
/// </summary>
/// <param name="Revision">
///     Deterministic revision tag: SHA-256 (16 hex characters) over the schema's protected
///     surface manifest, the caller's visibility tier and introspection eligibility. Two
///     callers with the same permission level resolve the same revision; the revision
///     changes when the protected surface or the caller's level changes.
/// </param>
/// <param name="VisibilityTier">The caller's effective schema surface tier.</param>
/// <param name="IntrospectionAllowed">Whether this caller may introspect the schema.</param>
public sealed record GraphQLSchemaVersion(
    string Revision,
    GraphQLSchemaVisibilityTier VisibilityTier,
    bool IntrospectionAllowed);

/// <summary>
///     Permission-level schema versioning (issue #335). HotChocolate builds one static
///     schema per process, so per-user compiled schemas are not constructible without
///     forking the platform; this evaluator delivers the closest honest contract: a
///     deterministic revision of the caller's effective surface, computed from the schema's
///     protected-field manifest and the caller's resolved permission level (schema-admin
///     bypass and full visibility produce the Full tier; anything else is Restricted).
/// </summary>
internal sealed class GraphQLSchemaVersionEvaluator(GraphQLSchemaVisibilityEvaluator visibilityEvaluator)
{
    private const string RevisionFormatTag = "gg-graphql-schema-v1";

    private static readonly ConcurrentDictionary<ISchema, string> ManifestCache = new();

    /// <summary>Evaluates the caller's schema version: revision, tier, introspection eligibility.</summary>
    public async Task<GraphQLSchemaVersion> EvaluateAsync(
        IResolverContext resolverContext,
        CancellationToken cancellationToken)
    {
        var visibility = await visibilityEvaluator.EvaluateCallerVisibilityAsync(resolverContext, cancellationToken)
            .ConfigureAwait(false);
        var introspection = await visibilityEvaluator.AuthorizeIntrospectionAsync(resolverContext, cancellationToken)
            .ConfigureAwait(false);

        var tier = visibility.Allowed
            ? GraphQLSchemaVisibilityTier.Full
            : GraphQLSchemaVisibilityTier.Restricted;
        var manifest = ManifestCache.GetOrAdd(resolverContext.Schema, static schema => BuildManifest(schema));
        var revision = ComputeRevision(manifest, tier, introspection.Allowed);
        return new GraphQLSchemaVersion(revision, tier, introspection.Allowed);
    }

    /// <summary>
    ///     Builds the schema's protected-surface manifest: the sorted field coordinates of
    ///     every <c>@authorize</c>-protected field with its rule scope class (resource or
    ///     planning-decidable). The manifest is cached per compiled schema; it contains no
    ///     caller information and is never exposed to callers — only its hash is.
    /// </summary>
    private static string BuildManifest(ISchema schema)
    {
        var entries = GraphQLAuthorizeFieldRules.CollectProtectedFields(schema)
            .SelectMany(entry => entry.Rules.Select(rule =>
                $"{entry.Field.DeclaringType.Name}.{entry.Field.Name}|{(rule.IsResourceScoped ? "resource" : "decidable")}"))
            .Order(StringComparer.Ordinal)
            .ToList();
        return string.Join(";", entries);
    }

    private static string ComputeRevision(
        string manifest,
        GraphQLSchemaVisibilityTier tier,
        bool introspectionAllowed)
    {
        var input = $"{RevisionFormatTag}|{(int)tier}|{introspectionAllowed}|{manifest}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash, 0, 8);
    }
}

/// <summary>
///     Host-owned query extension publishing the permission-level schema version. The
///     field itself is not protected: it exposes only the caller's own revision, tier and
///     introspection eligibility — never a field inventory.
/// </summary>
[ExtendObjectType("Query")]
public sealed class GraphQLSchemaVersionQueries
{
    /// <summary>
    ///     The permission-level schema version for the current caller: a deterministic
    ///     revision of the caller's effective schema surface (issue #335). The internal
    ///     evaluator is resolved from the request services so it never surfaces in the
    ///     public resolver signature.
    /// </summary>
    public Task<GraphQLSchemaVersion> SchemaVersionAsync(
        IResolverContext resolverContext,
        CancellationToken cancellationToken)
        => resolverContext.Service<GraphQLSchemaVersionEvaluator>()
            .EvaluateAsync(resolverContext, cancellationToken);
}
