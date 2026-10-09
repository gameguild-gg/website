using System.Collections.Concurrent;
using System.Text.Json;
using GameGuild.Configuration.PresentationLayer.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Policy cache implementation with optional distributed cache support.
///     Uses memory cache as L1 and distributed cache (Redis) as L2 when enabled.
/// </summary>
public sealed class MemoryPolicyCache : IPolicyCache
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache? _distributedCache;
    private readonly AuthorizationCacheOptions _options;
    private readonly ILogger<MemoryPolicyCache>? _logger;
    private readonly ConcurrentDictionary<string, HashSet<string>> _tenantKeys = new();

    /// <summary>
    ///     Initializes a new instance of <see cref="MemoryPolicyCache"/>.
    /// </summary>
    public MemoryPolicyCache(
        IMemoryCache memoryCache,
        IOptions<AuthorizationCacheOptions> options,
        IDistributedCache? distributedCache = null,
        ILogger<MemoryPolicyCache>? logger = null)
    {
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    ///     Gets whether distributed caching is enabled and available.
    /// </summary>
    private bool UseDistributedCache => _options.UseDistributedCache && _distributedCache is not null;

    /// <inheritdoc />
    public AuthorizationPolicy? Get(string policyName, string tenantId, long version)
    {
        var key = BuildKey(policyName, tenantId, version);

        // L1: Try memory cache first
        if (_memoryCache.TryGetValue(key, out AuthorizationPolicy? policy))
        {
            return policy;
        }

        // L2: Try distributed cache if enabled
        if (UseDistributedCache)
        {
            try
            {
                var cachedData = _distributedCache!.GetString(key);
                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedPolicy = DeserializePolicy(cachedData);
                    if (cachedPolicy is not null)
                    {
                        // Populate L1 cache
                        SetInMemory(key, tenantId, cachedPolicy);
                        return cachedPolicy;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to retrieve policy from distributed cache: {Key}", key);
                // Fall through to return null
            }
        }

        return null;
    }

    /// <inheritdoc />
    public void Set(string policyName, string tenantId, long version, AuthorizationPolicy policy)
    {
        var key = BuildKey(policyName, tenantId, version);

        // L1: Set in memory cache
        SetInMemory(key, tenantId, policy);

        // L2: Set in distributed cache if enabled
        if (UseDistributedCache)
        {
            try
            {
                var serialized = SerializePolicy(policy);
                var distributedOptions = new DistributedCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromSeconds(_options.PolicyTtlSeconds)
                };
                _distributedCache!.SetString(key, serialized, distributedOptions);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to store policy in distributed cache: {Key}", key);
                // Continue - memory cache still works
                throw;
            }
        }
    }

    /// <inheritdoc />
    public void Invalidate(string tenantId)
    {
        // L1: Remove from memory cache
        if (_tenantKeys.TryRemove(tenantId, out var keys))
        {
            foreach (var key in keys)
            {
                _memoryCache.Remove(key);

                // L2: Remove from distributed cache if enabled
                if (UseDistributedCache)
                {
                    try
                    {
                        _distributedCache!.Remove(key);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to remove policy from distributed cache: {Key}", key);
                        throw;
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public void Invalidate(string policyName, string tenantId)
    {
        if (_tenantKeys.TryGetValue(tenantId, out var keys))
        {
            var pattern = $"{policyName}|{tenantId}|";
            var keysToRemove = keys.Where(k => k.StartsWith(pattern, StringComparison.Ordinal)).ToList();

            foreach (var key in keysToRemove)
            {
                _memoryCache.Remove(key);
                keys.Remove(key);

                // L2: Remove from distributed cache if enabled
                if (UseDistributedCache)
                {
                    try
                    {
                        _distributedCache!.Remove(key);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to remove policy from distributed cache: {Key}", key);
                        throw;
                    }
                }
            }
        }
    }

    private void SetInMemory(string key, string tenantId, AuthorizationPolicy policy)
    {
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromSeconds(_options.PolicyTtlSeconds))
            .SetSize(1);

        _memoryCache.Set(key, policy, cacheOptions);
        TrackTenantKey(tenantId, key);
    }

    private static string BuildKey(string policyName, string tenantId, long version)
        => $"policy:{policyName}|{tenantId}|{version}";

    private void TrackTenantKey(string tenantId, string key)
    {
        var keys = _tenantKeys.GetOrAdd(tenantId, _ => []);
        lock (keys)
        {
            keys.Add(key);
        }
    }

    /// <summary>
    ///     Serializes an AuthorizationPolicy for distributed cache storage.
    ///     Note: We serialize the metadata, not the compiled policy handlers.
    /// </summary>
    private static string SerializePolicy(AuthorizationPolicy policy)
    {
        // Store essential policy data that can be reconstructed
        var dto = new CachedPolicyDto
        {
            AuthenticationSchemes = policy.AuthenticationSchemes.ToList(),
            RequireAuthenticatedUser = policy.Requirements.OfType<Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement>().Any(),
            RequirementTypes = policy.Requirements.Select(r => r.GetType().AssemblyQualifiedName ?? r.GetType().FullName ?? "Unknown").ToList()
        };
        return JsonSerializer.Serialize(dto);
    }

    /// <summary>
    ///     Deserializes a cached policy.
    ///     Note: For complex policies with custom requirements, the policy needs to be rebuilt.
    ///     This is a simplified implementation that works for basic authentication-only policies.
    /// </summary>
    private static AuthorizationPolicy? DeserializePolicy(string data)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<CachedPolicyDto>(data);
            if (dto is null)
            {
                return null;
            }

            var builder = new AuthorizationPolicyBuilder();

            if (dto.AuthenticationSchemes.Count > 0)
            {
                builder.AddAuthenticationSchemes(dto.AuthenticationSchemes.ToArray());
            }

            if (dto.RequireAuthenticatedUser)
            {
                builder.RequireAuthenticatedUser();
            }

            return builder.Build();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     DTO for serializing policy data to distributed cache.
    /// </summary>
    private sealed class CachedPolicyDto
    {
        public List<string> AuthenticationSchemes { get; set; } = [];
        public bool RequireAuthenticatedUser { get; set; }
        public List<string> RequirementTypes { get; set; } = [];
    }
}
