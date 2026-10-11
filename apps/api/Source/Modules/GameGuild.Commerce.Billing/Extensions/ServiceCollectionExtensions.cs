using GameGuild.CQRS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Extension methods for registering Billing module services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add Billing module services to the service collection
    /// </summary>
    public static IServiceCollection AddBillingModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register CQRS handlers from this assembly
        services.AddCqrs(typeof(ServiceCollectionExtensions).Assembly);

        // Short-TTL query cache backing the webhook security summary handler and its
        // eviction hook in WebhookSecurityEventPublisher (issue #394).
        AddBillingQueryCaching(services);

        // Register configuration
        services.AddSingleton<IValidateOptions<BillingConfiguration>, BillingConfigurationProductionValidator>();
        services.AddOptions<BillingConfiguration>()
            .Bind(configuration.GetSection(BillingConfiguration.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.Configure<PayPalSettings>(configuration.GetSection($"{BillingConfiguration.SectionName}:PayPal"));
        services.Configure<ApplePaySettings>(configuration.GetSection($"{BillingConfiguration.SectionName}:ApplePay"));

        // Register repositories
        services.AddScoped<IBillingWebhookRepository, BillingWebhookRepository>();

        // Invoice materialization (issue #411): the production writer behind both payment-success
        // paths (first-party charges via the Subscriptions port, provider-billed cycles via the
        // Stripe invoice.payment_succeeded webhook) plus the invoice-issued email renderer.
        services.AddScoped<IInvoiceGenerationService, InvoiceGenerationService>();
        services.AddScoped<GameGuild.Commerce.Subscriptions.ISubscriptionInvoiceMaterializer, SubscriptionInvoiceMaterializer>();
        services.AddScoped<GameGuild.Notifications.Services.Email.IEmailRenderer, Services.Email.Renderers.InvoiceIssuedEmailRenderer>();

        // Named billing integration events (issue #396): publish through the durable transport.
        services.AddScoped<IBillingIntegrationEventPublisher, BillingIntegrationEventPublisher>();

        // Hosted asynchronous retry of failed webhook inbox events (issue #396).
        services.AddScoped<IBillingWebhookRetryWorker, BillingWebhookRetryWorker>();
        services.AddHostedService<BillingWebhookRetryBackgroundService>();

        // Register Apple sub-services
        services.AddSingleton<IAppleJwsVerificationService, AppleJwsVerificationService>();
        services.AddSingleton<IAppleStoreAuthService, AppleStoreAuthService>();

        // Register verification services
        services.AddHttpClient<IPayPalSignatureVerificationService, PayPalSignatureVerificationService>();
        services.AddHttpClient<IApplePayReceiptValidationService, ApplePayReceiptValidationService>();
        services.AddSingleton<IStripeWebhookVerifier, StripeWebhookVerifier>();
        services.AddScoped<IStripeProviderObjectBindingValidator, StripeProviderObjectBindingValidator>();

        // Register webhook services for each payment provider
        services.AddScoped<IBillingWebhookService, StripeBillingWebhookService>();
        services.AddScoped<StripeBillingWebhookService>();
        services.AddScoped<PayPalBillingWebhookService>();
        services.AddScoped<ApplePayBillingWebhookService>();
        services.AddSingleton<IGooglePayWebhookVerificationService, GooglePayWebhookVerificationService>();
        services.AddScoped<GooglePayBillingWebhookService>();

        // Register webhook source security controls (IP allowlist, threshold monitor,
        // security-event publishing, and the authorization filter that enforces them).
        services.AddBillingWebhookSourceSecurity();

        return services;
    }

    /// <summary>
    ///     Register the webhook source security controls shared by the provider callback
    ///     endpoints: CIDR allowlist, suspicious-activity auto-blocking, and security-event
    ///     publishing into the Compliance.Audit security event pipeline.
    /// </summary>
    public static IServiceCollection AddBillingWebhookSourceSecurity(this IServiceCollection services)
    {
        // The publisher registered here evicts the cached webhook security summary, so a
        // cache service must be resolvable even when only the security controls are wired.
        AddBillingQueryCaching(services);

        services.AddSingleton<WebhookSourceIpAllowlist>();
        services.AddSingleton<IWebhookSuspiciousActivityMonitor, WebhookSuspiciousActivityMonitor>();
        services.AddScoped<IWebhookSecurityEventPublisher, WebhookSecurityEventPublisher>();
        services.AddScoped<WebhookSourceSecurityFilter>();
        return services;
    }

    /// <summary>
    ///     Ensures an <see cref="ICacheService"/> is resolvable for the short-TTL webhook
    ///     security summary query cache (issue #394). The API host registers its own
    ///     implementation first via SetupMemoryCaching (Redis when enabled, in-process
    ///     otherwise) and wins; standalone hosts and tests fall back to the SharedKernel
    ///     in-process implementation. Idempotent (TryAdd + AddMemoryCache are both no-ops
    ///     when the host already configured the cache).
    /// </summary>
    private static void AddBillingQueryCaching(IServiceCollection services)
    {
        services.AddMemoryCache();
        services.TryAddSingleton<ICacheService, GameGuild.CQRS.Implementation.MemoryCacheService>();
    }

    /// <summary>
    ///     Add Billing webhook processing services
    /// </summary>
    public static IServiceCollection AddBillingWebhooks(this IServiceCollection services)
    {
        services.AddOptions<BillingConfiguration>();

        // Register webhook-specific services
        services.AddScoped<IBillingWebhookRepository, BillingWebhookRepository>();
        services.AddSingleton<IStripeWebhookVerifier, StripeWebhookVerifier>();
        services.AddScoped<IStripeProviderObjectBindingValidator, StripeProviderObjectBindingValidator>();
        
        // Register all webhook service implementations
        services.AddScoped<IBillingWebhookService, StripeBillingWebhookService>();
        services.AddScoped<StripeBillingWebhookService>();
        services.AddScoped<PayPalBillingWebhookService>();
        services.AddScoped<ApplePayBillingWebhookService>();
        services.AddSingleton<IGooglePayWebhookVerificationService, GooglePayWebhookVerificationService>();
        services.AddScoped<GooglePayBillingWebhookService>();

        // Register webhook source security controls (IP allowlist, threshold monitor,
        // security-event publishing, and the authorization filter that enforces them).
        services.AddBillingWebhookSourceSecurity();

        return services;
    }
}
