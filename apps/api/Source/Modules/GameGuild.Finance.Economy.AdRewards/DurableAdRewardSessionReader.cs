using GameGuild.Finance.Economy.AdRewards.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.AdRewards;

public sealed record DurableAdRewardSessionStatus(
    Guid SessionId,
    DurableAdRewardSessionState State,
    string Network,
    string CreativeId,
    long RewardSoftUnits,
    Guid? PostingId,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset UpdatedAt);

public interface IDurableAdRewardSessionReader
{
    ValueTask<DurableAdRewardSessionStatus?> FindAsync(
        Guid tenantId,
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);
}

public sealed class PostgreSqlDurableAdRewardSessionReader(IApplicationDbContext context)
    : IDurableAdRewardSessionReader
{
    private readonly DbContext _db = context as DbContext
        ?? throw new InvalidOperationException(
            "Durable ad reward status requires the application's relational DbContext.");

    public async ValueTask<DurableAdRewardSessionStatus?> FindAsync(
        Guid tenantId,
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty || sessionId == Guid.Empty)
            throw new ArgumentException("Tenant, user and session IDs are required.");
        var row = await _db.Set<AdRewardSessionRow>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sessionId &&
                                          item.TenantId == tenantId &&
                                          item.UserId == userId,
                cancellationToken);
        var completion = row is null
            ? null
            : await _db.Set<AdRewardCompletionRow>().AsNoTracking()
                .SingleOrDefaultAsync(item => item.SessionId == sessionId &&
                                              item.TenantId == tenantId &&
                                              item.UserId == userId,
                    cancellationToken);
        return row is null
            ? null
            : new DurableAdRewardSessionStatus(
                row.Id,
                row.State,
                row.Network,
                row.CreativeId,
                completion?.RewardSoftUnits ?? 0,
                completion?.PostingId,
                row.IssuedAt,
                row.ExpiresAt,
                row.UpdatedAt);
    }
}
