using System.Text.Json;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Database-backed implementation of policy definition store.
///     Uses the IPolicyDefinitionRepository for persistence.
/// </summary>
public sealed class DatabasePolicyDefinitionStore(IPolicyDefinitionRepository repository) : IPolicyDefinitionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public async Task<PolicyDefinition?> GetPolicyAsync(
        string policyName,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        Guid? tenantGuid = string.IsNullOrEmpty(tenantId) ? null : Guid.TryParse(tenantId, out var g) ? g : null;
        
        if (!string.IsNullOrEmpty(tenantId) && !tenantGuid.HasValue)
        {
            return null;
        }

        var entity = await repository.GetByNameAsync(policyName, tenantGuid, cancellationToken).ConfigureAwait(false);
        if (tenantGuid.HasValue && entity?.TenantId != tenantGuid)
        {
            return null;
        }

        return entity == null ? null : MapToDefinition(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PolicyDefinition>> GetTenantPoliciesAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(tenantId, out var tenantGuid))
        {
            return [];
        }

        var entities = await repository.GetByTenantAsync(tenantGuid, includeGlobal: true, cancellationToken).ConfigureAwait(false);
        
        return entities.Select(MapToDefinition).ToList();
    }

    /// <inheritdoc />
    public async Task<long> GetVersionAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(tenantId, out var tenantGuid))
        {
            return 0;
        }

        var policies = await repository.GetByTenantAsync(tenantGuid, includeGlobal: false, cancellationToken).ConfigureAwait(false);
        
        // Return the max version across all tenant policies
        return policies.Count > 0 ? policies.Max(p => p.PolicyVersion) : 0;
    }

    /// <summary>
    ///     Maps a PolicyDefinitionEntity to a PolicyDefinition value object.
    /// </summary>
    private static PolicyDefinition MapToDefinition(PolicyDefinitionEntity entity)
    {
        var authenticationSchemes = DeserializeList(entity.AuthenticationSchemesJson);
        var requiredPermissions = DeserializeList(entity.RequiredPermissionsJson);
        var requiredRoles = DeserializeList(entity.RequiredRolesJson);
        var rules = DeserializeRules(entity.RulesJson);

        return new PolicyDefinition
        {
            PolicyName = entity.PolicyName,
            RequireAuthentication = entity.RequireAuthentication,
            AuthenticationSchemes = authenticationSchemes.Values,
            RequiredPermissions = requiredPermissions.Values,
            RequiredRoles = requiredRoles.Values,
            RequireAccessControlListAccess = entity.RequireAccessControlListAccess,
            ResourceType = entity.ResourceType,
            MinimumAccessLevel = entity.MinimumAccessLevel,
            IsTenantScoped = entity.IsTenantScoped,
            Version = entity.PolicyVersion,
            UseRuleBasedEvaluation = entity.UseRuleBasedEvaluation,
            Rules = rules.Values,
            IsConfigurationValid = authenticationSchemes.IsValid &&
                                   requiredPermissions.IsValid &&
                                   requiredRoles.IsValid &&
                                   rules.IsValid
        };
    }

    private static (IReadOnlyList<string> Values, bool IsValid) DeserializeList(string? json)
    {
        if (string.IsNullOrEmpty(json) || json == "[]")
        {
            return ([], true);
        }

        try
        {
            var values = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
            return values is null ? ([], false) : (values, true);
        }
        catch
        {
            return ([], false);
        }
    }

    private static (IReadOnlyList<PolicyRule>? Values, bool IsValid) DeserializeRules(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return (null, true);
        }

        try
        {
            var rules = JsonSerializer.Deserialize<List<RuleDto>>(json, JsonOptions);
            return rules is null
                ? (null, false)
                : (rules.Select(MapRule).ToList(), true);
        }
        catch
        {
            return (null, false);
        }
    }

    private static PolicyRule MapRule(RuleDto rule) => new()
    {
        Type = rule.Type ?? string.Empty,
        Description = rule.Description,
        Params = rule.Params?.ToDictionary(
            kvp => kvp.Key,
            kvp => (object)kvp.Value),
        Rules = rule.Rules?.Select(MapRule).ToList(),
        Enabled = rule.Enabled
    };

    /// <summary>
    ///     DTO for deserializing rules from JSON.
    /// </summary>
    // ReSharper disable UnusedAutoPropertyAccessor.Local - Properties set by JSON deserializer
    // ReSharper disable CollectionNeverUpdated.Local - Collection populated by JSON deserializer
    private sealed class RuleDto
    {
        public string? Type { get; set; }
        public string? Description { get; set; }
        public Dictionary<string, JsonElement>? Params { get; set; }
        public List<RuleDto>? Rules { get; set; }
        public bool Enabled { get; set; } = true;
    }
    // ReSharper restore CollectionNeverUpdated.Local
    // ReSharper restore UnusedAutoPropertyAccessor.Local
}
