using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.Finance.Economy.AdRewards;

public sealed class AdRewardsModule : ModuleBase
{
    public override string Name => "Finance.Economy.AdRewards";
    public override bool EnabledByDefault => true;

    public override IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.TryAddSingleton<IAdRewardSessionEntropy, CryptographicAdRewardSessionEntropy>();
        services.AddScoped<IDurableAdRewardPolicyReader, PostgreSqlDurableAdRewardPolicyReader>();
        services.AddScoped<IAdRewardSessionTokenProtector, KmsAdRewardSessionTokenProtector>();
        services.AddScoped<IAdRewardProviderAdapterResolver, AdRewardProviderAdapterResolver>();
        services.AddScoped<IDurableAdRewardSessionService, DurableAdRewardSessionService>();
        services.AddScoped<IDurableAdRewardSessionReader, PostgreSqlDurableAdRewardSessionReader>();
        services.AddScoped<IDurableAdRewardCompletionService, DurableAdRewardCompletionService>();
        services.AddScoped<IDurableAdRewardReportService, DurableAdRewardReportService>();
        services.AddScoped<IDurableAdRewardReportReader, PostgreSqlDurableAdRewardReportReader>();
        services.AddScoped<IAdRewardOperationalQueryReader, PostgreSqlAdRewardOperationalQueryReader>();
        services.AddScoped<IDurableDeferredAdRewardService, DurableDeferredAdRewardService>();
        return services;
    }
}

public static class AdRewardsCompositionExtensions
{
    public static IServiceCollection AddAdRewardsComposition(
        this IServiceCollection services,
        IConfiguration configuration) => new AdRewardsModule().ConfigureServices(services, configuration);
}
