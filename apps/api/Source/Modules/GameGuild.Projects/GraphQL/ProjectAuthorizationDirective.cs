using System.Collections.Concurrent;
using System.Reflection;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Authorization;
using HotChocolate;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Projects;

public enum ProjectPermissionEvaluationMode
{
    All,
    Any,
}

internal static class ProjectGraphQLPermissionCache
{
    private const string PermissionCacheKey = "GameGuild.Projects.GraphQL.PermissionChecks";

    public static async Task<bool> HasPermissionAsync(
        IResolverContext resolverContext,
        IProjectAuthorizationService authorizationService,
        Guid projectId,
        PermissionType permission,
        CancellationToken cancellationToken)
    {
        var permissionCache = resolverContext.GetOrSetGlobalState(
            PermissionCacheKey,
            static _ => new ConcurrentDictionary<(Guid ProjectId, PermissionType Permission), Lazy<Task<bool>>>());
        var key = (projectId, permission);
        var decision = permissionCache.GetOrAdd(
            key,
            _ => new Lazy<Task<bool>>(
                () => authorizationService.HasPermissionAsync(projectId, permission, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await decision.Value.ConfigureAwait(false);
        }
        catch
        {
            permissionCache.TryRemove(key, out _);
            throw;
        }
    }
}

/// <summary>
/// Schema metadata describing the DAC permissions enforced for a GraphQL field.
/// The actor and tenant are resolved by the project authorization service from the
/// authenticated request context; they are never accepted from GraphQL input.
/// </summary>
public sealed class ProjectAuthorizationDirective
{
    public string[] Permissions { get; init; } = [];

    public bool MaskUnauthorized { get; init; }

    public string? ResourceIdArgumentName { get; init; }

    public string? ResourceIdParentPropertyName { get; init; }

    public string? PermissionSwitchArgumentName { get; init; }

    public string? PermissionWhenSwitchFalse { get; init; }

    public ProjectPermissionEvaluationMode Mode { get; init; } = ProjectPermissionEvaluationMode.All;
}

/// <summary>Declares repeatable project DAC metadata for GraphQL field definitions.</summary>
public sealed class ProjectAuthorizationDirectiveType : DirectiveType<ProjectAuthorizationDirective>
{
    protected override void Configure(IDirectiveTypeDescriptor<ProjectAuthorizationDirective> descriptor)
    {
        descriptor.Name("projectAuthorize");
        descriptor.Description("Requires the authenticated actor to hold project DAC permissions.");
        descriptor.Location(DirectiveLocation.FieldDefinition);
        descriptor.Repeatable();
        descriptor.Argument(directive => directive.Permissions)
            .Type<ListType<NonNullType<StringType>>>();
        descriptor.Argument(directive => directive.MaskUnauthorized)
            .Type<BooleanType>()
            .DefaultValue(false);
        descriptor.Argument(directive => directive.ResourceIdArgumentName)
            .Type<StringType>();
        descriptor.Argument(directive => directive.ResourceIdParentPropertyName)
            .Type<StringType>();
        descriptor.Argument(directive => directive.PermissionSwitchArgumentName)
            .Type<StringType>();
        descriptor.Argument(directive => directive.PermissionWhenSwitchFalse)
            .Type<StringType>();
        descriptor.Argument(directive => directive.Mode)
            .Type<EnumType<ProjectPermissionEvaluationMode>>()
            .DefaultValue(ProjectPermissionEvaluationMode.All);
    }
}

/// <summary>
/// Applies resource-level DAC checks to a GraphQL field and publishes the same policy
/// as repeatable <c>@projectAuthorize</c> schema metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireGraphQLProjectPermissionAttribute : ObjectFieldDescriptorAttribute
{
    private const string ResourceType = "Project";
    private const string PermissionEvaluationLogSource = "graphql";

    private readonly PermissionType[] _permissions;
    private readonly string? _permissionSwitchArgumentName;
    private readonly PermissionType? _permissionWhenSwitchFalse;

    /// <summary>Whether a denied field should be returned as null instead of a GraphQL error.</summary>
    public bool MaskUnauthorized { get; set; }

    public RequireGraphQLProjectPermissionAttribute(params PermissionType[] permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        _permissions = permissions.Distinct().ToArray();
    }

    public RequireGraphQLProjectPermissionAttribute(
        PermissionType permission,
        string permissionSwitchArgumentName,
        PermissionType permissionWhenSwitchFalse)
        : this(permission)
    {
        if (string.IsNullOrWhiteSpace(permissionSwitchArgumentName))
        {
            throw new ArgumentException("A conditional permission requires a GraphQL boolean argument name.", nameof(permissionSwitchArgumentName));
        }

        _permissionSwitchArgumentName = permissionSwitchArgumentName;
        _permissionWhenSwitchFalse = permissionWhenSwitchFalse;
    }

    /// <summary>
    ///     GraphQL argument path containing the project ID. Dotted paths support input
    ///     objects, for example <c>input.projectId</c>.
    /// </summary>
    public string ResourceIdArgumentName { get; set; } = "id";

    /// <summary>Project identifier property path on the parent resolver value for nested fields.</summary>
    public string? ResourceIdParentPropertyName { get; set; }

    /// <summary>Whether every listed permission or at least one must be granted.</summary>
    public ProjectPermissionEvaluationMode Mode { get; set; } = ProjectPermissionEvaluationMode.All;

    protected override void OnConfigure(
        IDescriptorContext context,
        IObjectFieldDescriptor descriptor,
        MemberInfo member)
    {
        if (_permissions.Length == 0 || _permissions.Any(permission => !Enum.IsDefined(permission)))
        {
            throw new InvalidOperationException("A GraphQL project authorization rule must require one or more valid permissions.");
        }

        if (!Enum.IsDefined(Mode))
        {
            throw new InvalidOperationException("A GraphQL project authorization rule must use a valid permission evaluation mode.");
        }

        if (ResourceIdParentPropertyName is null && string.IsNullOrWhiteSpace(ResourceIdArgumentName))
        {
            throw new InvalidOperationException("A GraphQL project authorization rule must identify the project ID argument.");
        }

        if (ResourceIdParentPropertyName is not null && string.IsNullOrWhiteSpace(ResourceIdParentPropertyName))
        {
            throw new InvalidOperationException("A GraphQL project authorization rule must identify the parent project ID property.");
        }

        if (_permissionSwitchArgumentName is not null &&
            (_permissionWhenSwitchFalse is null || !Enum.IsDefined(_permissionWhenSwitchFalse.Value)))
        {
            throw new InvalidOperationException("A conditional GraphQL project authorization rule must specify a valid fallback permission.");
        }

        var permissionNames = _permissions.Select(permission => permission.ToString()).ToArray();
        descriptor.Directive(new ProjectAuthorizationDirective
        {
            Permissions = permissionNames,
            MaskUnauthorized = MaskUnauthorized,
            ResourceIdArgumentName = ResourceIdParentPropertyName is null ? ResourceIdArgumentName : null,
            ResourceIdParentPropertyName = ResourceIdParentPropertyName,
            PermissionSwitchArgumentName = _permissionSwitchArgumentName,
            PermissionWhenSwitchFalse = _permissionWhenSwitchFalse?.ToString(),
            Mode = Mode,
        });

        descriptor.Use(next => async resolverContext =>
        {
            var logger = resolverContext.Service<ILogger<RequireGraphQLProjectPermissionAttribute>>();
            var fieldName = resolverContext.Selection.Field.Name;
            var projectId = TryResolveProjectId(
                resolverContext,
                ResourceIdArgumentName,
                ResourceIdParentPropertyName);
            var requiredPermissions = TryResolveRequiredPermissions(resolverContext);
            if (projectId is null || projectId == Guid.Empty)
            {
                await RecordEvaluationAsync(
                    resolverContext,
                    logger,
                    fieldName,
                    projectId,
                    requiredPermissions ?? _permissions,
                    PermissionEvaluationOutcome.Deny,
                    "resource_id_unavailable").ConfigureAwait(false);
                logger.LogWarning(
                    "GraphQL project authorization denied for field {FieldName} because its resource ID was unavailable.",
                    fieldName);
                if (MaskUnauthorized)
                {
                    resolverContext.Result = null;
                    return;
                }

                throw CreateAuthorizationError();
            }

            if (requiredPermissions is null)
            {
                await RecordEvaluationAsync(
                    resolverContext,
                    logger,
                    fieldName,
                    projectId,
                    _permissions,
                    PermissionEvaluationOutcome.Deny,
                    "permission_condition_unavailable").ConfigureAwait(false);
                logger.LogWarning(
                    "GraphQL project authorization denied for field {FieldName} because its conditional permission input was unavailable.",
                    fieldName);
                if (MaskUnauthorized)
                {
                    resolverContext.Result = null;
                    return;
                }

                throw CreateAuthorizationError();
            }

            var authorizationService = resolverContext.Service<IProjectAuthorizationService>();
            bool allowed;
            try
            {
                allowed = await EvaluateAsync(
                    resolverContext,
                    authorizationService,
                    projectId.Value,
                    requiredPermissions,
                    resolverContext.RequestAborted).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (resolverContext.RequestAborted.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await RecordEvaluationAsync(
                    resolverContext,
                    logger,
                    fieldName,
                    projectId,
                    requiredPermissions,
                    PermissionEvaluationOutcome.Error,
                    "authorization_evaluation_failed").ConfigureAwait(false);
                logger.LogError(
                    exception,
                    "GraphQL project authorization could not be evaluated for field {FieldName} and resource {ProjectId}.",
                    fieldName,
                    projectId.Value);
                if (MaskUnauthorized)
                {
                    resolverContext.Result = null;
                    return;
                }

                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Authorization could not be evaluated.")
                        .SetCode("AUTHORIZATION_UNAVAILABLE")
                        .Build());
            }

            if (!allowed)
            {
                await RecordEvaluationAsync(
                    resolverContext,
                    logger,
                    fieldName,
                    projectId,
                    requiredPermissions,
                    PermissionEvaluationOutcome.Deny,
                    "permission_denied").ConfigureAwait(false);
                logger.LogWarning(
                    "GraphQL project authorization denied for field {FieldName} and resource {ProjectId}.",
                    fieldName,
                    projectId.Value);
                if (MaskUnauthorized)
                {
                    resolverContext.Result = null;
                    return;
                }

                throw CreateAuthorizationError();
            }

            await RecordEvaluationAsync(
                resolverContext,
                logger,
                fieldName,
                projectId,
                requiredPermissions,
                PermissionEvaluationOutcome.Allow).ConfigureAwait(false);

            await next(resolverContext).ConfigureAwait(false);
        });
    }

    private static async Task RecordEvaluationAsync(
        IResolverContext resolverContext,
        ILogger logger,
        string fieldName,
        Guid? projectId,
        IReadOnlyCollection<PermissionType> requiredPermissions,
        PermissionEvaluationOutcome outcome,
        string? reason = null)
    {
        try
        {
            var actor = resolverContext.Service<IActorContextAccessor>().ActorContext;
            await resolverContext.Service<IPermissionEvaluationLogService>().RecordAsync(
                new PermissionEvaluationRecord(
                    actor.SubjectIdAsGuid,
                    actor.TenantId,
                    ResourceType,
                    projectId?.ToString("D"),
                    requiredPermissions.Select(permission => permission.ToString()).ToArray(),
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
                "Failed to record GraphQL project permission evaluation for field {FieldName}.",
                fieldName);
        }
    }

    private async Task<bool> EvaluateAsync(
        IResolverContext resolverContext,
        IProjectAuthorizationService authorizationService,
        Guid projectId,
        IReadOnlyCollection<PermissionType> requiredPermissions,
        CancellationToken cancellationToken)
    {
        var sawAllowedPermission = false;

        foreach (var permission in requiredPermissions)
        {
            var granted = await ProjectGraphQLPermissionCache.HasPermissionAsync(
                resolverContext,
                authorizationService,
                projectId,
                permission,
                cancellationToken).ConfigureAwait(false);

            if (Mode == ProjectPermissionEvaluationMode.All && !granted)
            {
                return false;
            }

            if (Mode == ProjectPermissionEvaluationMode.Any && granted)
            {
                return true;
            }

            sawAllowedPermission |= granted;
        }

        return Mode == ProjectPermissionEvaluationMode.All || sawAllowedPermission;
    }

    private PermissionType[]? TryResolveRequiredPermissions(IResolverContext context)
    {
        if (_permissionSwitchArgumentName is null)
        {
            return _permissions;
        }

        if (_permissionWhenSwitchFalse is null)
        {
            return null;
        }

        bool switchValue;
        try
        {
            switchValue = context.ArgumentValue<bool>(_permissionSwitchArgumentName);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        return switchValue ? _permissions : [_permissionWhenSwitchFalse.Value];
    }

    private static Guid? TryResolveProjectId(
        IResolverContext context,
        string argumentPath,
        string? parentPropertyPath)
    {
        var path = (parentPropertyPath ?? argumentPath)
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

    private static GraphQLException CreateAuthorizationError() => new(
        ErrorBuilder.New()
            .SetMessage("Not authorized to access this resource.")
            .SetCode("AUTHORIZATION_DENIED")
            .Build());
}
