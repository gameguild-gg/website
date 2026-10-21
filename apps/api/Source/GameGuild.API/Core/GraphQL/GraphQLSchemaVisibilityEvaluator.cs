using GameGuild.Configuration.PresentationLayer.GraphQL;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.GraphQL;
using GameGuild.Identity.Context.Actors;
using HotChocolate.Resolvers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameGuild.API.Core.GraphQL;

/// <summary>Outcome of an introspection authorization check.</summary>
/// <param name="Allowed">True when the caller may introspect the schema.</param>
/// <param name="Reason">Machine-readable reason code for denials; null when allowed.</param>
internal sealed record GraphQLIntrospectionDecision(bool Allowed, string? Reason)
{
    public static GraphQLIntrospectionDecision Allow() => new(true, null);

    public static GraphQLIntrospectionDecision Deny(string reason) => new(false, reason);
}

/// <summary>
///     Permission-aware schema visibility for introspection (issue #335). The strategy is
///     fail-closed: a caller may introspect only when every <c>@authorize</c>-protected
///     field in the schema is visible to them. Callers that cannot see everything are
///     denied introspection entirely rather than served a partial schema, so protected
///     operations never leak through introspection metadata. In Production, introspection
///     is restricted to schema administrators (config-gated).
/// </summary>
internal sealed class GraphQLSchemaVisibilityEvaluator(
    IAuthorizationService authorizationService,
    IActorContextAccessor actorContextAccessor,
    IHostEnvironment hostEnvironment,
    GraphQLOptions graphQLOptions,
    ILogger<GraphQLSchemaVisibilityEvaluator> logger)
{
    /// <summary>
    ///     Decides whether the current caller may execute an introspection field
    ///     (<c>__schema</c> / <c>__type</c>).
    /// </summary>
    public async Task<GraphQLIntrospectionDecision> AuthorizeIntrospectionAsync(
        IResolverContext resolverContext,
        CancellationToken cancellationToken)
    {
        var options = graphQLOptions.SchemaAuthorization;

        try
        {
            var actor = actorContextAccessor.ActorContext;

            if (await IsSchemaAdminAsync(resolverContext, actor, options, cancellationToken).ConfigureAwait(false))
            {
                return GraphQLIntrospectionDecision.Allow();
            }

            var isProduction = hostEnvironment.IsProduction();
            if (isProduction && options.RestrictIntrospectionToSchemaAdminsInProduction)
            {
                return GraphQLIntrospectionDecision.Deny("introspection_restricted");
            }

            if (!options.FilterSchema)
            {
                return GraphQLIntrospectionDecision.Allow();
            }

            return await CheckFullSchemaVisibilityAsync(resolverContext, actor, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "GraphQL schema visibility evaluation failed; introspection is denied (fail-closed).");
            return GraphQLIntrospectionDecision.Deny("introspection_evaluation_failed");
        }
    }

    /// <summary>
    ///     Decides whether the caller's effective schema surface is unrestricted, without
    ///     the environment-specific introspection restriction: schema administrators and
    ///     callers whose permission set satisfies every protected field's
    ///     planning-decidable rule see the Full tier (used by the permission-level schema
    ///     version, issue #335). Fail-closed semantics match
    ///     <see cref="AuthorizeIntrospectionAsync"/>.
    /// </summary>
    internal async Task<GraphQLIntrospectionDecision> EvaluateCallerVisibilityAsync(
        IResolverContext resolverContext,
        CancellationToken cancellationToken)
    {
        var options = graphQLOptions.SchemaAuthorization;

        try
        {
            var actor = actorContextAccessor.ActorContext;

            if (await IsSchemaAdminAsync(resolverContext, actor, options, cancellationToken).ConfigureAwait(false))
            {
                return GraphQLIntrospectionDecision.Allow();
            }

            if (!options.FilterSchema)
            {
                return GraphQLIntrospectionDecision.Allow();
            }

            return await CheckFullSchemaVisibilityAsync(resolverContext, actor, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "GraphQL schema visibility evaluation failed; the caller is treated as restricted (fail-closed).");
            return GraphQLIntrospectionDecision.Deny("visibility_evaluation_failed");
        }
    }

    private async Task<bool> IsSchemaAdminAsync(
        IResolverContext resolverContext,
        ActorContext actor,
        GraphQLSchemaAuthorizationOptions options,
        CancellationToken cancellationToken)
    {
        // Fail closed: an unknown/blank permission key grants nobody schema-admin access,
        // and an invalid actor context or evaluation failure is not an admin either.
        var adminPermission = options.SchemaAdminPermission;
        if (string.IsNullOrWhiteSpace(adminPermission) ||
            actor.SubjectIdAsGuid is not { } userId || userId == Guid.Empty ||
            actor.TenantId is not { } tenantId)
        {
            return false;
        }

        var permissions = await GraphQLAuthorizationSupport.ResolveEffectivePermissionsAsync(
            resolverContext,
            EffectivePermissionContext.ForTenant(userId, tenantId),
            cancellationToken).ConfigureAwait(false);
        return permissions.ContextValid && permissions.HasPermission(adminPermission);
    }

    private async Task<GraphQLIntrospectionDecision> CheckFullSchemaVisibilityAsync(
        IResolverContext resolverContext,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        var protectedFields = GraphQLAuthorizeFieldRules.CollectProtectedFields(resolverContext.Schema);

        foreach (var (_, rules) in protectedFields)
        {
            foreach (var rule in rules)
            {
                // Resource-scoped rules cannot be proven visible without a concrete
                // resource: fail closed (the field is treated as not visible).
                if (rule.IsResourceScoped)
                {
                    return GraphQLIntrospectionDecision.Deny("protected_fields_not_visible");
                }

                if (!await IsRuleVisibleAsync(resolverContext, actor, rule, cancellationToken).ConfigureAwait(false))
                {
                    return GraphQLIntrospectionDecision.Deny("protected_fields_not_visible");
                }
            }
        }

        return GraphQLIntrospectionDecision.Allow();
    }

    private async Task<bool> IsRuleVisibleAsync(
        IResolverContext resolverContext,
        ActorContext actor,
        GraphQLAuthorizeFieldRule rule,
        CancellationToken cancellationToken)
    {
        if (rule.Roles.Length > 0 && !rule.Roles.Any(actor.IsInRole))
        {
            return false;
        }

        if (rule.Policy is not null)
        {
            var principal = resolverContext.Service<Microsoft.AspNetCore.Http.IHttpContextAccessor>()
                .HttpContext?.User;
            if (principal is null)
            {
                return false;
            }

            var policyResult = await authorizationService
                .AuthorizeAsync(principal, rule.Policy)
                .ConfigureAwait(false);
            if (!policyResult.Succeeded)
            {
                return false;
            }
        }

        if (rule.HasPermissions)
        {
            if (actor.SubjectIdAsGuid is not { } userId || userId == Guid.Empty ||
                actor.TenantId is not { } tenantId)
            {
                return false;
            }

            var keys = new List<PermissionType>(rule.Permissions.Length);
            foreach (var key in rule.Permissions)
            {
                if (!Enum.TryParse<PermissionType>(key, out var permission) || !Enum.IsDefined(permission))
                {
                    return false;
                }

                keys.Add(permission);
            }

            var names = keys.Select(key => key.ToString()).ToArray();
            var permissions = await GraphQLAuthorizationSupport.ResolveEffectivePermissionsAsync(
                resolverContext,
                EffectivePermissionContext.ForTenant(userId, tenantId),
                cancellationToken).ConfigureAwait(false);
            if (!permissions.ContextValid)
            {
                return false;
            }

            var visible = rule.Mode == GraphQLAuthorizationMode.Any
                ? permissions.HasAnyPermission(names)
                : permissions.HasAllPermissions(names);
            if (!visible)
            {
                return false;
            }
        }

        return true;
    }
}
