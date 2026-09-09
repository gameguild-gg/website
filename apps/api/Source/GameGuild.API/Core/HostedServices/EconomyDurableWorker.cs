using GameGuild.Finance.Economy.Bounties;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Marketplace;
using GameGuild.Finance.Economy.Payouts;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Finance.Economy.Treasury;
using Microsoft.Extensions.Options;

namespace GameGuild.API.HostedServices;

public sealed class EconomyDurableWorkerOptions
{
    public const string SectionName = "Economy:Workers";

    public bool SafetyWorkersEnabled { get; set; } = true;
    public bool DispatchWorkersEnabled { get; set; }
    public bool AnchorWorkersEnabled { get; set; }
    public bool InternalOutboxWorkersEnabled { get; set; } = true;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);
    public int JournalBatchSize { get; set; } = 1_000;
    public int BountyExpirationBatchSize { get; set; } = 100;
    public int MarketplaceOutboxBatchSize { get; set; } = 100;
}

/// <summary>
/// Runs durable Economy jobs through scoped PostgreSQL services. External value dispatch and
/// WORM publication have independent, fail-closed switches and therefore never start merely
/// because the modules are composed.
/// </summary>
public sealed class EconomyDurableWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<EconomyDurableWorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<EconomyDurableWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        Validate(settings);
        var workerId = $"{Environment.MachineName}:{Environment.ProcessId}";

        while (!stoppingToken.IsCancellationRequested)
        {
            if (settings.SafetyWorkersEnabled)
                await RunSafetyCycleAsync(workerId, settings, stoppingToken).ConfigureAwait(false);
            if (settings.DispatchWorkersEnabled)
                await RunDispatchCycleAsync(workerId, stoppingToken).ConfigureAwait(false);

            await Task.Delay(settings.PollInterval, timeProvider, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunSafetyCycleAsync(
        string workerId,
        EconomyDurableWorkerOptions settings,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var now = timeProvider.GetUtcNow();
            await scope.ServiceProvider.GetRequiredService<IEconomyCapabilityPolicyStore>()
                .ActivateDueAsync(now, cancellationToken).ConfigureAwait(false);
            var integrity = await scope.ServiceProvider.GetRequiredService<IJournalIntegrityService>()
                .RunIncrementAsync(workerId, now, settings.JournalBatchSize, cancellationToken)
                .ConfigureAwait(false);
            await scope.ServiceProvider.GetRequiredService<IDurableBountyExpirationWorkflow>()
                .ExpireDueAsync(now, settings.BountyExpirationBatchSize, cancellationToken)
                .ConfigureAwait(false);

            if (settings.InternalOutboxWorkersEnabled)
            {
                var marketplaceOutbox = scope.ServiceProvider.GetRequiredService<IMarketplaceOutboxProcessor>();
                for (var index = 0; index < settings.MarketplaceOutboxBatchSize; index++)
                {
                    var outcome = await marketplaceOutbox.ProcessNextAsync(
                        workerId, now, cancellationToken).ConfigureAwait(false);
                    if (outcome.Status != MarketplaceOutboxProcessStatus.Published) break;
                }
            }

            if (settings.AnchorWorkersEnabled && integrity.Status == JournalIntegrityRunStatus.Verified)
            {
                await scope.ServiceProvider.GetRequiredService<IEconomyAnchorPublisher>()
                    .PublishIfDueAsync(now, false, null, cancellationToken).ConfigureAwait(false);
                await scope.ServiceProvider.GetRequiredService<IEconomyAnchorVerificationService>()
                    .VerifyPublishedAnchorsAsync(now, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Economy safety worker cycle failed closed.");
        }
    }

    private async Task RunDispatchCycleAsync(string workerId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var now = timeProvider.GetUtcNow();
            await scope.ServiceProvider.GetRequiredService<IPayoutDispatchOutboxProcessor>()
                .ProcessNextAsync(workerId, now, cancellationToken).ConfigureAwait(false);
            await scope.ServiceProvider.GetRequiredService<IAdminWithdrawalDispatchOutboxProcessor>()
                .ProcessNextAsync(workerId, now, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Economy provider dispatch worker cycle failed closed.");
        }
    }

    private static void Validate(EconomyDurableWorkerOptions settings)
    {
        if (settings.PollInterval < TimeSpan.FromMilliseconds(100))
            throw new OptionsValidationException(
                EconomyDurableWorkerOptions.SectionName,
                typeof(EconomyDurableWorkerOptions),
                ["PollInterval must be at least 100 milliseconds."]);
        if (settings.JournalBatchSize is < 1 or > 10_000)
            throw new OptionsValidationException(
                EconomyDurableWorkerOptions.SectionName,
                typeof(EconomyDurableWorkerOptions),
                ["JournalBatchSize must be between 1 and 10,000."]);
        if (settings.BountyExpirationBatchSize is < 1 or > 1_000)
            throw new OptionsValidationException(
                EconomyDurableWorkerOptions.SectionName,
                typeof(EconomyDurableWorkerOptions),
                ["BountyExpirationBatchSize must be between 1 and 1,000."]);
        if (settings.MarketplaceOutboxBatchSize is < 1 or > 1_000)
            throw new OptionsValidationException(
                EconomyDurableWorkerOptions.SectionName,
                typeof(EconomyDurableWorkerOptions),
                ["MarketplaceOutboxBatchSize must be between 1 and 1,000."]);
    }
}
