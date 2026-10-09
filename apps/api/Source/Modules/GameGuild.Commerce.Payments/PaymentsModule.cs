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

        // Register services
        services.AddScoped<IDisputeService, DisputeService>();
        services.AddScoped<IRevenueAuditService, RevenueAuditService>();
        services.AddScoped<ITaxCalculationService, TaxCalculationService>();
        services.AddScoped<IWalletService, WalletService>();
        services.Replace(ServiceDescriptor.Scoped<IOrderPaymentProcessor, OrderPaymentService>());
        services.Replace(ServiceDescriptor.Scoped<IOrderPaymentAuthority, OrderPaymentService>());
        services.Replace(ServiceDescriptor.Scoped<IOrderPaymentIntentPreparer, OrderPaymentIntentService>());

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
