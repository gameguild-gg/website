using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using GameGuild.Identity.Authentication;

namespace GameGuild.Compliance.Audit;

/// <summary>
/// Extension methods for registering Audit module services
/// </summary>
public static class AuditModule
{
    /// <summary>
    /// Registers all Audit module services
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAuditServices(this IServiceCollection services)
        => AddAuditServicesCore(services, null);

    /// <summary>
    /// Registers all Audit module services and configures signing keys.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureSigningOptions">Signing-key configuration.</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAuditServices(
        this IServiceCollection services,
        Action<AuditSigningOptions> configureSigningOptions)
    {
        ArgumentNullException.ThrowIfNull(configureSigningOptions);
        return AddAuditServicesCore(services, configureSigningOptions);
    }

    private static IServiceCollection AddAuditServicesCore(
        IServiceCollection services,
        Action<AuditSigningOptions>? configureSigningOptions)
    {
        // Fail fast when the pre-built retention policy templates are internally inconsistent
        // or cannot produce a valid configuration request.
        AuditRetentionPolicyTemplates.ValidateCatalog();

        services.AddOptions<AuditExportWebhookOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                var configuredOptions = AuditExportWebhookOptionsConfiguration.BindFrom(configuration);
                options.SigningSecret = configuredOptions.SigningSecret;
                options.AllowedHosts = configuredOptions.AllowedHosts;
                options.MaxAttempts = configuredOptions.MaxAttempts;
                options.RetryDelayMilliseconds = configuredOptions.RetryDelayMilliseconds;
                options.TimeoutSeconds = configuredOptions.TimeoutSeconds;
            });
        services.AddOptions<AuditScheduledExportOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                var section = configuration.GetSection(AuditScheduledExportOptions.ConfigurationSection);
                if (int.TryParse(section[AuditScheduledExportOptions.StaleClaimThresholdMinutesKey], out var staleMinutes))
                {
                    options.StaleClaimThreshold = TimeSpan.FromMinutes(staleMinutes);
                }
            })
            .Validate(
                options => options.StaleClaimThreshold > TimeSpan.Zero,
                $"{AuditScheduledExportOptions.ConfigurationSection}:{AuditScheduledExportOptions.StaleClaimThresholdMinutesKey} " +
                "must be a positive number of minutes; a zero or negative threshold would mark every in-progress claim stale.");
        services.AddHttpClient<IAuditExportWebhookNotifier, AuditExportWebhookNotifier>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        services.AddOptions<AuditSigningOptions>();
        if (configureSigningOptions is not null)
        {
            services.Configure(configureSigningOptions);
        }

        services.AddOptions<SecurityEventPipelineOptions>()
            .Configure<IConfiguration>((pipelineOptions, configuration) =>
            {
                var bound = SecurityEventPipelineOptionsConfiguration.BindFrom(configuration);
                pipelineOptions.DatabaseWriteAttempts = bound.DatabaseWriteAttempts;
                pipelineOptions.RetryDelayMilliseconds = bound.RetryDelayMilliseconds;
                pipelineOptions.SpoolingEnabled = bound.SpoolingEnabled;
                pipelineOptions.SpoolDirectoryPath = bound.SpoolDirectoryPath;
                pipelineOptions.DrainInterval = bound.DrainInterval;
                pipelineOptions.FailedAuthenticationAlertThreshold = bound.FailedAuthenticationAlertThreshold;
                pipelineOptions.FailedAuthenticationWindowMinutes = bound.FailedAuthenticationWindowMinutes;
                pipelineOptions.RetentionEnforcementInterval = bound.RetentionEnforcementInterval;
                pipelineOptions.DefaultRetentionDays = bound.DefaultRetentionDays;
                pipelineOptions.RetentionBatchSize = bound.RetentionBatchSize;
            });

        // Register audit services
        services.AddScoped<IAuditService, AuditService>();
        services.TryAddSingleton(TimeProvider.System);

        // Durable security event pipeline: taxonomy-driven capture, alerting, and retention enforcement.
        services.AddSingleton<ISecurityEventSpool>(provider =>
        {
            var pipelineOptions = provider.GetRequiredService<IOptions<SecurityEventPipelineOptions>>().Value;
            return new SecurityEventFileSpool(pipelineOptions);
        });
        services.AddSingleton<ISecurityEventPipelineStatusTracker, SecurityEventPipelineStatusTracker>();
        services.AddScoped<ISecurityAlertRuleEvaluator, SecurityAlertRuleEvaluator>();
        services.AddScoped<ISecurityEventLogger, SecurityEventLogger>();
        services.AddHostedService<SecurityEventSpoolDrainerBackgroundService>();
        services.AddScoped<ISecurityEventQueryService, SecurityEventQueryService>();
        services.AddScoped<ISecurityLogRetentionRepository, SecurityLogRetentionRepository>();
        services.AddScoped<ISecurityLogRetentionService, SecurityLogRetentionService>();
        services.AddHostedService<SecurityLogRetentionBackgroundService>();

        services.AddSingleton<AuditRetentionSimulationEngine>();
        services.AddScoped<IAuditRetentionSimulationRepository, AuditRetentionSimulationRepository>();
        services.AddScoped<IAuditRetentionSimulationService, AuditRetentionSimulationService>();
        services.AddScoped<IAuditDataAccessRecorder, AuditDataAccessRecorder>();
        services.AddScoped<IAuthenticationAuditEventSink, CentralAuthenticationAuditEventSink>();
        services.AddScoped<IApiKeyAuditEventSink, CentralApiKeyAuditEventSink>();
        services.AddScoped<IAuditActionTypeSearchService, AuditActionTypeSearchService>();
        services.AddSingleton<IAuditExportProgressTracker, DistributedAuditExportProgressTracker>();
        services.AddScoped<IAuditExportCronSchedule, AuditExportCronSchedule>();
        services.AddScoped<IScheduledAuditExportRepository, ScheduledAuditExportRepository>();
        services.AddScoped<IScheduledAuditExportService, ScheduledAuditExportService>();
        services.AddHostedService<ScheduledAuditExportBackgroundService>();
        services.AddScoped<ITamperEvidentAuditService, TamperEvidentAuditService>();
        services.AddSingleton<ICryptographicSigningService, EcdsaCryptographicSigningService>();
        services.AddSingleton<ComplianceEvidenceValidationEngine>();
        services.AddSingleton<ComplianceArtifactBuilder>();
        services.AddSingleton<IComplianceFrameworkCatalog, ComplianceFrameworkCatalog>();
        services.AddScoped<ICompliancePackagingRepository, CompliancePackagingRepository>();
        services.AddScoped<IComplianceEvidencePackagingService, ComplianceEvidencePackagingService>();
        services.AddSingleton<AuditChainEvidenceVerifier>();

        // Register security audit sub-services
        services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
        services.AddScoped<IAuditReportService, AuditReportService>();

        // Register security audit aggregator facade for backward compatibility
        services.AddScoped<ISecurityAuditAggregator, SecurityAuditAggregator>();

        return services;
    }
}
