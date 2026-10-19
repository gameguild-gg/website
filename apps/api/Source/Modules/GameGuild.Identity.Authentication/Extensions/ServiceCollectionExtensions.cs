using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Service collection extensions for Authentication Presentation layer
///     Provides comprehensive registration of all authentication and authorization services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add Authentication Presentation layer services
    /// </summary>
    public static IServiceCollection AddAuthenticationPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure authentication options
        services.Configure<AuthenticationModuleOptions>(configuration.GetSection(AuthenticationModuleOptions.SectionName));

        // Register presentation services
        RegisterPresentationServices(services);

        // Register authorization services
        RegisterAuthorizationServices(services);

        // Register policy evaluation services
        RegisterPolicyEvaluationServices(services);

        // Register access review services
        RegisterAccessReviewServices(services);

        // Register caching services
        RegisterCachingServices(services);

        // Register audit services
        RegisterAuditServices(services);

        return services;
    }

    /// <summary>
    ///     Register core presentation services
    /// </summary>
    private static void RegisterPresentationServices(IServiceCollection services)
    {
        // Register API controllers
        services.AddControllers()
            .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.WriteIndented = true;
                }
            );

        // PLANNED: Register presentation services when implementations exist.
        // Requires: ModelValidationService, ResponseFormattingService, ErrorHandlingService classes.
        // Register model validation
        // services.AddScoped<IModelValidationService, ModelValidationService>();

        // Register response formatting
        // services.AddScoped<IResponseFormattingService, ResponseFormattingService>();

        // Register error handling
        // services.AddScoped<IErrorHandlingService, ErrorHandlingService>();
    }

    /// <summary>Register the compatible and typed permission contracts as aliases of one scoped facade.</summary>
    public static IServiceCollection AddPermissionServiceContracts(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<PermissionService>();
        services.AddScoped<IPermissionService>(provider => provider.GetRequiredService<PermissionService>());
        services.AddScoped<IPermissionServiceV2>(provider => provider.GetRequiredService<PermissionService>());
        services.AddScoped<IPermissionReadService>(provider => provider.GetRequiredService<PermissionService>());
        services.AddScoped<IPermissionUpdateService>(provider => provider.GetRequiredService<PermissionService>());
        services.AddScoped<IPermissionLifecycleService>(provider => provider.GetRequiredService<PermissionService>());
        return services;
    }

    /// <summary>
    ///     Register authorization services
    /// </summary>
    private static void RegisterAuthorizationServices(IServiceCollection services)
    {
        services.AddPermissionServiceContracts();

        // Content-type hierarchy for effective-permission resolution (issue #314):
        // implements the Authorization module's IContentTypeHierarchyProvider contract
        // over the registered ContentTypePermission rows (parent links + registration).
        services.AddScoped<GameGuild.Identity.Authorization.IContentTypeHierarchyProvider, ContentTypePermissionHierarchyProvider>();

        // PLANNED: Register advanced authorization presentation services once implementations are created.
        // Requires: PermissionAuthorizationService, PermissionHierarchyService, PermissionTemplateService,
        //           BulkPermissionService, BulkOperationResultService, PermissionAnalyticsService, PermissionAuditService.
        // services.AddScoped<IPermissionAuthorizationService, PermissionAuthorizationService>();
        // services.AddScoped<IPermissionHierarchyService, PermissionHierarchyService>();
        // services.AddScoped<IPermissionTemplateService, PermissionTemplateService>();

        // Register bulk operation services
        // services.AddScoped<IBulkPermissionService, BulkPermissionService>();
        // services.AddScoped<IBulkOperationResultService, BulkOperationResultService>();

        // Register permission analytics
        // services.AddScoped<IPermissionAnalyticsService, PermissionAnalyticsService>();
        // services.AddScoped<IPermissionAuditService, PermissionAuditService>();
    }

    /// <summary>
    ///     Register policy evaluation services
    /// </summary>
    private static void RegisterPolicyEvaluationServices(IServiceCollection services)
    {
        _ = services; // PLANNED: Register ABAC and conditional policy services once implementations are created.
        // Requires: AbacPolicyEvaluationService, AbacExpressionValidationService, ConditionalPolicyEvaluationService,
        //           PolicyAnalyticsService, PolicyUsageTrackingService.
        // services.AddScoped<IAbacPolicyEvaluationService, AbacPolicyEvaluationService>();
        // services.AddScoped<IAbacExpressionValidationService, AbacExpressionValidationService>();
        // services.AddScoped<IAbacPolicyConflictDetectionService, AbacPolicyConflictDetectionService>();
        // services.AddScoped<IAbacPolicyTemplateService, AbacPolicyTemplateService>();

        // Register conditional policy services
        // services.AddScoped<IConditionalPolicyEvaluationService, ConditionalPolicyEvaluationService>();
        // services.AddScoped<IConditionalPolicyValidationService, ConditionalPolicyValidationService>();
        // services.AddScoped<IConditionalPolicySimulationService, ConditionalPolicySimulationService>();
        // services.AddScoped<IConditionalPolicyTemplateService, ConditionalPolicyTemplateService>();

        // Register policy analytics
        // services.AddScoped<IPolicyAnalyticsService, PolicyAnalyticsService>();
        // services.AddScoped<IPolicyUsageTrackingService, PolicyUsageTrackingService>();
    }

    /// <summary>
    ///     Register access review services
    /// </summary>
    private static void RegisterAccessReviewServices(IServiceCollection services)
    {
        _ = services; // PLANNED: Register access review and compliance services once implementations are created.
        // Requires: AccessReviewOrchestrationService, AccessReviewCampaignService, PeriodicAccessReviewService,
        //           AccessRevocationService, ComplianceReportingService.
        // services.AddScoped<IAccessReviewOrchestrationService, AccessReviewOrchestrationService>();
        // services.AddScoped<IAccessReviewCampaignService, AccessReviewCampaignService>();
        // services.AddScoped<IAccessReviewItemService, AccessReviewItemService>();

        // Register periodic reviews
        // services.AddScoped<IPeriodicAccessReviewService, PeriodicAccessReviewService>();
        // services.AddScoped<IAccessReviewSchedulingService, AccessReviewSchedulingService>();

        // Register access revocation
        // services.AddScoped<IAccessRevocationService, AccessRevocationService>();
        // services.AddScoped<IBulkAccessRevocationService, BulkAccessRevocationService>();

        // Register compliance and reporting
        // services.AddScoped<IComplianceReportingService, ComplianceReportingService>();
        // services.AddScoped<IAccessReviewAnalyticsService, AccessReviewAnalyticsService>();
        // services.AddScoped<IAccessReviewTemplateService, AccessReviewTemplateService>();

        // Register notification services
        // services.AddScoped<IReviewReminderService, ReviewReminderService>();
        // services.AddScoped<IAccessReviewNotificationService, AccessReviewNotificationService>();
    }

    /// <summary>
    ///     Register caching services
    /// </summary>
    private static void RegisterCachingServices(IServiceCollection services)
    {
        _ = services; // PLANNED: Register permission and policy caching services once implementations are created.
        // Requires: PermissionCacheService, PolicyCacheService, CacheInvalidationService, CacheWarmupService.
        // services.AddScoped<IPermissionCacheService, PermissionCacheService>();
        // services.AddScoped<IPermissionCacheStatsService, PermissionCacheStatsService>();

        // Register policy caching
        // services.AddScoped<IPolicyCacheService, PolicyCacheService>();
        // services.AddScoped<IPolicyEvaluationCacheService, PolicyEvaluationCacheService>();

        // Register cache invalidation
        // services.AddScoped<ICacheInvalidationService, CacheInvalidationService>();
        // services.AddScoped<ICacheWarmupService, CacheWarmupService>();
    }

    /// <summary>
    ///     Register audit services
    /// </summary>
    private static void RegisterAuditServices(IServiceCollection services)
    {
        _ = services; // PLANNED: Register audit and compliance monitoring services once implementations are created.
        // Requires: PermissionAuditLogger, PolicyAuditLogger, AuditTrailService, ComplianceMonitoringService.
        // services.AddScoped<IPermissionAuditLogger, PermissionAuditLogger>();
        // services.AddScoped<IPolicyAuditLogger, PolicyAuditLogger>();
        // services.AddScoped<IAccessReviewAuditLogger, AccessReviewAuditLogger>();

        // Register audit trail services
        // services.AddScoped<IAuditTrailService, AuditTrailService>();
        // services.AddScoped<IAuditReportingService, AuditReportingService>();

        // Register compliance monitoring
        // services.AddScoped<IComplianceMonitoringService, ComplianceMonitoringService>();
        // services.AddScoped<ISecurityEventService, SecurityEventService>();
    }

    /// <summary>
    ///     Configure health checks for authentication services
    /// </summary>
    public static IServiceCollection AddAuthenticationHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        // PLANNED: Add health checks for authentication sub-services when they exist.
        services.AddHealthChecks();
        // .AddCheck<PermissionServiceHealthCheck>("permission-service")
        // .AddCheck<AbacPolicyServiceHealthCheck>("abac-policy-service")
        // .AddCheck<ConditionalPolicyServiceHealthCheck>("conditional-policy-service")
        // .AddCheck<AccessReviewServiceHealthCheck>("access-review-service")
        // .AddCheck<PermissionCacheHealthCheck>("permission-cache");

        return services;
    }

    /// <summary>
    ///     Configure metrics collection for authentication services
    /// </summary>
    public static IServiceCollection AddAuthenticationMetrics(this IServiceCollection services)
    {
        // PLANNED: Register metrics collectors for permission, policy evaluation, access review, and cache performance.
        // Requires: PermissionMetricsCollector, PolicyEvaluationMetricsCollector, etc.
        // services.AddScoped<IPermissionMetricsCollector, PermissionMetricsCollector>();
        // services.AddScoped<IPolicyEvaluationMetricsCollector, PolicyEvaluationMetricsCollector>();
        // services.AddScoped<IAccessReviewMetricsCollector, AccessReviewMetricsCollector>();
        // services.AddScoped<ICachePerformanceMetricsCollector, CachePerformanceMetricsCollector>();

        // Register performance monitoring
        // services.AddScoped<IPerformanceMonitoringService, PerformanceMonitoringService>();
        // services.AddScoped<IMetricsAggregationService, MetricsAggregationService>();

        return services;
    }
}
