using System.Collections.Concurrent;
using System.Reflection;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.GraphQL;
using GameGuild.Identity.Context.Actors;
using HotChocolate.Resolvers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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

    /// <summary>Bounded error code for authorization denials (no store detail).</summary>
    public const string DeniedErrorCode = "AUTHORIZATION_DENIED";

    /// <summary>Bounded error code for authorization evaluation failures (fail-closed).</summary>
    public const string UnavailableErrorCode = "AUTHORIZATION_UNAVAILABLE";

    private const string PermissionResolutionCacheKey = "GameGuild.API.GraphQL.EffectivePermissions";
    private const string RoleAttributionCacheKey = "GameGuild.API.GraphQL.RoleAttribution";

    /// <summary>
    ///     Resolves the effective permissions for one authorization context, cached per
    ///     request so repeated checks for the same user/tenant/resource pay once. This is
    ///     the integration point with the effective-permission engine (issue #330 contract,
    ///     owner override #372, temporal precedence #363): a lapsed ownership bound fails
    ///     closed inside the engine and the permission simply is not minted.
    /// </summary>
    public static Task<EffectivePermissions> ResolveEffectivePermissionsAsync(
        IResolverContext resolverContext,
        EffectivePermissionContext permissionContext,
        CancellationToken cancellationToken)
        => ResolveEffectivePermissionsAsync(
            resolverContext.Service<IServiceProvider>(),
            resolverContext.ContextData,
            permissionContext,
            cancellationToken);

    /// <summary>
    ///     Provider-based effective-permission resolution with the same per-request cache
    ///     as the resolver-context overload. Used by pipeline components that hold an
    ///     <see cref="IServiceProvider"/> and the request context data instead of a
    ///     resolver context (the operation-planning authorization gate).
    /// </summary>
    public static async Task<EffectivePermissions> ResolveEffectivePermissionsAsync(
        IServiceProvider services,
        IDictionary<string, object?> contextData,
        EffectivePermissionContext permissionContext,
        CancellationToken cancellationToken)
    {
        var cache = GetOrCreateCache(
            contextData, PermissionResolutionCacheKey,
            static _ => new ConcurrentDictionary<EffectivePermissionContext, Lazy<Task<EffectivePermissions>>>());
        var entry = cache.GetOrAdd(
            permissionContext,
            context => new Lazy<Task<EffectivePermissions>>(
                () => services.GetRequiredService<IEffectivePermissionResolver>().ResolveAsync(context, cancellationToken),
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
    public static Task<IReadOnlyCollection<string>> ResolveActorRolesAsync(
        IResolverContext resolverContext,
        ActorContext actor)
        => ResolveActorRolesAsync(
            resolverContext.Service<IServiceProvider>(),
            resolverContext.ContextData,
            actor);

    /// <summary>Provider-based role attribution with the same per-request cache.</summary>
    public static Task<IReadOnlyCollection<string>> ResolveActorRolesAsync(
        IServiceProvider services,
        IDictionary<string, object?> contextData,
        ActorContext actor)
    {
        if (actor.SubjectIdAsGuid is not { } userId || userId == Guid.Empty || actor.TenantId is not { } tenantId)
        {
            return Task.FromResult(PermissionEvaluationRoles.Empty);
        }

        var roleCache = GetOrCreateCache(
            contextData, RoleAttributionCacheKey,
            static _ => new ConcurrentDictionary<(Guid UserId, Guid TenantId), Lazy<Task<IReadOnlyCollection<string>>>>());
        var cacheKey = (userId, tenantId);
        var attribution = roleCache.GetOrAdd(
            cacheKey,
            _ => new Lazy<Task<IReadOnlyCollection<string>>>(
                () => ResolveActorRolesCoreAsync(services, userId, tenantId),
                LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return attribution.Value;
        }
        catch
        {
            roleCache.TryRemove(cacheKey, out _);
            return Task.FromResult(PermissionEvaluationRoles.Empty);
        }
    }

    private static async Task<IReadOnlyCollection<string>> ResolveActorRolesCoreAsync(
        IServiceProvider services,
        Guid userId,
        Guid tenantId)
    {
        try
        {
            // Null-safe so hosts that do not register the RBAC resolver (tests, minimal
            // probes) still record evaluations with an empty attribution instead of a
            // resolution error. Best-effort by contract: failures never change the decision.
            if (services.GetService(typeof(IRbacPermissionResolver)) is not IRbacPermissionResolver rbacResolver)
            {
                return PermissionEvaluationRoles.Empty;
            }

            var rbacResult = await rbacResolver
                .ResolvePermissionsAsync(userId, tenantId, CancellationToken.None)
                .ConfigureAwait(false);
            return PermissionEvaluationRoles.FromContributions(rbacResult.RoleContributions);
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
    public static Task RecordEvaluationAsync(
        IResolverContext resolverContext,
        ILogger logger,
        string fieldName,
        string resourceType,
        string? resourceId,
        IReadOnlyCollection<string> requiredPermissions,
        PermissionEvaluationOutcome outcome,
        string? reason = null)
        => RecordEvaluationAsync(
            resolverContext.Service<IServiceProvider>(),
            resolverContext.ContextData,
            resolverContext.RequestAborted,
            logger,
            fieldName,
            resourceType,
            resourceId,
            requiredPermissions,
            outcome,
            reason);

    /// <summary>
    ///     Provider-based evaluation logging with the same semantics: recording never
    ///     changes the decision and never throws (except cancellation).
    /// </summary>
    public static async Task RecordEvaluationAsync(
        IServiceProvider services,
        IDictionary<string, object?> contextData,
        CancellationToken requestAborted,
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
            var actor = services.GetRequiredService<IActorContextAccessor>().ActorContext;
            var roles = await ResolveActorRolesAsync(services, contextData, actor).ConfigureAwait(false);
            await services.GetRequiredService<IPermissionEvaluationLogService>().RecordAsync(
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
                requestAborted).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
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
    ///     Authorizes a computed field against the permissions of a RELATED entity (issue
    ///     #335): the caller's effective permissions are evaluated on the related entity's
    ///     resource context, not the parent type's. Resolvers for computed fields (for
    ///     example <c>canEdit</c> or projections of a related entity) call this with the
    ///     related entity's resource type and identifier; the check routes through the
    ///     effective-permission engine (owner override, expiration-aware precedence),
    ///     reuses the per-request permission cache and records the evaluation. Failures
    ///     surface as <see cref="GraphQLAuthorizationOutcome.Unavailable"/>, never throw.
    /// </summary>
    /// <param name="resolverContext">The executing resolver context.</param>
    /// <param name="resourceType">DAC resource type of the related entity.</param>
    /// <param name="relatedEntityId">
    ///     Resource identifier of the related entity. Null fails closed (the related
    ///     entity cannot be identified, so its permissions cannot be granted).
    /// </param>
    /// <param name="permissions">Typed permission keys required on the related entity.</param>
    /// <param name="mode">How <paramref name="permissions"/> compose (AND default, OR).</param>
    public static async Task<GraphQLAuthorizationOutcome> AuthorizeRelatedEntityAsync(
        IResolverContext resolverContext,
        string resourceType,
        Guid? relatedEntityId,
        PermissionType[] permissions,
        GraphQLAuthorizationMode mode = GraphQLAuthorizationMode.All)
    {
        ArgumentNullException.ThrowIfNull(resolverContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);
        ArgumentNullException.ThrowIfNull(permissions);

        var keys = permissions.Select(permission => permission.ToString()).ToArray();

        var actor = resolverContext.Service<IActorContextAccessor>().ActorContext;
        if (!actor.IsAuthenticated
            || actor.SubjectIdAsGuid is not { } userId || userId == Guid.Empty
            || actor.TenantId is not { } tenantId)
        {
            return await RecordRelatedEntityOutcomeAsync(
                resolverContext, resourceType, relatedEntityId, keys,
                GraphQLAuthorizationOutcome.Deny("actor_context_unavailable")).ConfigureAwait(false);
        }

        if (relatedEntityId is not { } resourceId || resourceId == Guid.Empty)
        {
            return await RecordRelatedEntityOutcomeAsync(
                resolverContext, resourceType, relatedEntityId, keys,
                GraphQLAuthorizationOutcome.Deny("related_entity_id_unavailable")).ConfigureAwait(false);
        }

        try
        {
            var permissionContext = EffectivePermissionContext.ForResource(
                userId, tenantId, resourceType, resourceId.ToString("D"));
            var resolved = await ResolveEffectivePermissionsAsync(resolverContext, permissionContext,
                resolverContext.RequestAborted).ConfigureAwait(false);
            if (!resolved.ContextValid)
            {
                return await RecordRelatedEntityOutcomeAsync(
                    resolverContext, resourceType, relatedEntityId, keys,
                    GraphQLAuthorizationOutcome.Deny("actor_context_unavailable")).ConfigureAwait(false);
            }

            var granted = mode == GraphQLAuthorizationMode.Any
                ? resolved.HasAnyPermission(keys)
                : resolved.HasAllPermissions(keys);
            return await RecordRelatedEntityOutcomeAsync(
                resolverContext, resourceType, relatedEntityId, keys,
                granted
                    ? GraphQLAuthorizationOutcome.Allow()
                    : GraphQLAuthorizationOutcome.Deny("permission_denied")).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return await RecordRelatedEntityOutcomeAsync(
                resolverContext, resourceType, relatedEntityId, keys,
                GraphQLAuthorizationOutcome.Unavailable("authorization_evaluation_failed")).ConfigureAwait(false);
        }
    }

    private static async Task<GraphQLAuthorizationOutcome> RecordRelatedEntityOutcomeAsync(
        IResolverContext resolverContext,
        string resourceType,
        Guid? relatedEntityId,
        string[] keys,
        GraphQLAuthorizationOutcome outcome)
    {
        var evaluationOutcome = outcome.Outcome switch
        {
            GraphQLAuthorizationDecision.Allowed => PermissionEvaluationOutcome.Allow,
            GraphQLAuthorizationDecision.Denied => PermissionEvaluationOutcome.Deny,
            _ => PermissionEvaluationOutcome.Error,
        };
        await RecordEvaluationAsync(
            resolverContext,
            NullLogger.Instance,
            resolverContext.Selection.Field.Name,
            resourceType,
            relatedEntityId?.ToString("D"),
            keys,
            evaluationOutcome,
            outcome.Reason).ConfigureAwait(false);
        return outcome;
    }

    private static TCache GetOrCreateCache<TCache>(
        IDictionary<string, object?> contextData,
        string key,
        Func<IDictionary<string, object?>, TCache> factory)
        where TCache : class
    {
        if (contextData.TryGetValue(key, out var cached) && cached is TCache existing)
        {
            return existing;
        }

        var created = factory(contextData);
        contextData[key] = created;
        return created;
    }

    /// <summary>
    ///     The bounded, non-revealing authorization denial error: a generic message with
    ///     the <see cref="DeniedErrorCode"/> code. The authorization error filter strips
    ///     the response path so the denied field's identity never leaks.
    /// </summary>
    public static GraphQLException CreateAuthorizationError() => new(
        ErrorBuilder.New()
            .SetMessage("Not authorized to access this resource.")
            .SetCode(DeniedErrorCode)
            .Build());

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

/// <summary>Decision value of a framework authorization evaluation.</summary>
public enum GraphQLAuthorizationDecision
{
    /// <summary>The requirement set was satisfied.</summary>
    Allowed = 0,

    /// <summary>The requirement set was not satisfied (fail-closed outcome included).</summary>
    Denied = 1,

    /// <summary>The evaluation itself failed; the decision defaulted to closed.</summary>
    Unavailable = 2,
}

/// <summary>
///     Outcome of a computed-field authorization against a related entity's permissions
///     (issue #335): the related entity's resource-scoped permission decision with a
///     machine-readable reason code for denials. Reason codes are server-side detail;
///     client-facing errors remain the bounded framework messages.
/// </summary>
public sealed record GraphQLAuthorizationOutcome(GraphQLAuthorizationDecision Outcome, string? Reason = null)
{
    /// <summary>True when <see cref="Outcome"/> is <see cref="GraphQLAuthorizationDecision.Allowed"/>.</summary>
    public bool Allowed => Outcome == GraphQLAuthorizationDecision.Allowed;

    /// <summary>Creates the allowed outcome.</summary>
    public static GraphQLAuthorizationOutcome Allow() => new(GraphQLAuthorizationDecision.Allowed);

    /// <summary>Creates a denied outcome with a machine-readable reason code.</summary>
    public static GraphQLAuthorizationOutcome Deny(string reason) => new(GraphQLAuthorizationDecision.Denied, reason);

    /// <summary>Creates an evaluation-failure outcome (decision defaulted to closed).</summary>
    public static GraphQLAuthorizationOutcome Unavailable(string reason)
        => new(GraphQLAuthorizationDecision.Unavailable, reason);
}
