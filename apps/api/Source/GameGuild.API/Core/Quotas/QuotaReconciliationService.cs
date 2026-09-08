using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using GameGuild.API.Database;
using GameGuild.Assets;
using GameGuild.Identity.Tenants;
using GameGuild.Resources;

namespace GameGuild.API.Core.Quotas;

public sealed class QuotaReconciliationOptions
{
    public const string SectionName = "QuotaReconciliation";
    public bool Enabled { get; set; } = true;
    public int InitialDelayMinutes { get; set; } = 5;
    public int IntervalMinutes { get; set; } = 360;
}

public sealed class QuotaReconciliationService
{
    private readonly ApplicationDbContext context;
    private readonly IReadOnlyDictionary<ResourceUsageType, IQuotaAuthoritativeUsageSource> sources;

    public QuotaReconciliationService(ApplicationDbContext context,
        IEnumerable<IQuotaAuthoritativeUsageSource>? productSources = null)
    {
        this.context = context;
        IQuotaAuthoritativeUsageSource[] commonSources =
            [new TenantMemberQuotaUsageSource(context), new AssetReferenceQuotaUsageSource(context)];
        // ToDictionary deliberately rejects duplicate ownership instead of choosing an arbitrary counter.
        sources = commonSources.Concat(productSources ?? []).ToDictionary(source => source.ResourceType);
    }

    public async Task<int> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var reconciledTypes = sources.Keys.ToArray();
        var quotas = await context.Set<ResourceQuota>()
            .IgnoreQueryFilters()
            .Where(quota => quota.UserId == null && reconciledTypes.Contains(quota.Type))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var repairs = 0;
        foreach (var quota in quotas)
        {
            var authoritativeUsage = await sources[quota.Type].CountAsync(
                    quota.TenantId ?? DurableIntegrationEventTenants.Platform,
                    cancellationToken)
                .ConfigureAwait(false);
            var delta = authoritativeUsage - quota.CurrentUsage;
            if (delta == 0)
                continue;

            quota.CurrentUsage = authoritativeUsage;
            quota.Touch();
            context.Add(new QuotaLedgerEntry
            {
                EventId = Guid.NewGuid(),
                TenantId = quota.TenantId,
                ResourceType = quota.Type,
                Delta = delta,
                OperationCode = "quota.reconciled",
                CorrelationId = Guid.NewGuid(),
                AggregateType = nameof(ResourceQuota),
                AggregateId = quota.Id.ToString(),
                TimestampUtc = SystemClock.UtcNow,
                IsReservationFinalization = false
            });
            repairs++;
        }

        if (repairs > 0)
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return repairs;
    }

    private sealed class TenantMemberQuotaUsageSource(ApplicationDbContext context) : IQuotaAuthoritativeUsageSource
    {
        public ResourceUsageType ResourceType => ResourceUsageType.Users;
        public Task<long> CountAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            context.Set<TenantMember>()
                .IgnoreQueryFilters()
                .LongCountAsync(member => member.TenantId == tenantId && member.IsActive && member.DeletedAt == null,
                    cancellationToken);
    }

    private sealed class AssetReferenceQuotaUsageSource(ApplicationDbContext context) : IQuotaAuthoritativeUsageSource
    {
        public ResourceUsageType ResourceType => ResourceUsageType.Assets;
        public Task<long> CountAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            context.Set<AssetReference>()
                .IgnoreQueryFilters()
                .LongCountAsync(reference => reference.TenantId == tenantId && reference.DeletedAt == null,
                    cancellationToken);
    }
}

public sealed class QuotaReconciliationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<QuotaReconciliationOptions> options,
    ILogger<QuotaReconciliationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
            return;

        await Task.Delay(TimeSpan.FromMinutes(Math.Max(0, settings.InitialDelayMinutes)), stoppingToken)
            .ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<QuotaReconciliationService>();
                var repairs = await service.ReconcileAsync(stoppingToken).ConfigureAwait(false);
                if (repairs > 0)
                    logger.LogWarning("Quota reconciliation repaired {RepairCount} tenant counters", repairs);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Quota reconciliation failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, settings.IntervalMinutes)), stoppingToken)
                .ConfigureAwait(false);
        }
    }
}
