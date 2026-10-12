using System.Collections.Concurrent;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.GraphQL;
using HotChocolate.Types;

namespace GameGuild.API.Core.GraphQL;

/// <summary>
///     One parsed <c>@authorize</c> directive application for a field (issue #335).
///     Permission keys are kept as strings here and parsed against
///     <see cref="PermissionType"/> at evaluation time so that unknown keys fail closed
///     at runtime instead of crashing schema compilation.
/// </summary>
internal sealed record GraphQLAuthorizeFieldRule
{
    public required string[] Permissions { get; init; }

    public string? Policy { get; init; }

    public required string[] Roles { get; init; }

    public GraphQLAuthorizationMode Mode { get; init; }

    public bool MaskUnauthorized { get; init; }

    public string? ResourceType { get; init; }

    public string? ResourceIdArgumentName { get; init; }

    public string? ResourceIdParentPropertyName { get; init; }

    /// <summary>True when the rule requires a resource-scoped permission evaluation.</summary>
    public bool IsResourceScoped => ResourceType is not null;

    /// <summary>True when the rule carries permission requirements (vs policy/roles only).</summary>
    public bool HasPermissions => Permissions.Length > 0;

    public static GraphQLAuthorizeFieldRule FromDirective(GraphQLAuthorizeDirective directive) => new()
    {
        Permissions = directive.Permissions ?? [],
        Policy = directive.Policy,
        Roles = directive.Roles ?? [],
        Mode = directive.Mode,
        MaskUnauthorized = directive.MaskUnauthorized,
        ResourceType = directive.ResourceType,
        ResourceIdArgumentName = directive.ResourceIdArgumentName,
        ResourceIdParentPropertyName = directive.ResourceIdParentPropertyName,
    };
}

/// <summary>
///     Static per-schema cache of <c>@authorize</c> rules. Schema type objects are
///     compiled singletons, so the cache is keyed by <see cref="IObjectField"/> and never
/// /// evicted (bounded by the schema's protected-field count).
/// </summary>
internal static class GraphQLAuthorizeFieldRules
{
    private static readonly ConcurrentDictionary<IObjectField, GraphQLAuthorizeFieldRule[]> Cache = new();

    public static GraphQLAuthorizeFieldRule[] Get(IObjectField field) =>
        Cache.GetOrAdd(field, static f => ParseFieldRules(f));

    private static GraphQLAuthorizeFieldRule[] ParseFieldRules(IObjectField field)
    {
        var values = new List<GraphQLAuthorizeFieldRule>();
        foreach (var directive in field.Directives)
        {
            if (!string.Equals(directive.Type.Name, GraphQLAuthorizeDirective.DirectiveName, StringComparison.Ordinal))
            {
                continue;
            }

            var value = directive.AsValue<GraphQLAuthorizeDirective>();
            if (value is not null)
            {
                values.Add(GraphQLAuthorizeFieldRule.FromDirective(value));
            }
        }

        return values.ToArray();
    }

    /// <summary>
    ///     Collects every protected field's rules in the schema (depth-first type walk)
    ///     for schema-visibility evaluation.
    /// </summary>
    public static IReadOnlyList<(IObjectField Field, GraphQLAuthorizeFieldRule[] Rules)> CollectProtectedFields(
        ISchema schema)
    {
        var protectedFields = new List<(IObjectField, GraphQLAuthorizeFieldRule[])>();
        var visited = new HashSet<INamedType>();
        var stack = new Stack<INamedType>();

        foreach (var type in schema.Types)
        {
            stack.Push(type);
        }

        while (stack.Count > 0)
        {
            var type = stack.Pop();
            if (!visited.Add(type) || type is not ObjectType objectType)
            {
                continue;
            }

            foreach (var field in objectType.Fields)
            {
                var rules = Get(field);
                if (rules.Length > 0)
                {
                    protectedFields.Add((field, rules));
                }
            }

            // Depth-first continuation through output-field return types.
            foreach (var field in objectType.Fields)
            {
                var named = field.Type.NamedType();
                if (!visited.Contains(named))
                {
                    stack.Push(named);
                }
            }
        }

        return protectedFields;
    }
}
