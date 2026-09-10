using GameGuild.CQRS;
using GameGuild.Configuration;
using GameGuild.Finance.Contracts;
using GameGuild.Resources.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace GameGuild.Resources;

/// <summary>
///     Dependency injection configuration for Resources Infrastructure layer
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    ///     Registers all infrastructure services including DbContext and repositories
    /// </summary>
    public static IServiceCollection AddResourcesInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure Resources options using SharedKernel configuration utilities
        services.ConfigureOptions(configuration, () => new ResourcesOptions(), options => options.Validate());
        services.Configure<AwsCostAccountingOptions>(
            configuration.GetSection(AwsCostAccountingOptions.SectionName));

        // Register DbContext
        RegisterDbContext(services, configuration);

        // Register Repositories
        RegisterRepositories(services);

        // Register Application Services
        RegisterApplicationServices(services, configuration);

        return services;
    }

    /// <summary>
    ///     Registers all Resources application services including command/query handlers
    /// </summary>
    private static void RegisterApplicationServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register Quota Management Commands
        services.AddScoped<ICommandHandler<SetResourceQuotaCommand>, SetResourceQuotaCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteResourceQuotaCommand>, DeleteResourceQuotaCommandHandler>();
        services.AddScoped<ICommandHandler<ResetResourceQuotaCommand>, ResetResourceQuotaCommandHandler>();
        services.AddScoped<ICommandHandler<ToggleResourceQuotaCommand>, ToggleResourceQuotaCommandHandler>();

        // Register Usage Recording Commands
        services.AddScoped<ICommandHandler<RecordResourceUsageCommand, Guid>, RecordResourceUsageCommandHandler>();
        services.AddScoped<ICommandHandler<ResetResourceUsageCommand>, ResetResourceUsageCommandHandler>();

        // Register User-Level Quota Management Commands
        services.AddScoped<ICommandHandler<SetUserResourceQuotaCommand>, SetUserResourceQuotaCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteUserResourceQuotaCommand>, DeleteUserResourceQuotaCommandHandler>();
        services.AddScoped<ICommandHandler<ResetUserResourceQuotaCommand>, ResetUserResourceQuotaCommandHandler>();
        services.AddScoped<ICommandHandler<ToggleUserResourceQuotaCommand>, ToggleUserResourceQuotaCommandHandler>();

        // Register User-Level Usage Recording Commands
        services.AddScoped<ICommandHandler<RecordUserResourceUsageCommand, Guid>, RecordUserResourceUsageCommandHandler>();
        services.AddScoped<ICommandHandler<ResetUserResourceUsageCommand>, ResetUserResourceUsageCommandHandler>();

        // Register Quota Queries
        services.AddScoped<IQueryHandler<GetResourceQuotaQuery, ResourceQuotaResponse?>, GetResourceQuotaQueryHandler>();
        services.AddScoped<IQueryHandler<GetTenantResourceQuotasQuery, IEnumerable<ResourceQuotaResponse>>, GetTenantResourceQuotasQueryHandler>();
        services.AddScoped<IQueryHandler<CheckResourceQuotaQuery, ResourceQuotaEnforcementResult>, CheckResourceQuotaQueryHandler>();

        // Register Usage Queries
        services.AddScoped<IQueryHandler<GetResourceUsageRecordsQuery, PagedResult<UsageRecord>>, GetResourceUsageRecordsQueryHandler>();
        services.AddScoped<IQueryHandler<GetCurrentResourceUsageSummaryQuery, Dictionary<ResourceUsageType, int>>, GetCurrentResourceUsageSummaryQueryHandler>();
        services.AddScoped<IQueryHandler<GetResourceUsageByTypeQuery, Dictionary<Guid, int>>, GetResourceUsageByTypeQueryHandler>();

        // Register Limit Checking Queries
        services.AddScoped<IQueryHandler<CheckResourceUsageLimitsQuery, Dictionary<ResourceUsageType, bool>>, CheckResourceUsageLimitsQueryHandler>();

        // Register User-Level Quota Queries
        services.AddScoped<IQueryHandler<GetUserResourceQuotaQuery, ResourceQuotaResponse?>, GetUserResourceQuotaQueryHandler>();
        services.AddScoped<IQueryHandler<GetUserResourceQuotasQuery, IEnumerable<ResourceQuotaResponse>>, GetUserResourceQuotasQueryHandler>();
        services.AddScoped<IQueryHandler<CheckUserResourceQuotaQuery, ResourceQuotaEnforcementResult>, CheckUserResourceQuotaQueryHandler>();

        // Register User-Level Usage Queries
        services.AddScoped<IQueryHandler<GetUserResourceUsageRecordsQuery, IEnumerable<UsageRecord>>, GetUserResourceUsageRecordsQueryHandler>();
        services.AddScoped<IQueryHandler<GetCurrentUserResourceUsageSummaryQuery, Dictionary<ResourceUsageType, long>>, GetCurrentUserResourceUsageSummaryQueryHandler>();

        // Register User-Level Limit Checking Queries
        services.AddScoped<IQueryHandler<CheckUserResourceUsageLimitsQuery, Dictionary<ResourceUsageType, bool>>, CheckUserResourceUsageLimitsQueryHandler>();

        // Register focused sub-services
        services.AddScoped<IQuotaManagementService, QuotaManagementService>();
        services.AddScoped<IQuotaEnforcementService, QuotaEnforcementService>();
        services.AddScoped<IQuotaMaintenanceService, QuotaMaintenanceService>();

        // Register the thin facade (used by CachedResourceQuotaService decorator)
        services.AddScoped<ResourceQuotaService>();

        // Register the unified IResourceQuotaService with caching decorator
        services.AddScoped<IResourceQuotaService>(sp =>
            new CachedResourceQuotaService(
                sp.GetRequiredService<ResourceQuotaService>(),
                sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CachedResourceQuotaService>>(),
                sp.GetService<ICostTelemetryRecorder>()));

        // ISP: Register segregated interfaces pointing to the unified service
        services.AddScoped<IResourceQuotaReader>(sp => sp.GetRequiredService<IResourceQuotaService>());
        services.AddScoped<IResourceQuotaWriter>(sp => sp.GetRequiredService<IResourceQuotaService>());
        services.AddScoped<IResourceQuotaEnforcer>(sp => sp.GetRequiredService<IResourceQuotaService>());
        services.AddScoped<IResourceQuotaAnalytics>(sp => sp.GetRequiredService<IResourceQuotaService>());
        services.AddScoped<IResourceQuotaMaintenance>(sp => sp.GetRequiredService<IResourceQuotaService>());

        services.AddScoped<IUsageService, UsageService>();
        services.AddScoped<IResourceThrottlingService, ResourceThrottlingService>();
        if (configuration.GetValue<bool>("Redis:Enabled"))
        {
            services.AddScoped<IDistributedRateLimiter, RedisDistributedRateLimiter>();
        }
        else
        {
            services.AddScoped<IDistributedRateLimiter, DistributedCacheRateLimiter>();
        }

        services.AddScoped<IUsageRetentionService, UsageRetentionService>();
        services.AddScoped<IUsageTrendAnalysisService, UsageTrendAnalysisService>();
        services.AddScoped<ISlaImpactAnalysisService, SlaImpactAnalysisService>();
        services.AddScoped<ICostAllocationService, CostAllocationService>();
        services.AddScoped<ICloudCostIngestionService, CloudCostIngestionService>();
        services.AddScoped<IInternalCostLedgerReader, InternalCostLedgerReader>();
        services.AddScoped<InternalCostValuationService>();
        services.AddScoped<PendingCostValuationService>();
        services.AddSingleton<IAwsCur2CostSource, AwsCur2CsvCostSource>();
        if (configuration.GetValue<bool>($"{AwsCostAccountingOptions.SectionName}:Enabled"))
        {
            services.AddSingleton<Amazon.Pricing.IAmazonPricing>(
                _ => new Amazon.Pricing.AmazonPricingClient(Amazon.RegionEndpoint.USEast1));
            services.AddSingleton<Amazon.CostExplorer.IAmazonCostExplorer>(
                _ => new Amazon.CostExplorer.AmazonCostExplorerClient(Amazon.RegionEndpoint.USEast1));
            services.AddSingleton<IAwsPriceListRateSource, AwsPriceListRateSource>();
            services.AddSingleton<IAwsCostExplorerSource, AwsCostExplorerSource>();
            services.AddHostedService<AwsCostAccountingBackgroundService>();
        }

        services.AddScoped<QuotaLedgerEventHandler>();
        services.AddScoped<IIntegrationEventHandler<UserCreatedEvent>>(provider => provider.GetRequiredService<QuotaLedgerEventHandler>());
        services.AddScoped<IIntegrationEventHandler<UserDeletedEvent>>(provider => provider.GetRequiredService<QuotaLedgerEventHandler>());
        services.AddScoped<IIntegrationEventHandler<AssetReferenceCreatedEvent>>(provider => provider.GetRequiredService<QuotaLedgerEventHandler>());
        services.AddScoped<IIntegrationEventHandler<AssetReferenceRemovedEvent>>(provider => provider.GetRequiredService<QuotaLedgerEventHandler>());

        services.AddScoped<CostAccountingEventHandler>();
        services.AddScoped<IIntegrationEventHandler<UseCaseOperationOccurredV1>>(provider => provider.GetRequiredService<CostAccountingEventHandler>());
        services.AddScoped<IIntegrationEventHandler<UserCreatedEvent>>(provider => provider.GetRequiredService<CostAccountingEventHandler>());
        services.AddScoped<IIntegrationEventHandler<UserDeletedEvent>>(provider => provider.GetRequiredService<CostAccountingEventHandler>());
        services.AddScoped<IIntegrationEventHandler<AssetReferenceCreatedEvent>>(provider => provider.GetRequiredService<CostAccountingEventHandler>());
        services.AddScoped<IIntegrationEventHandler<AssetReferenceRemovedEvent>>(provider => provider.GetRequiredService<CostAccountingEventHandler>());
        services.AddScoped<IIntegrationEventHandler<AssetObjectStoredEvent>>(provider => provider.GetRequiredService<CostAccountingEventHandler>());
        services.AddScoped<IIntegrationEventHandler<AssetObjectDeletedEvent>>(provider => provider.GetRequiredService<CostAccountingEventHandler>());
        services.AddScoped<IIntegrationEventHandler<AssetTransformedEvent>>(provider => provider.GetRequiredService<CostAccountingEventHandler>());
        services.AddScoped<IIntegrationEventHandler<AssetServedEvent>>(provider => provider.GetRequiredService<CostAccountingEventHandler>());
        services.AddScoped<IIntegrationEventHandler<EconomyPostingAcceptedEventV1>>(provider => provider.GetRequiredService<CostAccountingEventHandler>());
        services.AddScoped<IIntegrationEventHandler<ApiRequestMeasuredEventV1>>(provider => provider.GetRequiredService<CostAccountingEventHandler>());

        // SLA Incident Escalation Services
        // SlaIncidentEscalationService depends on ISlaImpactAnalysisRepository + IIncidentTicketProvider
        // directly, avoiding the former circular dependency with ISlaImpactAnalysisService.
        services.AddScoped<ISlaNotificationSender, LoggingSlaNotificationSender>();
        services.AddScoped<ISlaIncidentEscalationService, SlaIncidentEscalationService>();
        services.AddScoped<IIncidentTicketProvider, DefaultIncidentTicketProvider>();

        // Event Handlers for Alerts and Observability
        services.AddScoped<INotificationHandler<QuotaExceededEvent>, QuotaExceededAlertHandler>();
    }

    /// <summary>
    ///     Register database-related services (the ApplicationDbContext is registered by the main API)
    ///     This method is kept for compatibility but no longer registers its own DbContext
    /// </summary>
    // ReSharper disable once UnusedParameter.Local - Parameters kept for signature compatibility
    private static void RegisterDbContext(IServiceCollection _, IConfiguration _2)
    {
        // NOTE: The Resources module now uses the shared ApplicationDbContext from GameGuild.API
        // This context is registered in the main API's DependencyInjection.AddInfrastructureData method
        // No need to register a separate ResourcesDbContext

        // The IApplicationDbContext service is already registered by the main API
        // and includes all Resources entities (ResourceQuota, UsageRecord, etc.)
    }

    /// <summary>
    ///     Register all repository implementations
    /// </summary>
    private static void RegisterRepositories(IServiceCollection services)
    {
        // Core Resources Repositories
        services.AddScoped<IResourceQuotaRepository, ResourceQuotaRepository>();
        services.AddScoped<IUsageRecordRepository, UsageRecordRepository>();
        services.AddScoped<ICostAllocationReportRepository, CostAllocationReportRepository>();
        services.AddScoped<IResourceThrottlingPolicyRepository, ResourceThrottlingPolicyRepository>();
        services.AddScoped<ISlaImpactAnalysisRepository, SlaImpactAnalysisRepository>();
        services.AddScoped<IUsageRetentionPolicyRepository, UsageRetentionPolicyRepository>();
        services.AddScoped<IResourceUsageTrendRepository, ResourceUsageTrendRepository>();

        // Metadata and Settings Repositories
        services.AddScoped<IResourceMetadataRepository, ResourceMetadataRepository>();
        services.AddScoped<IResourceSettingsRepository, ResourceSettingsRepository>();
    }
}
