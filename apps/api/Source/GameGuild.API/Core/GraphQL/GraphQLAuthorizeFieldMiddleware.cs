using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.GraphQL;
using GameGuild.Identity.Context.Actors;
using HotChocolate;
using HotChocolate.Resolvers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameGuild.API.Core.GraphQL;

/// <summary>
///     Field-level enforcement for the general <c>@authorize</c> directive (issue #335).
///     Protected fields are deny-by-default: the resolver only runs after every applied
///     directive requirement passes. Permission checks run through the effective-permission
///     engine, so resource-scoped rules inherit the owner-override and expiration-aware
///     precedence behavior (issues #372/#363). Introspection roots are gated by the
///     permission-aware schema visibility evaluator. All failures fail closed.
/// </summary>
public sealed class GraphQLAuthorizeFieldMiddleware(
    FieldDelegate next,
    ILogger<GraphQLAuthorizeFieldMiddleware> logger)
{
    private readonly FieldDelegate _next = next;
    private readonly ILogger<GraphQLAuthorizeFieldMiddleware> _logger = logger;

    public async ValueTask InvokeAsync(IMiddlewareContext context)
    {
        var field = context.Selection.Field;

        if (field.IsIntrospectionField)
        {
            await HandleIntrospectionFieldAsync(context).ConfigureAwait(false);
            return;
        }

        var rules = GraphQLAuthorizeFieldRules.Get(field);
        if (rules.Length == 0)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        await EnforceFieldRulesAsync(context, field.Name, rules).ConfigureAwait(false);
    }

    private async Task HandleIntrospectionFieldAsync(IMiddlewareContext context)
    {
        // Only the schema-revealing roots are gated; __typename and friends carry no schema.
        if (context.Selection.Field.Name is not ("__schema" or "__type"))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var fieldName = context.Selection.Field.Name;
        GraphQLIntrospectionDecision decision;
        try
        {
            var evaluator = context.Service<GraphQLSchemaVisibilityEvaluator>();
            decision = await evaluator.AuthorizeIntrospectionAsync(context, context.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "GraphQL introspection authorization could not be evaluated for field {FieldName}.",
                fieldName);
            decision = GraphQLIntrospectionDecision.Deny("introspection_evaluation_failed");
        }

        if (!decision.Allowed)
        {
            _logger.LogWarning(
                "GraphQL introspection denied for field {FieldName}: {Reason}.",
                fieldName,
                decision.Reason);
            await GraphQLAuthorizationSupport.RecordEvaluationAsync(
                context,
                _logger,
                fieldName,
                GraphQLAuthorizationSupport.SchemaResourceType,
                null,
                [],
                PermissionEvaluationOutcome.Deny,
                decision.Reason).ConfigureAwait(false);
            throw CreateAuthorizationError();
        }

        await GraphQLAuthorizationSupport.RecordEvaluationAsync(
            context,
            _logger,
            fieldName,
            GraphQLAuthorizationSupport.SchemaResourceType,
            null,
            [],
            PermissionEvaluationOutcome.Allow).ConfigureAwait(false);

        await _next(context).ConfigureAwait(false);
    }

    private async Task EnforceFieldRulesAsync(
        IMiddlewareContext context,
        string fieldName,
        GraphQLAuthorizeFieldRule[] rules)
    {
        ActorContext actor;
        try
        {
            actor = context.Service<IActorContextAccessor>().ActorContext;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // No trusted actor context available: fail closed.
            await DenyAsync(context, fieldName, rules, "actor_context_unavailable", permissionKeys: null)
                .ConfigureAwait(false);
            return;
        }

        if (!actor.IsAuthenticated)
        {
            await DenyAsync(context, fieldName, rules, "anonymous_denied", permissionKeys: null)
                .ConfigureAwait(false);
            return;
        }

        foreach (var rule in rules)
        {
            if (rule.Roles.Length > 0 && !await CheckRolesAsync(context, actor, rule).ConfigureAwait(false))
            {
                await DenyAsync(context, fieldName, rules, "role_denied", permissionKeys: rule.Permissions)
                    .ConfigureAwait(false);
                return;
            }

            if (rule.Policy is not null)
            {
                var policyOutcome = await CheckPolicyAsync(context, rule.Policy).ConfigureAwait(false);
                if (policyOutcome is not PolicyCheckResult.Allowed)
                {
                    await DenyAsync(
                        context,
                        fieldName,
                        rules,
                        policyOutcome is PolicyCheckResult.Failed ? "policy_denied" : "policy_evaluation_failed",
                        permissionKeys: rule.Permissions).ConfigureAwait(false);
                    return;
                }
            }

            if (rule.HasPermissions)
            {
                var permissionOutcome = await CheckPermissionsAsync(context, actor, rule).ConfigureAwait(false);
                switch (permissionOutcome.Outcome)
                {
                    case PermissionCheckOutcome.Allowed:
                        break;
                    case PermissionCheckOutcome.Denied:
                        await DenyAsync(
                            context,
                            fieldName,
                            rules,
                            permissionOutcome.Reason ?? "permission_denied",
                            permissionKeys: rule.Permissions,
                            resourceId: permissionOutcome.ResourceId?.ToString("D"),
                            resourceType: rule.ResourceType).ConfigureAwait(false);
                        return;
                    default:
                        await UnavailableAsync(
                            context,
                            fieldName,
                            rules,
                            permissionOutcome.Reason ?? "authorization_evaluation_failed",
                            permissionOutcome.ResourceId,
                            rule.ResourceType).ConfigureAwait(false);
                        return;
                }
            }
        }

        var requiredPermissions = rules.SelectMany(rule => rule.Permissions).Distinct().ToArray();
        await GraphQLAuthorizationSupport.RecordEvaluationAsync(
            context,
            _logger,
            fieldName,
            rules.Select(rule => rule.ResourceType).FirstOrDefault(type => type is not null)
                ?? GraphQLAuthorizationSupport.TenantResourceType,
            null,
            requiredPermissions,
            PermissionEvaluationOutcome.Allow).ConfigureAwait(false);

        await _next(context).ConfigureAwait(false);
    }

    private static Task<bool> CheckRolesAsync(IResolverContext context, ActorContext actor, GraphQLAuthorizeFieldRule rule)
    {
        // Roles are evaluated against the trusted actor context (never GraphQL input);
        // a caller holding any listed role satisfies the requirement.
        return Task.FromResult(rule.Roles.Any(actor.IsInRole));
    }

    private async Task<PolicyCheckResult> CheckPolicyAsync(IResolverContext context, string policy)
    {
        try
        {
            var httpContextAccessor = context.Service<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            var principal = httpContextAccessor.HttpContext?.User;
            if (principal is null)
            {
                return PolicyCheckResult.Failed;
            }

            var authorizationService = context.Service<IAuthorizationService>();
            var result = await authorizationService.AuthorizeAsync(principal, policy).ConfigureAwait(false);
            return result.Succeeded ? PolicyCheckResult.Allowed : PolicyCheckResult.Failed;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "GraphQL authorization policy {Policy} could not be evaluated; failing closed.",
                policy);
            return PolicyCheckResult.EvaluationFailed;
        }
    }

    private async Task<PermissionCheck> CheckPermissionsAsync(
        IResolverContext context,
        ActorContext actor,
        GraphQLAuthorizeFieldRule rule)
    {
        var keys = new List<PermissionType>(rule.Permissions.Length);
        foreach (var key in rule.Permissions)
        {
            // Unknown permission keys fail closed rather than crash: the schema is
            // compiled once and SDL-authored directives are not attribute-validated.
            if (!Enum.TryParse<PermissionType>(key, out var permission) || !Enum.IsDefined(permission))
            {
                return PermissionCheck.Deny("permission_key_invalid");
            }

            keys.Add(permission);
        }

        if (actor.SubjectIdAsGuid is not { } userId || userId == Guid.Empty || actor.TenantId is not { } tenantId)
        {
            return PermissionCheck.Deny("actor_context_unavailable");
        }

        Guid? resourceId = null;
        EffectivePermissionContext permissionContext;
        if (rule.IsResourceScoped)
        {
            resourceId = GraphQLAuthorizationSupport.TryResolveResourceId(
                context, rule.ResourceIdArgumentName, rule.ResourceIdParentPropertyName);
            if (resourceId is null || resourceId == Guid.Empty)
            {
                return PermissionCheck.Deny("resource_id_unavailable");
            }

            permissionContext = EffectivePermissionContext.ForResource(
                userId, tenantId, rule.ResourceType!, resourceId.Value.ToString("D"));
        }
        else if (rule.IsContentTypeScoped)
        {
            // Content-type scoped rules (issues #360/#365/#320) route through the
            // engine's content-type layer: content-type grants mint the qualified
            // {ContentType}.{Permission} names checked below, tenant-layer grants still
            // satisfy the unqualified names (Auto inheritance), DENY-WINS applies.
            permissionContext = EffectivePermissionContext.ForContentType(
                userId, tenantId, rule.ContentType!);
        }
        else
        {
            permissionContext = EffectivePermissionContext.ForTenant(userId, tenantId);
        }

        try
        {
            var permissions = await GraphQLAuthorizationSupport.ResolveEffectivePermissionsAsync(
                context, permissionContext, context.RequestAborted).ConfigureAwait(false);
            if (!permissions.ContextValid)
            {
                return PermissionCheck.Deny("actor_context_unavailable", resourceId);
            }

            // Layer-aware satisfaction (content-type scoped rules, issues #360/#365):
            // each permission key is satisfied by EITHER the content-type qualified name
            // (the engine's content-type grant layer) OR the unqualified tenant name
            // (inheritance: a tenant-layer grant satisfies a content-type scoped rule).
            // The All/Any composition applies across keys, never across a key's names.
            var contentType = rule.IsContentTypeScoped ? rule.ContentType : null;
            bool Satisfies(PermissionType key)
            {
                var name = key.ToString();
                return contentType is null
                    ? permissions.HasPermission(name)
                    : permissions.HasPermission(name) || permissions.HasPermission($"{contentType}.{name}");
            }

            var granted = rule.Mode == GraphQLAuthorizationMode.Any
                ? keys.Any(Satisfies)
                : keys.All(Satisfies);
            return granted
                ? PermissionCheck.Ok(resourceId)
                : PermissionCheck.Deny("permission_denied", resourceId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "GraphQL permission evaluation failed for a protected field; failing closed.");
            return PermissionCheck.Unavailable("authorization_evaluation_failed", resourceId);
        }
    }

    private async Task DenyAsync(
        IMiddlewareContext context,
        string fieldName,
        GraphQLAuthorizeFieldRule[] rules,
        string reason,
        string[]? permissionKeys,
        string? resourceId = null,
        string? resourceType = null)
    {
        var mask = rules.All(rule => rule.MaskUnauthorized);
        _logger.LogWarning(
            "GraphQL authorization denied for field {FieldName}: {Reason}.",
            fieldName, reason);
        await GraphQLAuthorizationSupport.RecordEvaluationAsync(
            context,
            _logger,
            fieldName,
            resourceType ?? GraphQLAuthorizationSupport.TenantResourceType,
            resourceId,
            permissionKeys ?? [],
            PermissionEvaluationOutcome.Deny,
            reason).ConfigureAwait(false);

        if (mask)
        {
            context.Result = null;
            return;
        }

        throw CreateAuthorizationError();
    }

    private async Task UnavailableAsync(
        IMiddlewareContext context,
        string fieldName,
        GraphQLAuthorizeFieldRule[] rules,
        string reason,
        Guid? resourceId,
        string? resourceType)
    {
        var mask = rules.All(rule => rule.MaskUnauthorized);
        _logger.LogError(
            "GraphQL authorization could not be evaluated for field {FieldName}: {Reason}.",
            fieldName, reason);
        await GraphQLAuthorizationSupport.RecordEvaluationAsync(
            context,
            _logger,
            fieldName,
            resourceType ?? GraphQLAuthorizationSupport.TenantResourceType,
            resourceId?.ToString("D"),
            rules.SelectMany(rule => rule.Permissions).Distinct().ToArray(),
            PermissionEvaluationOutcome.Error,
            reason).ConfigureAwait(false);

        if (mask)
        {
            context.Result = null;
            return;
        }

        throw new GraphQLException(
            ErrorBuilder.New()
                .SetMessage("Authorization could not be evaluated.")
                .SetCode("AUTHORIZATION_UNAVAILABLE")
                .Build());
    }

    private static GraphQLException CreateAuthorizationError() => new(
        ErrorBuilder.New()
            .SetMessage("Not authorized to access this resource.")
            .SetCode("AUTHORIZATION_DENIED")
            .Build());

    private enum PolicyCheckResult
    {
        Allowed,
        Failed,
        EvaluationFailed,
    }

    private enum PermissionCheckOutcome
    {
        Allowed,
        Denied,
        Unavailable,
    }

    private sealed record PermissionCheck(
        PermissionCheckOutcome Outcome,
        string? Reason = null,
        Guid? ResourceId = null)
    {
        public static PermissionCheck Ok(Guid? resourceId) => new(PermissionCheckOutcome.Allowed, null, resourceId);

        public static PermissionCheck Deny(string reason, Guid? resourceId = null)
            => new(PermissionCheckOutcome.Denied, reason, resourceId);

        public static PermissionCheck Unavailable(string reason, Guid? resourceId = null)
            => new(PermissionCheckOutcome.Unavailable, reason, resourceId);
    }
}
