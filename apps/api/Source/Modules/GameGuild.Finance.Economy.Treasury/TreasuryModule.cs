using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.Finance.Economy.Treasury;

public sealed class TreasuryModule : ModuleBase
{
    public override string Name => "Finance.Economy.Treasury";
    public override bool EnabledByDefault => true;

    public override IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddScoped<IAdminWithdrawalStore, PostgreSqlAdminWithdrawalStore>();
        services.AddScoped<IAdminWithdrawalAuditTrail, PostgreSqlAdminWithdrawalAuditTrail>();
        services.AddScoped<IAdminWithdrawalFencingTokenAllocator, PostgreSqlAdminWithdrawalFencingTokenAllocator>();
        services.AddOptions<StripePlatformWithdrawalOptions>()
            .Bind(configuration.GetSection(StripePlatformWithdrawalOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient<IAdminWithdrawalProvider, StripePlatformAdminWithdrawalProvider>();
        services.AddHttpClient<IStripeTreasuryWebhookNormalizer, StripePlatformAdminWithdrawalProvider>();
        services.AddSingleton<IAdminWithdrawalProviderEvidenceVerifier, StripeAdminWithdrawalProviderEvidenceVerifier>();
        services.AddScoped<IDurableAdminWithdrawalWorkflow, PostgreSqlDurableAdminWithdrawalWorkflow>();
        services.AddScoped<IDurableAdminWithdrawalApplicationService, DurableAdminWithdrawalApplicationService>();
        services.AddScoped<IAdminWithdrawalDispatchOutboxWriter, PostgreSqlAdminWithdrawalDispatchOutboxWriter>();
        services.AddScoped<IAdminWithdrawalDispatchOutboxProcessor, PostgreSqlAdminWithdrawalDispatchOutboxProcessor>();
        return services;
    }
}

public static class TreasuryCompositionExtensions
{
    public static IServiceCollection AddTreasuryComposition(
        this IServiceCollection services,
        IConfiguration configuration) => new TreasuryModule().ConfigureServices(services, configuration);
}
