using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Ledger;

public sealed record EconomyWalletIdentity(
    WalletId WalletId,
    Guid TenantId,
    Guid OwnerId,
    WalletLifecycleState State);

public interface IEconomyWalletDirectory
{
    ValueTask<EconomyWalletIdentity> GetOwnerWalletAsync(
        Guid tenantId,
        Guid ownerId,
        CancellationToken cancellationToken = default);

    ValueTask<EconomyWalletIdentity> GetWalletAsync(
        Guid tenantId,
        WalletId walletId,
        CancellationToken cancellationToken = default);
}

public sealed class PostgreSqlEconomyWalletDirectory : IEconomyWalletDirectory
{
    private readonly DbContext _db;

    public PostgreSqlEconomyWalletDirectory(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Economy wallet resolution requires the application's relational DbContext.");
    }

    public async ValueTask<EconomyWalletIdentity> GetOwnerWalletAsync(
        Guid tenantId,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || ownerId == Guid.Empty)
            throw new ArgumentException("Tenant and owner IDs are required.");
        var row = await _db.Set<EconomyWalletRow>().AsNoTracking()
            .SingleOrDefaultAsync(
                wallet => wallet.TenantId == tenantId && wallet.OwnerId == ownerId,
                cancellationToken);
        return MapActive(row);
    }

    public async ValueTask<EconomyWalletIdentity> GetWalletAsync(
        Guid tenantId,
        WalletId walletId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        var row = await _db.Set<EconomyWalletRow>().AsNoTracking()
            .SingleOrDefaultAsync(
                wallet => wallet.TenantId == tenantId && wallet.Id == walletId.Value,
                cancellationToken);
        return MapActive(row);
    }

    private static EconomyWalletIdentity MapActive(EconomyWalletRow? row)
    {
        if (row is null || row.State != WalletLifecycleState.Active)
            throw new EconomyWalletUnavailableException(
                "An active Economy wallet was not found in the actor tenant.");
        return new EconomyWalletIdentity(new WalletId(row.Id), row.TenantId, row.OwnerId, row.State);
    }
}

public sealed class EconomyWalletUnavailableException : InvalidOperationException
{
    public EconomyWalletUnavailableException(string message) : base(message) { }

    public EconomyWalletUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}
