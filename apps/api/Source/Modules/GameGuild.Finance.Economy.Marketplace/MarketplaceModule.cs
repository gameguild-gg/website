using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using GameGuild.Commerce.Orders;

namespace GameGuild.Finance.Economy.Marketplace;

public sealed class MarketplaceModule : ModuleBase
{
    public override string Name => "Finance.Economy.Marketplace";
    public override bool EnabledByDefault => true;
    public override IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddScoped<IAuthoritativeMarketplaceOrderReader, EfAuthoritativeMarketplaceOrderReader>();
        services.AddScoped<IDurableMarketplacePolicyReader, PostgreSqlDurableMarketplacePolicyReader>();
        services.AddScoped<IDurableMarketplaceSettlementService, DurableMarketplaceSettlementService>();
        services.AddScoped<IDurableMarketplaceRefundService, DurableMarketplaceRefundService>();
        services.AddScoped<IMarketplaceOperationalQueryReader, PostgreSqlMarketplaceOperationalQueryReader>();
        services.TryAddSingleton(TimeProvider.System);
        services.Replace(ServiceDescriptor.Scoped<
            IOrderMarketplaceSettlementAuthority,
            CommerceOrderMarketplaceSettlementAuthority>());
        services.AddScoped<IMarketplaceOutboxHandler, CommerceMarketplaceOutboxHandler>();
        services.AddScoped<IMarketplaceOutboxProcessor, PostgreSqlMarketplaceOutboxProcessor>();
        return services;
    }
}

public static class MarketplaceCompositionExtensions
{
    public static IServiceCollection AddMarketplaceComposition(
        this IServiceCollection services,
        IConfiguration configuration) =>
        new MarketplaceModule().ConfigureServices(services, configuration);
}
