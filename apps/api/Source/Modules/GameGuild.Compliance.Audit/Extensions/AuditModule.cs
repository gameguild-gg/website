using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Provisioning;

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
        services.AddScoped<IAuditReplayCaptureService, AuditReplayCaptureService>();
        services.AddScoped<IAuditReplayReconstructionService, AuditReplayReconstructionService>();
        services.TryAddSingleton(TimeProvider.System);

        // PII redaction & tokenization pipeline (issue #192): rule cache, deterministic HMAC tokenizer,
        // the pipeline itself, its administration surface, and the save interceptor applied at the
        // central audit sink.
        services.AddSingleton<IPiiRedactionRuleProvider, PiiRedactionRuleProvider>();
        services.AddSingleton<IPiiTokenSource, PiiHmacTokenizer>();
        services.AddScoped<IPiiRedactionService, PiiRedactionService>();
        services.AddScoped<IPiiRedactionAdminService, PiiRedactionAdminService>();
        services.AddScoped<AuditLogPiiRedactionInterceptor>();
        services.AddHostedService<PiiRedactionRuleRefreshService>();

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
        services.AddScoped<IScimProvisioningAuditSink, CentralScimProvisioningAuditEventSink>();
        // Central bridges for the module audit sinks (issues #180/#181/#184): content pages,
        // learning, and teams mutations are required audit events recorded transactionally.
        // The learning-courses entity interceptor lives in the host composition root because
        // referencing Learning.Courses from here would close a dependency cycle.
        services.AddScoped<GameGuild.Content.Pages.IContentPagesAuditEventSink, CentralContentPagesAuditEventSink>();
        services.AddScoped<GameGuild.Learning.ILearningAuditEventSink, CentralLearningAuditEventSink>();
        services.AddScoped<GameGuild.Teams.ITeamsAuditEventSink, CentralTeamsAuditEventSink>();
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

        // Audit query DSL: typed filter model, fail-closed parser, bounded executor, saved views.
        services.AddOptions<AuditQueryDslOptions>()
            .Configure<IConfiguration>((dslOptions, configuration) =>
            {
                var section = configuration.GetSection(AuditQueryDslOptions.ConfigurationSection);
                if (int.TryParse(section["MaxPageSize"], out var maxPageSize))
                {
                    dslOptions.MaxPageSize = maxPageSize;
                }

                if (int.TryParse(section["MaxRowsScanned"], out var maxRowsScanned))
                {
                    dslOptions.MaxRowsScanned = maxRowsScanned;
                }

                if (int.TryParse(section["DefaultLookbackDays"], out var defaultLookbackDays))
                {
                    dslOptions.DefaultLookbackDays = defaultLookbackDays;
                }

                if (int.TryParse(section["DefaultTimeoutSeconds"], out var defaultTimeoutSeconds))
                {
                    dslOptions.DefaultTimeoutSeconds = defaultTimeoutSeconds;
                }

                if (int.TryParse(section["MaxTimeoutSeconds"], out var maxTimeoutSeconds))
                {
                    dslOptions.MaxTimeoutSeconds = maxTimeoutSeconds;
                }
            })
            .Validate(
                dslOptions => dslOptions.MaxPageSize > 0
                              && dslOptions.MaxRowsScanned > 0
                              && dslOptions.DefaultLookbackDays > 0
                              && dslOptions.DefaultTimeoutSeconds > 0
                              && dslOptions.MaxTimeoutSeconds >= dslOptions.DefaultTimeoutSeconds,
                $"{AuditQueryDslOptions.ConfigurationSection} bounds must be positive and MaxTimeoutSeconds must cover DefaultTimeoutSeconds.");
        services.AddSingleton<IAuditQueryDslParser, AuditQueryDslParser>();
        services.AddScoped<IAuditQueryDslExecutor, AuditQueryDslExecutor>();
        services.AddScoped<ISavedAuditQueryRepository, SavedAuditQueryRepository>();
        services.AddScoped<ISavedAuditQueryService, SavedAuditQueryService>();

        return services;
    }
}
