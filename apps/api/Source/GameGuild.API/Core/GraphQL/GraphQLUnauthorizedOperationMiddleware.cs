using System.Security.Claims;
using GameGuild.Configuration.PresentationLayer.GraphQL;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.GraphQL;
using GameGuild.Identity.Context.Actors;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Language;
using HotChocolate.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameGuild.API.Core.GraphQL;

/// <summary>
///     Operation-level authorization gate for the GraphQL authorization framework (issue
///     #335): root operations the caller is not authorized to execute are hidden at the
///     operation-planning stage. The middleware runs after document validation and before
///     operation compilation: every root field referenced by the request (directly, through
///     inline fragments or fragment spreads) that carries a planning-decidable
///     <c>@authorize</c> rule is checked against the caller's resolved permission set; any
///     denial fails the whole request closed with the same bounded error shape HotChocolate
///     produces for a nonexistent field, so the response never reveals that a protected
///     operation exists (no path, no <c>data</c> payload).
///     <para>
///         Planning-decidable rules are tenant-scoped permissions, content-type scoped
///         permissions, roles and policies. Resource-scoped rules depend on a concrete
///         resource (and its owner-override state) and remain enforced during execution by
///         <see cref="GraphQLAuthorizeFieldMiddleware"/>, where the authorization error
///         filter strips the denied field's path. Schema administrators bypass the gate.
///         Every failure mode (anonymous actor, unresolvable actor context, unknown
///         permission keys, evaluation errors) denies.
///     </para>
/// </summary>
public sealed class GraphQLUnauthorizedOperationMiddleware(RequestDelegate next)
{
    private const string FieldSelectionsSpecSection =
        "sec-Field-Selections-on-Objects-Interfaces-and-Unions-Types";

    private readonly RequestDelegate _next = next;

    public async ValueTask InvokeAsync(IRequestContext context)
    {
        if (context.Document is null || context.Result is not null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var services = context.Services;
        var options = services.GetService<GraphQLOptions>()?.SchemaAuthorization
            ?? new GraphQLSchemaAuthorizationOptions();
        if (!options.HideUnauthorizedOperations)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var logger = services.GetService<ILogger<GraphQLUnauthorizedOperationMiddleware>>()
            ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GraphQLUnauthorizedOperationMiddleware>.Instance;
        var denial = await FindUnauthorizedOperationAsync(context, services, options, logger)
            .ConfigureAwait(false);
        if (denial is not null)
        {
            // Fail closed before execution: the response carries only the replicated
            // nonexistent-field error (no data payload, no path, no authorization code).
            context.Result = OperationResultBuilder.CreateError([denial.Value.Error]);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private async Task<HiddenOperationError?> FindUnauthorizedOperationAsync(
        IRequestContext context,
        IServiceProvider services,
        GraphQLSchemaAuthorizationOptions options,
        ILogger logger)
    {
        try
        {
            var actor = services.GetService<IActorContextAccessor>()?.ActorContext ?? ActorContext.Anonymous;

            var referencedRootFields = EnumerateReferencedRootFields(
                context.Document, context.Schema, context.Request.OperationName);
            foreach (var selection in referencedRootFields)
            {
                var rules = GraphQLAuthorizeFieldRules.Get(selection.Field);
                if (rules.Length == 0)
                {
                    continue;
                }

                if (await IsSchemaAdminAsync(context, services, actor, options).ConfigureAwait(false))
                {
                    return null;
                }

                foreach (var rule in rules)
                {
                    // Resource-scoped rules are decided during execution (concrete resource,
                    // owner override); they are not plannable here and stay enforced by the
                    // field middleware.
                    if (rule.IsResourceScoped)
                    {
                        continue;
                    }

                    var reason = await EvaluateRuleAsync(context, services, actor, rule).ConfigureAwait(false);
                    if (reason is null)
                    {
                        continue;
                    }

                    logger.LogWarning(
                        "GraphQL operation {RootType}.{FieldName} is hidden from the caller at planning time: {Reason}.",
                        selection.RootType.Name,
                        selection.FieldName,
                        reason);
                    await GraphQLAuthorizationSupport.RecordEvaluationAsync(
                        services,
                        context.ContextData,
                        context.RequestAborted,
                        logger,
                        selection.FieldName,
                        GraphQLAuthorizationSupport.TenantResourceType,
                        null,
                        rule.Permissions,
                        PermissionEvaluationOutcome.Deny,
                        reason).ConfigureAwait(false);
                    return CreateHiddenOperationError(selection);
                }
            }

            return null;
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Fail closed: an evaluation failure hides the referenced protected operation.
            logger.LogError(
                exception,
                "GraphQL operation-visibility evaluation failed; the request is denied (fail-closed).");
            return CreateFallbackError(context.Document);
        }
    }

    /// <summary>
    ///     Evaluates one planning-decidable rule for the caller. Returns null when the rule
    ///     is satisfied; otherwise a machine-readable reason code for the denial.
    /// </summary>
    private static async Task<string?> EvaluateRuleAsync(
        IRequestContext context,
        IServiceProvider services,
        ActorContext actor,
        GraphQLAuthorizeFieldRule rule)
    {
        if (rule.Roles.Length > 0 && !rule.Roles.Any(actor.IsInRole))
        {
            return "role_denied";
        }

        if (rule.Policy is not null)
        {
            var principal = services.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>()
                ?.HttpContext?.User;
            if (principal is null || !await AuthorizePolicyAsync(services, principal, rule.Policy).ConfigureAwait(false))
            {
                return "policy_denied";
            }
        }

        if (rule.HasPermissions)
        {
            if (!actor.IsAuthenticated
                || actor.SubjectIdAsGuid is not { } userId || userId == Guid.Empty
                || actor.TenantId is not { } tenantId)
            {
                return "actor_context_unavailable";
            }

            var keys = new List<PermissionType>(rule.Permissions.Length);
            foreach (var key in rule.Permissions)
            {
                // Unknown permission keys fail closed rather than crash.
                if (!Enum.TryParse<PermissionType>(key, out var permission) || !Enum.IsDefined(permission))
                {
                    return "permission_key_invalid";
                }

                keys.Add(permission);
            }

            var permissionContext = rule.IsContentTypeScoped
                ? EffectivePermissionContext.ForContentType(userId, tenantId, rule.ContentType!)
                : EffectivePermissionContext.ForTenant(userId, tenantId);
            var permissions = await GraphQLAuthorizationSupport.ResolveEffectivePermissionsAsync(
                services, context.ContextData, permissionContext, context.RequestAborted).ConfigureAwait(false);
            if (!permissions.ContextValid)
            {
                return "actor_context_unavailable";
            }

            // Same layer-aware satisfaction as the field middleware: content-type scoped
            // rules accept the qualified or the unqualified (inherited) tenant name.
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
            if (!granted)
            {
                return "permission_denied";
            }
        }

        return null;
    }

    private static async Task<bool> AuthorizePolicyAsync(
        IServiceProvider services,
        ClaimsPrincipal principal,
        string policy)
    {
        try
        {
            var authorizationService = services.GetService<IAuthorizationService>();
            if (authorizationService is null)
            {
                return false;
            }

            var result = await authorizationService.AuthorizeAsync(principal, policy).ConfigureAwait(false);
            return result.Succeeded;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task<bool> IsSchemaAdminAsync(
        IRequestContext context,
        IServiceProvider services,
        ActorContext actor,
        GraphQLSchemaAuthorizationOptions options)
    {
        // Fail closed: unknown/blank permission keys grant nobody schema-admin access.
        var adminPermission = options.SchemaAdminPermission;
        if (string.IsNullOrWhiteSpace(adminPermission)
            || !actor.IsAuthenticated
            || actor.SubjectIdAsGuid is not { } userId || userId == Guid.Empty
            || actor.TenantId is not { } tenantId)
        {
            return false;
        }

        var permissions = await GraphQLAuthorizationSupport.ResolveEffectivePermissionsAsync(
            services,
            context.ContextData,
            EffectivePermissionContext.ForTenant(userId, tenantId),
            context.RequestAborted).ConfigureAwait(false);
        return permissions.ContextValid && permissions.HasPermission(adminPermission);
    }

    /// <summary>
    ///     Enumerates the root field selections referenced by the executed operation(s),
    ///     expanding inline fragments and fragment spreads so protected operations cannot
    ///     bypass the gate by hiding behind a fragment. Introspection meta-fields
    ///     (<c>__</c>-prefixed) are skipped: they are gated by the schema visibility
    ///     evaluator during execution.
    /// </summary>
    private static IEnumerable<(ObjectType RootType, string FieldName, IObjectField Field, FieldNode Node)>
        EnumerateReferencedRootFields(DocumentNode document, ISchema schema, string? operationName)
    {
        var fragments = new Dictionary<string, FragmentDefinitionNode>(StringComparer.Ordinal);
        foreach (var definition in document.Definitions)
        {
            if (definition is FragmentDefinitionNode fragment)
            {
                fragments[fragment.Name.Value] = fragment;
            }
        }

        foreach (var operation in document.Definitions)
        {
            if (operation is not OperationDefinitionNode operationDefinition)
            {
                continue;
            }

            if (operationName is not null
                && !string.Equals(operationDefinition.Name?.Value, operationName, StringComparison.Ordinal))
            {
                continue;
            }

            var rootType = operationDefinition.Operation switch
            {
                OperationType.Query => schema.QueryType,
                OperationType.Mutation => schema.MutationType,
                OperationType.Subscription => schema.SubscriptionType,
                _ => null,
            };
            if (rootType is null)
            {
                continue;
            }

            var visitedFragments = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fieldNode in WalkSelections(operationDefinition.SelectionSet, fragments, visitedFragments))
            {
                if (fieldNode.Name.Value.StartsWith("__", StringComparison.Ordinal))
                {
                    continue;
                }

                var field = rootType.Fields[fieldNode.Name.Value];
                if (field is null)
                {
                    continue;
                }

                yield return (rootType, field.Name, field, fieldNode);
            }
        }
    }

    private static IEnumerable<FieldNode> WalkSelections(
        SelectionSetNode selectionSet,
        IReadOnlyDictionary<string, FragmentDefinitionNode> fragments,
        HashSet<string> visitedFragments)
    {
        foreach (var selection in selectionSet.Selections)
        {
            switch (selection)
            {
                case FieldNode field:
                    yield return field;
                    break;
                case InlineFragmentNode inline:
                    foreach (var nested in WalkSelections(inline.SelectionSet, fragments, visitedFragments))
                    {
                        yield return nested;
                    }

                    break;
                case FragmentSpreadNode spread when visitedFragments.Add(spread.Name.Value)
                    && fragments.TryGetValue(spread.Name.Value, out var definition):
                    foreach (var nested in WalkSelections(definition.SelectionSet, fragments, visitedFragments))
                    {
                        yield return nested;
                    }

                    break;
            }
        }
    }

    /// <summary>
    ///     Replicates HotChocolate's nonexistent-field validation error
    ///     (<c>The field \`X\` does not exist on the type \`Query\`.</c>) so a hidden
    ///     operation is indistinguishable from an unknown one: same message, location,
    ///     extensions and spec reference, no authorization code.
    /// </summary>
    private static HiddenOperationError CreateHiddenOperationError(
        (ObjectType RootType, string FieldName, IObjectField Field, FieldNode Node) selection)
        => new(
            ErrorBuilder.New()
                .SetMessage(
                    $"The field `{selection.Node.Name.Value}` does not exist on the type `{selection.RootType.Name}`.")
                .SetLocations([selection.Node])
                .SetExtension("type", selection.RootType.Name)
                .SetExtension("field", selection.Node.Name.Value)
                .SetExtension("responseName", (selection.Node.Alias ?? selection.Node.Name).Value)
                .SetExtension("specifiedBy", $"https://spec.graphql.org/October2021/#{FieldSelectionsSpecSection}")
                .Build());

    private static HiddenOperationError CreateFallbackError(DocumentNode document) => new(
        ErrorBuilder.New()
            .SetMessage("The request could not be validated.")
            .SetLocations([document])
            .Build());

    private readonly record struct HiddenOperationError(IError Error);
}
