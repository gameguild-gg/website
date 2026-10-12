using Microsoft.EntityFrameworkCore;

namespace GameGuild.Compliance.Audit;

public interface IAuditRetentionSimulationRepository
{
    Task<AuditRetentionConfiguration?> GetConfigurationAsync(Guid tenantId, CancellationToken cancellationToken);
    Task SaveConfigurationAsync(AuditRetentionConfiguration configuration, bool isNew, CancellationToken cancellationToken);
    Task AddRunAsync(AuditRetentionSimulationRun run, CancellationToken cancellationToken);
    Task<AuditRetentionSimulationRun?> GetRunAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditRetentionSimulationSummary>> GetRunsAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken);
}

public sealed class AuditRetentionSimulationRepository(IApplicationDbContext context) : IAuditRetentionSimulationRepository
{
    public Task<AuditRetentionConfiguration?> GetConfigurationAsync(Guid tenantId, CancellationToken cancellationToken) =>
        context.Set<AuditRetentionConfiguration>().SingleOrDefaultAsync(item => item.TenantId == tenantId, cancellationToken);

    public async Task SaveConfigurationAsync(AuditRetentionConfiguration configuration, bool isNew, CancellationToken cancellationToken)
    {
        if (isNew) { context.Set<AuditRetentionConfiguration>().Add(configuration); }
        try { await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false); }
        catch (DbUpdateConcurrencyException) { throw new AuditRetentionConcurrencyException(); }
        catch (DbUpdateException exception) when (isNew && IsUniqueViolation(exception))
        {
            throw new AuditRetentionConcurrencyException();
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException?.GetType().GetProperty("SqlState")?.GetValue(exception.InnerException) as string == "23505";

    public async Task AddRunAsync(AuditRetentionSimulationRun run, CancellationToken cancellationToken)
    {
        context.Set<AuditRetentionSimulationRun>().Add(run);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<AuditRetentionSimulationRun?> GetRunAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        context.Set<AuditRetentionSimulationRun>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AuditRetentionSimulationSummary>> GetRunsAsync(
        Guid tenantId, int skip, int take, CancellationToken cancellationToken) =>
        await context.Set<AuditRetentionSimulationRun>().AsNoTracking().Where(item => item.TenantId == tenantId)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Skip(skip).Take(take)
            .Select(item => new AuditRetentionSimulationSummary(item.Id, item.CreatedByUserId, item.CreatedAt,
                item.ConfigurationRevision, item.Currency, item.ForecastMonths, item.BaselineTotalCost, item.RecommendedTotalCost))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
}
