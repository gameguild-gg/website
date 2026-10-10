using GameGuild.Configuration.PresentationLayer.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MsAuthorizationOptions = Microsoft.AspNetCore.Authorization.AuthorizationOptions;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Dynamic authorization policy provider that loads policies from the database
///     with tenant-aware caching and version-based invalidation.
///     Registered as Singleton to satisfy ASP.NET Core requirements.
///     Uses IServiceScopeFactory to resolve scoped services (DbContext, repositories).
/// </summary>
public sealed class DbAuthorizationPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly MsAuthorizationOptions _authzOptions;
    private readonly IPolicyCache _policyCache;
    private readonly IPolicyMerger _policyMerger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TenancyOptions _tenancyOptions;
    private readonly ILogger<DbAuthorizationPolicyProvider> _logger;

    /// <summary>
    ///     Initializes a new instance of <see cref="DbAuthorizationPolicyProvider"/>.
    /// </summary>
    public DbAuthorizationPolicyProvider(
        IOptions<MsAuthorizationOptions> authzOptions,
        IPolicyCache policyCache,
        IPolicyMerger policyMerger,
        IServiceScopeFactory scopeFactory,
        IOptions<TenancyOptions> tenancyOptions,
        ILogger<DbAuthorizationPolicyProvider> logger)
    {
        _authzOptions = authzOptions.Value;
        _policyCache = policyCache;
        _policyMerger = policyMerger;
        _scopeFactory = scopeFactory;
        _tenancyOptions = tenancyOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var builtInPolicy = _authzOptions.GetPolicy(policyName);
        if (!Policies.IsValid(policyName) && builtInPolicy is not null)
        {
            return builtInPolicy;
        }

        // Resolve tenant context and scoped services within a scope
        string tenantId;
        long version;
        PolicyDefinition? baseDefinition;
        PolicyDefinition? tenantDefinition;

        using (var scope = _scopeFactory.CreateScope())
        {
            var tenantContext = scope.ServiceProvider.GetService<IAuthorizationTenantContext>();
            // TenantId is now Guid? - convert to string for cache key, or use default
            tenantId = tenantContext?.TenantId?.ToString() ?? _tenancyOptions.DefaultTenantId;

            var versionStore = scope.ServiceProvider.GetRequiredService<ITenantSecurityVersionStore>();
            version = await versionStore.GetVersionAsync(tenantId).ConfigureAwait(false);

            // Check cache first (before loading from database)
            var cachedPolicy = _policyCache.Get(policyName, tenantId, version);
            if (cachedPolicy is not null)
            {
                _logger.LogDebug("Policy '{PolicyName}' loaded from cache for tenant '{TenantId}'",
                    policyName, tenantId);
                return cachedPolicy;
            }

            // Load from store (scoped service)
            var policyStore = scope.ServiceProvider.GetRequiredService<IPolicyDefinitionStore>();
            
            baseDefinition = await policyStore.GetPolicyAsync(
                policyName,
                tenantId: null).ConfigureAwait(false);

            if (baseDefinition is null)
            {
                var denyPolicy = CreateFailClosedPolicy();
                _policyCache.Set(policyName, tenantId, version, denyPolicy);
                _logger.LogError(
                    "Registered policy '{PolicyName}' is missing from the policy store for tenant '{TenantId}'. Denying access.",
                    policyName,
                    tenantId);
                return denyPolicy;
            }

            tenantDefinition = !string.Equals(tenantId, _tenancyOptions.DefaultTenantId, StringComparison.Ordinal)
                ? await policyStore.GetPolicyAsync(policyName, tenantId).ConfigureAwait(false)
                : null;
        }

        // Merge and build (can be done outside the scope)
        var mergedDefinition = _policyMerger.Merge(baseDefinition, tenantDefinition);
        var policy = _policyMerger.Build(mergedDefinition);

        // Cache the compiled policy
        _policyCache.Set(policyName, tenantId, version, policy);

        _logger.LogDebug("Policy '{PolicyName}' compiled and cached for tenant '{TenantId}'",
            policyName, tenantId);

        return policy;
    }

    /// <inheritdoc />
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return Task.FromResult(_authzOptions.DefaultPolicy);
    }

    /// <inheritdoc />
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return Task.FromResult(_authzOptions.FallbackPolicy);
    }

    private static AuthorizationPolicy CreateFailClosedPolicy() =>
        new AuthorizationPolicyBuilder()
            .RequireAssertion(_ => false)
            .Build();
}
