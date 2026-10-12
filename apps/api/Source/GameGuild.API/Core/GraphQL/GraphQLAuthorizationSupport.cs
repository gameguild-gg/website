using System.Collections.Concurrent;
using System.Reflection;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using HotChocolate;
using HotChocolate.Resolvers;
using Microsoft.Extensions.Logging;

namespace GameGuild.API.Core.GraphQL;

/// <summary>
///     Shared support for the GraphQL authorization framework (issue #335): request-scoped
///     effective-permission caching, resource-id resolution from GraphQL arguments or the
///     parent value, role attribution and durable evaluation logging.
/// </summary>
internal static class GraphQLAuthorizationSupport
{
    public const string PermissionEvaluationLogSource = "graphql";
    public const string SchemaResourceType = "Schema";
    public const string TenantResourceType = "Tenant";

    private const string PermissionResolutionCacheKey = "GameGuild.API.GraphQL.EffectivePermissions";
    private const string RoleAttributionCacheKey = "GameGuild.API.GraphQL.RoleAttribution";

    /// <summary>
    ///     Resolves the effective permissions for one authorization context, cached per
    ///     request so repeated checks for the same user/tenant/resource pay once. This is
    ///     the integration point with the effective-permission engine (issue #330 contract,
    ///     owner override #372, temporal precedence #363): a lapsed ownership bound fails
    ///     closed inside the engine and the permission simply is not minted.
    /// </summary>
    public static async Task<EffectivePermissions> ResolveEffectivePermissionsAsync(
        IResolverContext resolverContext,
        EffectivePermissionContext permissionContext,
        CancellationToken cancellationToken)
    {
        var cache = resolverContext.GetOrSetGlobalState(
            PermissionResolutionCacheKey,
            static _ => new ConcurrentDictionary<EffectivePermissionContext, Lazy<Task<EffectivePermissions>>>());
        var entry = cache.GetOrAdd(
            permissionContext,
            context => new Lazy<Task<EffectivePermissions>>(
                () => resolverContext.Service<IEffectivePermissionResolver>().ResolveAsync(context, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return await entry.Value.ConfigureAwait(false);
        }
        catch
        {
            cache.TryRemove(permissionContext, out _);
            throw;
        }
    }

    /// <summary>
    ///     Resolves the acting user's role attribution for evaluation logging (issue #359)
    ///     from the RBAC resolver, cached per request. Anonymous or tenant-less actors
    ///     resolve to an empty attribution; failures are best-effort (empty) and never
    ///     change the decision.
    /// </summary>
    public static async Task<IReadOnlyCollection<string>> ResolveActorRolesAsync(
        IResolverContext resolverContext,
        ActorContext actor)
    {
        if (actor.SubjectIdAsGuid is not { } userId || userId == Guid.Empty || actor.TenantId is not { } tenantId)
        {
            return PermissionEvaluationRoles.Empty;
        }

        var roleCache = resolverContext.GetOrSetGlobalState(
            RoleAttributionCacheKey,
            static _ => new ConcurrentDictionary<(Guid UserId, Guid TenantId), Lazy<Task<IReadOnlyCollection<string>>>>());
        var cacheKey = (userId, tenantId);
        var attribution = roleCache.GetOrAdd(
            cacheKey,
            _ => new Lazy<Task<IReadOnlyCollection<string>>>(
                () => ResolveActorRolesCoreAsync(resolverContext, userId, tenantId),
                LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return await attribution.Value.ConfigureAwait(false);
        }
        catch
        {
            roleCache.TryRemove(cacheKey, out _);
            return PermissionEvaluationRoles.Empty;
        }
    }

    private static async Task<IReadOnlyCollection<string>> ResolveActorRolesCoreAsync(
        IResolverContext resolverContext,
        Guid userId,
        Guid tenantId)
    {
        try
        {
            // Null-safe so hosts that do not register the RBAC resolver (tests, minimal
            // probes) still record evaluations with an empty attribution instead of a
            // resolution error. Best-effort by contract: failures never change the decision.
            if (resolverContext.Service<IServiceProvider>()
                .GetService(typeof(IRbacPermissionResolver)) is not IRbacPermissionResolver rbacResolver)
            {
                return PermissionEvaluationRoles.Empty;
            }

            var rbacResult = await rbacResolver
                .ResolvePermissionsAsync(userId, tenantId, resolverContext.RequestAborted)
                .ConfigureAwait(false);
            return PermissionEvaluationRoles.FromContributions(rbacResult.RoleContributions);
        }
        catch (OperationCanceledException) when (resolverContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return PermissionEvaluationRoles.Empty;
        }
    }

    /// <summary>
    ///     Records one evaluation observation through the durable log service (issue #359).
    ///     Recording never changes the decision and never throws (except cancellation).
    /// </summary>
    public static async Task RecordEvaluationAsync(
        IResolverContext resolverContext,
        ILogger logger,
        string fieldName,
        string resourceType,
        string? resourceId,
        IReadOnlyCollection<string> requiredPermissions,
        PermissionEvaluationOutcome outcome,
        string? reason = null)
    {
        try
        {
            var actor = resolverContext.Service<IActorContextAccessor>().ActorContext;
            var roles = await ResolveActorRolesAsync(resolverContext, actor).ConfigureAwait(false);
            await resolverContext.Service<IPermissionEvaluationLogService>().RecordAsync(
                new PermissionEvaluationRecord(
                    actor.SubjectIdAsGuid,
                    actor.TenantId,
                    roles,
                    resourceType,
                    resourceId,
                    requiredPermissions,
                    outcome,
                    PermissionEvaluationLogSource,
                    fieldName,
                    reason),
                resolverContext.RequestAborted).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (resolverContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to record GraphQL permission evaluation for field {FieldName}.",
                fieldName);
        }
    }

    /// <summary>
    ///     Resolves a resource id from a dotted GraphQL argument path (for example
    ///     <c>input.projectId</c>) or from a property path on the parent resolver value
    ///     (for nested fields). Returns null when the id cannot be resolved; callers
    ///     treat that as fail-closed.
    /// </summary>
    public static Guid? TryResolveResourceId(
        IResolverContext context,
        string? argumentPath,
        string? parentPropertyPath)
    {
        if (parentPropertyPath is null && string.IsNullOrWhiteSpace(argumentPath))
        {
            return null;
        }

        var path = (parentPropertyPath ?? argumentPath)!
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (path.Length == 0)
        {
            return null;
        }

        object? value;
        if (parentPropertyPath is not null)
        {
            try
            {
                value = context.Parent<object>();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
        else
        {
            try
            {
                value = context.ArgumentValue<object?>(path[0]);
            }
            catch (InvalidOperationException)
            {
                return null;
            }

            path = path.Skip(1).ToArray();
        }

        foreach (var segment in path)
        {
            value = ReadMember(value, segment);
            if (value is null)
            {
                return null;
            }
        }

        return value switch
        {
            Guid id => id,
            string text when Guid.TryParse(text, out var id) => id,
            _ => null,
        };
    }

    private static object? ReadMember(object? value, string memberName)
    {
        if (value is null)
        {
            return null;
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            var pair = readOnlyDictionary.FirstOrDefault(candidate =>
                string.Equals(candidate.Key, memberName, StringComparison.OrdinalIgnoreCase));
            return pair.Key is null ? null : pair.Value;
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            var pair = dictionary.FirstOrDefault(candidate =>
                string.Equals(candidate.Key, memberName, StringComparison.OrdinalIgnoreCase));
            return pair.Key is null ? null : pair.Value;
        }

        return value.GetType()
            .GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
            ?.GetValue(value);
    }
}
