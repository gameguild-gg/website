using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.Finance.Economy.Bounties;

public sealed class BountiesModule : ModuleBase
{
    public override string Name => "Finance.Economy.Bounties";
    public override bool EnabledByDefault => true;
    public override IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.TryAddScoped<IBountyEscrowStore, PostgreSqlBountyEscrowStore>();
        services.TryAddScoped<IBountyTerminalEventStore, PostgreSqlBountyTerminalEventStore>();
        services.TryAddScoped<IBountyPostableLotReader, PostgreSqlBountyPostableLotReader>();
        services.TryAddScoped<IDurableBountyEscrowPostWorkflow, PostgreSqlDurableBountyEscrowPostWorkflow>();
        services.TryAddScoped<IBountyTerminalClaimWriter, PostgreSqlBountyTerminalClaimWriter>();
        services.TryAddScoped<IDurableBountyClaimWorkflow, PostgreSqlDurableBountyClaimWorkflow>();
        services.TryAddScoped<IBountyTerminalReclaimWriter, PostgreSqlBountyTerminalReclaimWriter>();
        services.TryAddScoped<PostgreSqlBountyExpirationWorkflow>();
        services.TryAddScoped<IDurableBountyExpirationWorkflow>(provider =>
            provider.GetRequiredService<PostgreSqlBountyExpirationWorkflow>());
        services.TryAddScoped<IBountyExpirationTransition>(provider =>
            provider.GetRequiredService<PostgreSqlBountyExpirationWorkflow>());
        services.TryAddScoped<IDurableBountyReclaimWorkflow, PostgreSqlDurableBountyReclaimWorkflow>();
        services.TryAddScoped<IDurableBountyApplicationService, DurableBountyApplicationService>();
        return services;
    }
}

public static class BountiesCompositionExtensions
{
    public static IServiceCollection AddBountiesComposition(
        this IServiceCollection services,
        IConfiguration configuration) => new BountiesModule().ConfigureServices(services, configuration);
}
