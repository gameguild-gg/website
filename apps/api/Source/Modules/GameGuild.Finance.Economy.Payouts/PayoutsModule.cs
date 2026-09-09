using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.Finance.Economy.Payouts;

public sealed class PayoutsModule : ModuleBase
{
    public override string Name => "Finance.Economy.Payouts";
    public override bool EnabledByDefault => true;

    public override IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPayoutOperationStore, PostgreSqlPayoutOperationStore>();
        services.AddScoped<IPayoutRequestStore, PostgreSqlPayoutRequestStore>();
        services.AddScoped<IPayoutFencingTokenAllocator, PostgreSqlPayoutFencingTokenAllocator>();
        services.AddOptions<StripeConnectPayoutOptions>()
            .Bind(configuration.GetSection(StripeConnectPayoutOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient<IConnectPayoutProvider, StripeConnectPayoutProvider>();
        services.AddHttpClient<IStripeConnectWebhookNormalizer, StripeConnectPayoutProvider>();
        services.AddSingleton<IPayoutProviderEvidenceVerifier, StripePayoutProviderEvidenceVerifier>();
        services.AddScoped<IPayoutAuthorizationEvidenceWriter, PostgreSqlPayoutAuthorizationEvidenceWriter>();
        services.AddScoped<IPayoutDispatchOutboxWriter, PostgreSqlPayoutDispatchOutboxWriter>();
        services.AddScoped<IPayoutDispatchOutboxProcessor, PostgreSqlPayoutDispatchOutboxProcessor>();

        // Composition is unconditional. Signed policies, provider readiness and capability
        // receipts decide whether value may move; missing configuration stays fail-closed.
        services.AddScoped<IDurablePayoutReservationWorkflow, PostgreSqlDurablePayoutReservationWorkflow>();
        services.AddScoped<IDurablePayoutSettlementWorkflow, PostgreSqlDurablePayoutSettlementWorkflow>();
        services.AddScoped<IDurablePayoutApplicationService, DurablePayoutApplicationService>();

        return services;
    }
}

public static class PayoutsCompositionExtensions
{
    public static IServiceCollection AddPayoutsComposition(
        this IServiceCollection services,
        IConfiguration configuration) => new PayoutsModule().ConfigureServices(services, configuration);
}
