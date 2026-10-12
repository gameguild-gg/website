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
        services.AddScoped<IRefundRepository, RefundRepository>();
        services.AddScoped<IRevenueEventRepository, RevenueEventRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();

        // Refund approval workflow (issue #419): configurable manual-approval threshold,
        // approver notifications and the shared refund execution core.
        services.AddSingleton<IValidateOptions<RefundApprovalOptions>, RefundApprovalOptionsValidator>();
        services.AddOptions<RefundApprovalOptions>()
            .Bind(configuration.GetSection(RefundApprovalOptions.SectionName))
            .ValidateOnStart();
        services.AddScoped<IRefundProcessingService, RefundProcessingService>();

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

        // Multi-gateway processing (issue #413): config-driven fail-closed routing,
        // failover policy with per-gateway circuit breaker, and per-gateway metrics.
        services.AddSingleton<IValidateOptions<GatewayRoutingOptions>, GatewayRoutingOptionsValidator>();
        services.AddOptions<GatewayRoutingOptions>()
            .Bind(configuration.GetSection(GatewayRoutingOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<GatewayFailoverOptions>, GatewayFailoverOptionsValidator>();
        services.AddOptions<GatewayFailoverOptions>()
            .Bind(configuration.GetSection(GatewayFailoverOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<PayPalGatewayOptions>, PayPalGatewayOptionsValidator>();
        services.AddOptions<PayPalGatewayOptions>()
            .Bind(configuration.GetSection(PayPalGatewayOptions.SectionName))
            .ValidateOnStart();
        services.AddHttpClient<PayPalApiClient>();
        services.AddScoped<IPayPalApiClient>(provider => provider.GetRequiredService<PayPalApiClient>());

        // Register services
        services.AddScoped<IDisputeService, DisputeService>();
        services.AddScoped<IRevenueAuditService, RevenueAuditService>();
        services.AddScoped<ITaxCalculationService, TaxCalculationService>();

        // Unified pricing engine (issue #417): the single calculation path for base plan,
        // add-ons, usage tiers, discounts, proration, tax, credits and currency. Its
        // cross-module seams (proration, promo resolution) are Payments-owned abstractions
        // implemented by the Subscriptions and Products modules respectively.
        services.AddScoped<IPricingEngine, PricingEngine>();
        services.AddCurrencyConversion();
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

        // Multi-gateway processing (issue #413): both adapters are registered explicitly;
        // the registry is the routing surface, the selector orders/filters it, the circuit
        // breaker tracks per-gateway health, and the failover composite is the single
        // IPaymentGateway resolved by the payment path.
        services.AddScoped<StripePaymentGateway>();
        services.AddScoped<PayPalPaymentGateway>();
        services.AddScoped<IGatewayRegistry, GatewayRegistry>();
        services.AddScoped<IPaymentGatewaySelector, ConfigPaymentGatewaySelector>();
        services.AddSingleton<IGatewayCircuitBreaker>(provider =>
            new InMemoryGatewayCircuitBreaker(
                provider.GetRequiredService<IOptions<GatewayFailoverOptions>>().Value));
        services.AddSingleton<IGatewayAvailabilityAlerter, NotificationGatewayAvailabilityAlerter>();
        services.AddScoped<IGatewayMetricsService, GatewayMetricsService>();
        services.AddScoped<IPaymentGateway, FailoverPaymentGateway>();

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
