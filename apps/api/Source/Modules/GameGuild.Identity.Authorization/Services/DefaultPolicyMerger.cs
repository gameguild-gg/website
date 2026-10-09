using System.Text.Json;

using Microsoft.AspNetCore.Authorization;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Default implementation of policy merger that combines base and tenant policies.
/// </summary>
public sealed class DefaultPolicyMerger : IPolicyMerger
{
    /// <inheritdoc />
    public PolicyDefinition Merge(PolicyDefinition basePolicy, PolicyDefinition? tenantOverride)
    {
        if (tenantOverride is null)
        {
            return basePolicy;
        }

        // Tenant override takes precedence, with fallback to base
        var resourceType = tenantOverride.ResourceType;
        if (resourceType is null)
        {
            resourceType = basePolicy.ResourceType;
        }

        var minimumAccessLevel = tenantOverride.MinimumAccessLevel;
        if (minimumAccessLevel is null)
        {
            minimumAccessLevel = basePolicy.MinimumAccessLevel;
        }

        return new PolicyDefinition
        {
            PolicyName = basePolicy.PolicyName,
            RequireAuthentication = tenantOverride.RequireAuthentication | basePolicy.RequireAuthentication,
            AuthenticationSchemes = MergeCollections(
                basePolicy.AuthenticationSchemes,
                tenantOverride.AuthenticationSchemes),
            RequiredPermissions = MergeCollections(
                basePolicy.RequiredPermissions,
                tenantOverride.RequiredPermissions),
            RequiredRoles = MergeCollections(
                basePolicy.RequiredRoles,
                tenantOverride.RequiredRoles),
            RequireAccessControlListAccess = tenantOverride.RequireAccessControlListAccess | basePolicy.RequireAccessControlListAccess,
            ResourceType = resourceType,
            MinimumAccessLevel = minimumAccessLevel,
            IsTenantScoped = true,
            Version = Math.Max(basePolicy.Version, tenantOverride.Version),
            UseRuleBasedEvaluation = tenantOverride.UseRuleBasedEvaluation | basePolicy.UseRuleBasedEvaluation,
            IsConfigurationValid = basePolicy.IsConfigurationValid && tenantOverride.IsConfigurationValid,
            Rules = MergeRules(basePolicy.Rules, tenantOverride.Rules)
        };
    }

    /// <summary>
    ///     Merges rules from base and tenant policies.
    /// </summary>
    private static IReadOnlyList<PolicyRule>? MergeRules(
        IReadOnlyList<PolicyRule>? baseRules,
        IReadOnlyList<PolicyRule>? tenantRules)
    {
        if (baseRules is null or { Count: 0 })
        {
            return tenantRules;
        }

        if (tenantRules is null or { Count: 0 })
        {
            return baseRules;
        }

        return baseRules.Concat(tenantRules).ToList();
    }

    /// <inheritdoc />
    public AuthorizationPolicy Build(PolicyDefinition definition)
    {
        var builder = new AuthorizationPolicyBuilder();

        if (!definition.IsConfigurationValid)
        {
            builder.RequireAssertion(_ => false);
        }

        if (definition.RequireAuthentication)
        {
            builder.RequireAuthenticatedUser();
        }

        if (definition.AuthenticationSchemes.Count > 0)
        {
            builder.AddAuthenticationSchemes(definition.AuthenticationSchemes.ToArray());
        }

        if (definition.UseRuleBasedEvaluation)
        {
            if (definition.Rules is { Count: > 0 })
            {
                var ruleset = new PolicyRuleset
                {
                    Name = definition.PolicyName,
                    Description = null,
                    RequireAuthentication = definition.RequireAuthentication,
                    Rules = ConvertToRuleDefinitions(definition.Rules),
                    Version = definition.Version,
                    IsActive = true
                };

                builder.AddRequirements(new RulesetRequirement(definition.PolicyName, ruleset));
            }
            else
            {
                builder.RequireAssertion(_ => false);
            }
        }

        if (definition.RequiredRoles.Count > 0)
        {
            builder.RequireRole(definition.RequiredRoles.ToArray());
        }

        foreach (var permission in definition.RequiredPermissions)
        {
            builder.AddRequirements(new PermissionRequirement(permission));
        }

        if (definition.RequireAccessControlListAccess)
        {
            if (Enum.TryParse<AccessLevel>(definition.MinimumAccessLevel, true, out var minimumAccessLevel))
            {
                builder.AddRequirements(new ResourceAccessRequirement(
                    requireAccessControlListAccess: true,
                    minimumAccessLevel: minimumAccessLevel,
                    resourceType: definition.ResourceType));
            }
            else
            {
                builder.RequireAssertion(_ => false);
            }
        }

        if (builder.Requirements.Count == 0)
        {
            builder.RequireAssertion(_ => definition.PolicyName == Policies.Anonymous);
        }

        return builder.Build();
    }

    private static IReadOnlyList<T> MergeCollections<T>(
        IReadOnlyList<T> baseList,
        IReadOnlyList<T> overrideList)
    {
        if (overrideList.Count == 0)
        {
            return baseList;
        }

        if (baseList.Count == 0)
        {
            return overrideList;
        }

        return baseList.Concat(overrideList).Distinct().ToList();
    }

    /// <summary>
    ///     Converts PolicyRule objects to RuleDefinition objects.
    /// </summary>
    private static IReadOnlyList<RuleDefinition> ConvertToRuleDefinitions(IReadOnlyList<PolicyRule>? policyRules)
    {
        if (policyRules is null or { Count: 0 })
        {
            return Array.Empty<RuleDefinition>();
        }

        var ruleDefinitions = new List<RuleDefinition>(policyRules.Count);

        foreach (var policyRule in policyRules)
        {
            var ruleDefinition = new RuleDefinition
            {
                Type = policyRule.Type,
                Description = policyRule.Description,
                Params = ConvertParams(policyRule.Params),
                Rules = ConvertToRuleDefinitions(policyRule.Rules),
                Enabled = policyRule.Enabled
            };

            ruleDefinitions.Add(ruleDefinition);
        }

        return ruleDefinitions;
    }

    /// <summary>
    ///     Converts parameter dictionary from object to JsonElement.
    /// </summary>
    private static Dictionary<string, JsonElement>? ConvertParams(IReadOnlyDictionary<string, object>? sourceParams)
    {
        if (sourceParams is null or { Count: 0 })
        {
            return null;
        }

        var result = new Dictionary<string, JsonElement>(sourceParams.Count);

        foreach (var (key, value) in sourceParams)
        {
            // Serialize to JSON and deserialize as JsonElement
            var json = JsonSerializer.Serialize(value);
            var element = JsonSerializer.Deserialize<JsonElement>(json);
            result[key] = element;
        }

        return result;
    }
}
