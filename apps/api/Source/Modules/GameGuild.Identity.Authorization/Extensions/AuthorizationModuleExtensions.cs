

using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authorization.Caching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PresentationAuthorizationOptions = GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Extension methods for registering authorization module services.
/// </summary>
public static class AuthorizationModuleExtensions
{
    /// <summary>
    ///     Registers authorization configuration options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAuthorizationOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<TenancyOptions>(
            configuration.GetSection(TenancyOptions.SectionName));

        services.Configure<PresentationAuthorizationOptions>(
            configuration.GetSection(PresentationAuthorizationOptions.SectionName));

        services.Configure<AuthorizationCacheOptions>(
            configuration.GetSection(AuthorizationCacheOptions.SectionName));

        services.Configure<AuthorizationTokenOptions>(
            configuration.GetSection(AuthorizationTokenOptions.SectionName));

        services.Configure<PolicyBundleSigningOptions>(
            configuration.GetSection(PolicyBundleSigningOptions.SectionName));

        return services;
    }

    /// <summary>
    ///     Registers authorization application layer services (core business logic).
    ///     Always uses database as the source of truth with optional caching layer.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="enableCaching">If true, wraps stores with caching layer (recommended for production).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAuthorizationApplication(
        this IServiceCollection services, 
        bool enableCaching = true)
    {
        // Policy cache for compiled AuthorizationPolicy objects (always enabled)
        services.AddSingleton<IPolicyCache, MemoryPolicyCache>();
        services.AddSingleton<IPolicyMerger, DefaultPolicyMerger>();

        // Database as primary storage (source of truth)
        // Register the database implementations first
        services.AddScoped<DatabasePolicyDefinitionStore>();
        services.AddScoped<DatabaseAccessControlListService>();
        services.AddScoped<DatabaseTenantSecurityVersionStore>();

        // Tenant security version store (used for cache invalidation)
        services.AddScoped<ITenantSecurityVersionStore, DatabaseTenantSecurityVersionStore>();
        
        // User security version store (used for user-specific cache invalidation)
        services.AddSingleton<IUserSecurityVersionStore, InMemoryUserSecurityVersionStore>();

        if (enableCaching)
        {
            // Register caching infrastructure
            services.AddAuthorizationCaching();
            
            // Cached wrappers around database stores for fast reads
            services.AddScoped<IPolicyDefinitionStore>(sp =>
            {
                var innerStore = sp.GetRequiredService<DatabasePolicyDefinitionStore>();
                var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                var versionStore = sp.GetRequiredService<ITenantSecurityVersionStore>();
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthorizationCacheOptions>>();
                var hybridCache = sp.GetService<IHybridPermissionCache>();
                var metrics = sp.GetService<ICacheMetricsService>();
                return new CachedPolicyDefinitionStore(innerStore, cache, versionStore, options, hybridCache, metrics);
            });

            services.AddScoped<IAccessControlListService>(sp =>
            {
                var innerService = sp.GetRequiredService<DatabaseAccessControlListService>();
                var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                var tenantVersionStore = sp.GetRequiredService<ITenantSecurityVersionStore>();
                var userVersionStore = sp.GetRequiredService<IUserSecurityVersionStore>();
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthorizationCacheOptions>>();
                var hybridCache = sp.GetService<IHybridPermissionCache>();
                var metrics = sp.GetService<ICacheMetricsService>();
                var keyTracker = sp.GetRequiredService<IPermissionCacheKeyTracker>();
                var invalidationService = sp.GetRequiredService<ICacheInvalidationService>();
                var popularityTracker = sp.GetService<IPermissionCachePopularityTracker>();
                return new CachedAccessControlListService(
                    innerService,
                    cache,
                    tenantVersionStore,
                    userVersionStore,
                    options,
                    hybridCache,
                    metrics,
                    keyTracker,
                    invalidationService,
                    popularityTracker);
            });

            services.AddScoped<IPermissionCacheWarmupService, PermissionCacheWarmupService>();
            services.AddHostedService<AutomaticPermissionCacheWarmupService>();
        }
        else
        {
            // Direct database access without caching (useful for debugging)
            services.AddScoped<IPolicyDefinitionStore, DatabasePolicyDefinitionStore>();
            services.AddScoped<IAccessControlListService, DatabaseAccessControlListService>();
        }

        // Permission service adapter (composite interface for backward compatibility)
        services.AddScoped<IAuthorizationPermissionService, AuthorizationPermissionServiceAdapter>();
        
        // ISP-compliant focused interfaces (prefer these for new code)
        // These resolve to the same implementation via the composite interface
        services.AddScoped<IAuthorizationSinglePermissionChecker>(sp => sp.GetRequiredService<IAuthorizationPermissionService>());
        services.AddScoped<IAuthorizationPermissionResolver>(sp => sp.GetRequiredService<IAuthorizationPermissionService>());
        services.AddScoped<IAuthorizationBatchPermissionChecker>(sp => sp.GetRequiredService<IAuthorizationPermissionService>());

        return services;
    }

    /// <summary>
    ///     Registers authorization repository implementations for database-backed storage.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAuthorizationRepositories(this IServiceCollection services)
    {
        services.AddScoped<IPolicyDefinitionRepository, PolicyDefinitionRepository>();
        services.AddScoped<IAccessControlListEntryRepository, AccessControlListEntryRepository>();
        services.AddScoped<ITenantSecurityVersionRepository, TenantSecurityVersionRepository>();

        // Policy seeder for default policy definitions
        services.AddScoped<PolicyDefinitionSeeder>();

        return services;
    }

    /// <summary>
    ///     Registers a domain policy seed contributor whose policies are seeded alongside the
    ///     platform defaults by <see cref="PolicyDefinitionSeeder"/>.
    /// </summary>
    /// <typeparam name="TContributor">The contributor implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    ///     Register contributors from the host or a domain module; the common module itself
    ///     never registers domain-specific seeds.
    /// </remarks>
    public static IServiceCollection AddPolicySeedContributor<TContributor>(this IServiceCollection services)
        where TContributor : class, IPolicySeedContributor
    {
        services.AddScoped<IPolicySeedContributor, TContributor>();
        return services;
    }

    /// <summary>
    ///     Registers authorization presentation layer services (HTTP/ASP.NET integration).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAuthorizationPresentation(this IServiceCollection services)
    {
        // ClaimsPrincipal abstraction for DIP compliance
        services.AddScoped<IClaimsPrincipalAccessor, HttpContextClaimsPrincipalAccessor>();
        
        // Tenant context and resolver (scoped per request)
        services.AddScoped<HttpAuthorizationTenantContext>();
        services.AddScoped<IAuthorizationTenantContext>(sp => sp.GetRequiredService<HttpAuthorizationTenantContext>());
        services.AddScoped<IAuthorizationTenantResolver, AuthorizationTenantResolver>();

        // Authorization handlers
        services.AddScoped<IAuthorizationHandler, TenantMatchHandler>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.AddScoped<IAuthorizationHandler, EnvironmentHandler>();
        services.AddScoped<IAuthorizationHandler, ResourceAccessHandler>();

        // Resource permission authorization filter for controller attributes
        services.AddResourcePermissionAuthorization();

        // Dynamic policy provider (Singleton - required by ASP.NET Core MVC infrastructure)
        // Uses IServiceScopeFactory to resolve scoped services when needed
        services.AddSingleton<IAuthorizationPolicyProvider, DbAuthorizationPolicyProvider>();

        // Register TimeProvider for environment handler
        services.AddSingleton(TimeProvider.System);

        return services;
    }

    /// <summary>
    ///     Registers rule-based authorization services for DB-driven, tenant-configurable policies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRuleBasedAuthorization(this IServiceCollection services)
    {
        // Ruleset provider (loads rules from database)
        services.AddScoped<IRulesetProvider, RulesetProvider>();

        // Register stateless rule evaluators as singletons
        services.AddSingleton<RequireMfaRuleEvaluator>();
        services.AddSingleton<RequireTimeWindowRuleEvaluator>();
        services.AddSingleton<RequireAnyRoleRuleEvaluator>();

        // Register scoped evaluators that need per-request dependencies
        services.AddScoped<TenantMatchRuleEvaluator>();
        services.AddScoped<RequireAllPermissionsRuleEvaluator>();
        services.AddScoped<RequireAnyPermissionRuleEvaluator>();
        services.AddScoped<SelfOrPermissionRuleEvaluator>();
        services.AddScoped<OwnerOrAclRuleEvaluator>();
        services.AddScoped<RequireIpAllowListRuleEvaluator>();

        // Rule evaluator registry (maps rule types to stateless singleton evaluators)
        services.AddSingleton<IRuleEvaluatorRegistry>(sp =>
        {
            var evaluators = new List<IRuleEvaluator>
            {
                sp.GetRequiredService<RequireMfaRuleEvaluator>(),
                sp.GetRequiredService<RequireTimeWindowRuleEvaluator>(),
                sp.GetRequiredService<RequireAnyRoleRuleEvaluator>()
            };
            return new RuleEvaluatorRegistry(evaluators);
        });

        // Scoped evaluator factory (resolves scoped evaluators dynamically - no hard-coded switch)
        services.AddScoped<IScopedRuleEvaluatorFactory, ScopedRuleEvaluatorFactory>();

        // Ruleset authorization handler (evaluates all rules in a policy)
        services.AddScoped<IAuthorizationHandler, RulesetAuthorizationHandler>();

        return services;
    }

    /// <summary>
    ///     Registers permission management services (tenant permissions, templates, audit).
    ///     These services were migrated from GameGuild.Permissions module.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPermissionServices(this IServiceCollection services)
    {
        // Core permission service (legacy - contains all operations)
        // Suppressed: intentional backward-compatible registration
        #pragma warning disable CS0618
        services.AddScoped<IPermissionService, PermissionService>();
        #pragma warning restore CS0618
        
        // SRP-compliant focused services (new - recommended for new code)
        services.AddScoped<IPermissionGrantService, PermissionGrantService>();
        services.AddScoped<IPermissionQueryService, PermissionQueryService>();
        services.AddScoped<IPermissionBulkService, PermissionBulkService>();
        
        // Tenant membership checker - default fail-closed implementation
        // The Tenants module should override this with an actual implementation
        // Using TryAddScoped so the actual implementation from Tenants module takes precedence
        services.TryAddScoped<ITenantMembershipChecker, FailClosedTenantMembershipChecker>();
        
        // Permission audit service
        services.AddScoped<IPermissionAuditService, PermissionAuditService>();
        
        // Policy evaluation debugging service
        services.AddScoped<IPolicyEvaluationLogger, PolicyEvaluationLogger>();

        // Permission evaluation logging (user, tenant, resource, outcome) for allow and
        // deny decisions; durable sinks are contributed by the host.
        services.TryAddScoped<IPermissionEvaluationLogService, PermissionEvaluationLogService>();
        
        // Repositories
        services.AddScoped<ITenantPermissionRepository, TenantPermissionRepository>();
        services.AddScoped<IPermissionAuditLogRepository, PermissionAuditLogRepository>();

        // Actor context is the primary identity abstraction
        // IActorContextAccessor is registered via AddActorContextIntegration()
        services.AddScoped<ILocalizationContext, LocalizationContext>();

        // CQRS Authorization Behavior
        services.AddScoped(typeof(CQRS.IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));

        return services;
    }

    /// <summary>
    ///     Registers advanced permission services (JIT elevation, delegation, SoD, access reviews).
    ///     These services were migrated from GameGuild.Permissions module.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAdvancedPermissionServices(this IServiceCollection services)
    {
        // JIT Elevation
        services.AddScoped<IJitElevationService, JitElevationService>();
        services.AddScoped<IJitElevationRequestRepository, JitElevationRequestRepository>();

        // Permission Delegation
        services.AddScoped<IPermissionDelegationService, PermissionDelegationService>();
        services.AddScoped<IPermissionDelegationRepository, PermissionDelegationRepository>();

        // Separation of Duties (SoD)
        services.AddScoped<ISoDService, SoDService>();
        services.AddScoped<ISoDRuleRepository, SoDRuleRepository>();
        services.AddScoped<ISoDViolationRepository, SoDViolationRepository>();

        // Access Review
        services.AddScoped<IAccessReviewService, AccessReviewService>();
        services.AddScoped<IAccessReviewCampaignRepository, AccessReviewCampaignRepository>();
        services.AddScoped<IAccessReviewItemRepository, AccessReviewItemRepository>();

        // Delegated Administration
        services.AddScoped<IDelegatedAdminService, DelegatedAdminService>();
        services.AddScoped<IDelegatedAdminScopeRepository, DelegatedAdminScopeRepository>();

        // Analytics
        services.AddScoped<IPermissionAnalyticsService, PermissionAnalyticsService>();

        // Resource Permissions
        services.AddScoped<IResourcePermissionService, ResourcePermissionService>();

        // Advanced repositories
        services.AddScoped<IAbacPolicyRepository, AbacPolicyRepository>();
        services.AddScoped<IConditionalPolicyRepository, ConditionalPolicyRepository>();
        services.AddScoped<IDataMaskingRuleRepository, DataMaskingRuleRepository>();
        services.AddScoped<IDataMaskingService, DataMaskingService>();
        services.AddScoped<IPolicyBundleRepository, PolicyBundleRepository>();
        services.AddScoped<IPolicyBundleDeploymentRepository, PolicyBundleDeploymentRepository>();
        services.AddScoped<IPermissionTemplateVersionRepository, PermissionTemplateVersionRepository>();
        services.AddScoped<IPermissionTemplateMigrationRepository, PermissionTemplateMigrationRepository>();
        services.AddScoped<IPolicyRegistryAuditLogRepository, PolicyRegistryAuditLogRepository>();

        // Central policy registry: signed policy bundles (fail-closed verification)
        services.AddScoped<IPolicyBundleSignatureService, PolicyBundleSignatureService>();
        services.AddScoped<ISignedPolicyBundleStore, SignedPolicyBundleStore>();
        services.AddScoped<IPolicyBundlePolicyMaterializer, PolicyBundlePolicyMaterializer>();

        return services;
    }

    /// <summary>
    ///     Registers the unified 3-layer authorization architecture services.
    ///     Layer 1: Policy Gates (DENY-WINS) - Conditional, ABAC, Environment
    ///     Layer 2: Permission Resolution (ALLOW-WINS) - RBAC, Global, Tenant, Direct
    ///     Layer 3: Permission Check (binary allow/deny)
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddUnifiedAuthorizationLayer(this IServiceCollection services)
    {
        // Dynamic Role repositories (RBAC with hierarchy)
        services.AddScoped<IDynamicRoleRepository, DynamicRoleRepository>();
        services.AddScoped<IDynamicRoleAssignmentRepository, DynamicRoleAssignmentRepository>();

        // Layer 1: Policy Gate evaluators
        services.AddScoped<IConditionalPolicyEvaluator, ConditionalPolicyEvaluator>();
        services.AddScoped<IAbacPolicyEvaluator, AbacPolicyEvaluator>();

        // Layer 1: Unified Policy Gate Service (DENY-WINS)
        services.AddScoped<IPolicyGateService, PolicyGateService>();

        // Layer 2: Permission resolvers
        services.AddScoped<IRbacPermissionResolver, RbacPermissionResolver>();

        // Layer 2: Permission stores (interfaces defined in EffectivePermissionResolverService)
        // These are adapters to existing stores
        services.AddScoped<ITenantPermissionStore>(sp =>
        {
            // Adapt existing TenantPermissionRepository
            var repo = sp.GetRequiredService<ITenantPermissionRepository>();
            return new TenantPermissionStoreAdapter(repo);
        });
        services.AddScoped<IResourcePermissionStore>(sp =>
        {
            // Adapt existing ResourcePermissionService
            var resourcePermissionService = sp.GetRequiredService<IResourcePermissionService>();
            return new ResourcePermissionStoreAdapter(resourcePermissionService);
        });

        // Layer 2: Unified Permission Resolver (ALLOW-WINS)
        services.AddScoped<IEffectivePermissionResolver, EffectivePermissionResolverService>();

        return services;
    }
}

/// <summary>
///     Adapter to bridge ITenantPermissionRepository to ITenantPermissionStore.
/// </summary>
internal class TenantPermissionStoreAdapter(ITenantPermissionRepository repository) : ITenantPermissionStore
{
    public async Task<TenantPermission?> GetPermissionAsync(Guid tenantId, CancellationToken ct = default)
        => await repository.GetByUserAndTenantAsync(null, tenantId, ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<TenantPermission>> GetAllPermissionsAsync(Guid tenantId, CancellationToken ct = default)
        => await repository.GetByTenantAsync(tenantId, ct).ConfigureAwait(false);
}

/// <summary>
///     Adapter to bridge IResourcePermissionService to IResourcePermissionStore.
/// </summary>
internal class ResourcePermissionStoreAdapter(IResourcePermissionService service) : IResourcePermissionStore
{
    public async Task<IReadOnlyList<ResourceUserPermission>> GetUserPermissionsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken ct = default)
        => await service.GetUserResourcesAsync(new CQRS.Models.TenantId(tenantId), userId, null, ct).ConfigureAwait(false);

    public Task<IReadOnlyList<ResourceUserPermission>> GetResourcePermissionsAsync(
        Guid resourceId,
        CancellationToken ct = default)
        // Note: IResourcePermissionService doesn't have a direct "by resource id" method
        // This adapter returns empty - implementations should use GetResourceUsersAsync instead
        => Task.FromResult<IReadOnlyList<ResourceUserPermission>>([]);
}
