using System.Data.Common;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Ledger;

public interface IEconomyWalletProvisioner
{
    ValueTask<EconomyWalletIdentity> ProvisionAsync(
        Guid tenantId,
        Guid ownerId,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default);
}

public sealed class PostgreSqlEconomyWalletProvisioner : IEconomyWalletProvisioner
{
    private readonly DbContext _db;

    public PostgreSqlEconomyWalletProvisioner(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext ?? throw new InvalidOperationException(
            "Economy wallet provisioning requires the application's relational DbContext.");
    }

    public async ValueTask<EconomyWalletIdentity> ProvisionAsync(
        Guid tenantId,
        Guid ownerId,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner ID is required.", nameof(ownerId));

        try
        {
            var result = await _db.Set<EconomyWalletProvisioningReceiptRow>()
                .FromSqlInterpolated($"""
                    SELECT * FROM economy_private.provision_economy_wallet_v1(
                        {tenantId}, {ownerId}, {createdAt})
                    """)
                .AsNoTracking()
                .SingleAsync(cancellationToken);
            return new EconomyWalletIdentity(
                new Contracts.WalletId(result.WalletId),
                tenantId,
                ownerId,
                Contracts.WalletLifecycleState.Active);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            throw new EconomyWalletUnavailableException(
                "The protected Economy wallet provisioner rejected the request.", exception);
        }
    }

    private static bool IsDatabaseFailure(Exception exception) =>
        exception is DbException or DbUpdateException or InvalidOperationException ||
        exception.GetBaseException() is DbException;
}

internal sealed class EconomyWalletProvisioningReceiptRow
{
    public Guid WalletId { get; set; }
    public bool Created { get; set; }
}
