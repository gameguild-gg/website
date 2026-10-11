using GameGuild.CQRS;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Payments module configuration for comprehensive payment processing system
///     Integrates payment services, repositories, and data access layers
/// </summary>
public static class PaymentsModule
{
    /// <summary>
    ///     Register all payments services including repositories and data context
    /// </summary>
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register CQRS handlers from this assembly
        services.AddCqrs(typeof(PaymentsModule).Assembly);

        // Register repositories (using shared ApplicationDbContext)
        services.AddScoped<IAuditTrailRepository, AuditTrailRepository>();
        services.AddScoped<IFinancialLedgerRepository, FinancialLedgerRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IRevenueEventRepository, RevenueEventRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();

        // Revenue auditing (issue #404): external statement reconciliation, anomaly
        // detection, compliance reporting and CSV/JSON exports.
        services.AddScoped<IRevenueReconciliationRepository, RevenueReconciliationRepository>();
        services.AddScoped<IRevenueAnomalyAlertRepository, RevenueReconciliationRepository>();
        services.AddScoped<IExternalRevenueStatementSource, ConfigurationExternalRevenueStatementSource>();
        services.AddScoped<IRevenueReconciliationService, RevenueReconciliationService>();
        services.AddScoped<IRevenueAnomalyService, RevenueAnomalyService>();
        services.AddScoped<IRevenueReportService, RevenueReportService>();
        services.AddSingleton<IValidateOptions<RevenueAuditingOptions>, RevenueAuditingOptionsValidator>();
        services.AddOptions<RevenueAuditingOptions>()
            .Bind(configuration.GetSection(RevenueAuditingOptions.SectionName))
            .ValidateOnStart();
        services.AddHostedService<RevenueAnomalyDetectionBackgroundService>();

        // Configurable retry schedule (issue #403): max retries + exponential backoff
        // parameters, previously hardcoded in the Payment entity.
        services.AddSingleton<IValidateOptions<PaymentRetryOptions>, PaymentRetryOptionsValidator>();
        services.AddOptions<PaymentRetryOptions>()
            .Bind(configuration.GetSection(PaymentRetryOptions.SectionName))
            .ValidateOnStart();
        // Fraud assessment gate (issue #409): tenant-scoped options, velocity signal
        // provider and the aggregate Allow/Review/Block detection service.
        services.AddSingleton<IValidateOptions<FraudAssessmentOptions>, FraudAssessmentOptionsValidator>();
        services.AddOptions<FraudAssessmentOptions>()
            .Bind(configuration.GetSection(FraudAssessmentOptions.SectionName))
            .ValidateOnStart();
        services.AddScoped<IFraudSignalProvider, VelocityFraudSignalProvider>();
        services.AddScoped<IFraudDetectionService, FraudDetectionService>();

        // Failure-code retry policy + automated retry queue (issue #415): classifies gateway
        // failure codes into retriable/non-retriable schedules, and (opt-in) polls due retries
        // hourly through the existing RetryPaymentCommand path. Default disabled, matching the
        // revenue-auditing worker: automatic charging must be a deliberate deployment decision.
        services.AddSingleton<IValidateOptions<PaymentRetryQueueOptions>, PaymentRetryQueueOptionsValidator>();
        services.AddOptions<PaymentRetryQueueOptions>()
            .Bind(configuration.GetSection(PaymentRetryQueueOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IPaymentRetryPolicyResolver, PaymentRetryPolicyResolver>();
        services.AddScoped<IPaymentRetryMethodChangeService, PaymentRetryMethodChangeService>();
        services.AddScoped<IPaymentRetryQueueWorker, PaymentRetryQueueWorker>();
        services.AddHostedService<PaymentRetryQueueBackgroundService>();

        // Register services
        services.AddScoped<IDisputeService, DisputeService>();
        services.AddScoped<IRevenueAuditService, RevenueAuditService>();
        services.AddScoped<ITaxCalculationService, TaxCalculationService>();
        services.AddScoped<IWalletService, WalletService>();
        services.Replace(ServiceDescriptor.Scoped<IOrderPaymentProcessor, OrderPaymentService>());
        services.Replace(ServiceDescriptor.Scoped<IOrderPaymentAuthority, OrderPaymentService>());
        services.Replace(ServiceDescriptor.Scoped<IOrderPaymentIntentPreparer, OrderPaymentIntentService>());

        // Payment-method registry (issue #414): self-service management plus the daily
        // expiry monitor (30/14/7-day notices through the Notifications module).
        services.AddScoped<IPaymentMethodService, PaymentMethodRegistryService>();
        services.AddScoped<IPaymentMethodExpiryMonitor, PaymentMethodExpiryMonitor>();
        services.AddOptions<PaymentMethodExpiryOptions>()
            .Bind(configuration.GetSection(PaymentMethodExpiryOptions.SectionName))
            .ValidateOnStart();
        services.AddHostedService<PaymentMethodExpiryMonitorBackgroundService>();

        // Register payment gateway and sub-services
        services.AddSingleton<IValidateOptions<StripeGatewayOptions>, StripeGatewayOptionsValidator>();
        services.AddOptions<StripeGatewayOptions>()
            .Bind(configuration.GetSection(StripeGatewayOptions.SectionName))
            .ValidateOnStart();
        services.AddScoped<IStripePaymentService, StripePaymentService>();
        services.AddScoped<IStripeCustomerService, StripeCustomerService>();
        services.AddScoped<IPaymentGateway, StripePaymentGateway>();
        // Register controllers
        services.AddControllers().AddApplicationPart(typeof(PaymentsModule).Assembly);

        return services;
    }

    /// <summary>
    ///     Configure the payments module in the application pipeline
    /// </summary>
    public static IApplicationBuilder UsePaymentsModule(this IApplicationBuilder app)
    {
        // Payments module doesn't require specific middleware configuration
        // Payment processing is handled through controllers and services
        return app;
    }
}
