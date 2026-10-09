using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Provides rulesets for policies from the database with caching.
/// </summary>
public sealed class RulesetProvider : IRulesetProvider
{
    private const string CacheKeyPrefix = "ruleset:";
    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Tracks all cache keys for proper invalidation.
    ///     Using ConcurrentDictionary as a thread-safe set.
    /// </summary>
    private static readonly ConditionalWeakTable<IMemoryCache, ConcurrentDictionary<string, byte>> CacheKeySets = new();

    private readonly ConcurrentDictionary<string, byte> _cacheKeys;
    private readonly ILogger<RulesetProvider> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly IPolicyDefinitionRepository _policyRepository;

    public RulesetProvider(
        IPolicyDefinitionRepository policyRepository,
        IMemoryCache memoryCache,
        ILogger<RulesetProvider> logger)
    {
        _policyRepository = policyRepository;
        _memoryCache = memoryCache;
        _cacheKeys = CacheKeySets.GetValue(memoryCache, _ => new ConcurrentDictionary<string, byte>());
        _logger = logger;
    }

    public async Task<PolicyRuleset?> GetRulesetAsync(
        string policyName,
        CancellationToken cancellationToken = default)
    {
        return await GetRulesetForTenantAsync(policyName, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PolicyRuleset?> GetRulesetForTenantAsync(
        string policyName,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = tenantId.HasValue
            ? $"{CacheKeyPrefix}{policyName}:{tenantId}"
            : $"{CacheKeyPrefix}{policyName}";

        if (_memoryCache.TryGetValue(cacheKey, out PolicyRuleset? cached))
        {
            return cached;
        }

        // Get policy from repository (it handles tenant fallback internally)
        var entity = await _policyRepository.GetByNameAsync(policyName, tenantId, cancellationToken).ConfigureAwait(false);

        if (entity is null)
        {
            _logger.LogDebug("No policy found for {PolicyName}", policyName);
            return null;
        }

        var ruleset = ConvertToRuleset(entity);

        // Track the cache key for proper invalidation
        _cacheKeys.TryAdd(cacheKey, 0);

        _memoryCache.Set(cacheKey, ruleset, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = DefaultCacheDuration,
            PostEvictionCallbacks =
            {
                new PostEvictionCallbackRegistration
                {
                    EvictionCallback = (key, _, _, _) =>
                    {
                        if (key is string keyStr)
                        {
                            _cacheKeys.TryRemove(keyStr, out _);
                        }
                    }
                }
            }
        });

        return ruleset;
    }

    public void InvalidatePolicy(string policyName)
    {
        // Find all cache keys for this policy (base and tenant-specific)
        var keysToRemove = _cacheKeys.Keys
            .Where(k => k.StartsWith($"{CacheKeyPrefix}{policyName}", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _memoryCache.Remove(key);
            _cacheKeys.TryRemove(key, out _);
        }

        _logger.LogDebug("Invalidated {Count} cache entries for policy {PolicyName}", keysToRemove.Count, policyName);
    }

    public void InvalidateAll()
    {
        var keysToRemove = _cacheKeys.Keys.ToList();
        var count = keysToRemove.Count;

        foreach (var key in keysToRemove)
        {
            _memoryCache.Remove(key);
            _cacheKeys.TryRemove(key, out _);
        }

        _logger.LogInformation("Invalidated all {Count} cached rulesets", count);
    }

    private PolicyRuleset ConvertToRuleset(PolicyDefinitionEntity entity)
    {
        var rules = new List<RuleDefinition>();
        var configurationIsValid = true;

        if (!string.IsNullOrEmpty(entity.RulesJson))
        {
            try
            {
                var parsedRules = JsonSerializer.Deserialize<List<RuleDefinition>>(entity.RulesJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (parsedRules is not null)
                {
                    rules = parsedRules;
                }
                else
                {
                    configurationIsValid = false;
                }
            }
            catch (JsonException ex)
            {
                configurationIsValid = false;
                _logger.LogWarning(ex,
                    "Failed to parse RulesJson for policy {PolicyName}",
                    entity.PolicyName);
            }
        }

        var permissionsResult = ParseJsonArray(entity.RequiredPermissionsJson);
        configurationIsValid &= permissionsResult.IsValid;
        if (permissionsResult.Values.Count > 0)
        {
            rules.Add(new RuleDefinition
            {
                Type = RuleTypes.RequireAllPermissions,
                Description = "Require all specified permissions",
                Params = new Dictionary<string, JsonElement>
                {
                    ["permissions"] = JsonSerializer.SerializeToElement(permissionsResult.Values)
                },
                Enabled = true
            });
        }

        var rolesResult = ParseJsonArray(entity.RequiredRolesJson);
        configurationIsValid &= rolesResult.IsValid;
        if (rolesResult.Values.Count > 0)
        {
            rules.Add(new RuleDefinition
            {
                Type = RuleTypes.RequireAllPermissions,
                Description = "Require all specified roles (as permissions)",
                Params = new Dictionary<string, JsonElement>
                {
                    ["permissions"] = JsonSerializer.SerializeToElement(
                        rolesResult.Values.Select(role => $"role:{role}").ToList())
                },
                Enabled = true
            });
        }

        if (entity.RequireAccessControlListAccess)
        {
            rules.Add(new RuleDefinition
            {
                Type = RuleTypes.OwnerOrAcl,
                Description = "Check ACL access",
                Params = new Dictionary<string, JsonElement>
                {
                    ["allowOwner"] = JsonSerializer.SerializeToElement(false),
                    ["minimumAccessLevel"] = JsonSerializer.SerializeToElement(
                        entity.MinimumAccessLevel ?? "Read")
                },
                Enabled = true
            });
        }

        return new PolicyRuleset
        {
            Name = entity.PolicyName,
            Description = entity.Description,
            RequireAuthentication = entity.RequireAuthentication,
            Rules = rules,
            Version = entity.PolicyVersion,
            IsActive = entity.IsActive && configurationIsValid
        };
    }

    private (List<string> Values, bool IsValid) ParseJsonArray(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return ([], true);
        }

        try
        {
            var values = JsonSerializer.Deserialize<List<string>>(json);
            return values is null ? ([], false) : (values, true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON array: {Json}", json);
            return ([], false);
        }
    }
}
