using GameGuild.Commerce.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Commerce.Payments.UnitTests;

internal sealed class PaymentsPersistenceTestDbContext(DbContextOptions<PaymentsPersistenceTestDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Database.BeginTransactionAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UserWallet).Assembly);
        modelBuilder.Entity<Payment>();
        modelBuilder.Entity<CustomerTaxExemption>();
        modelBuilder.Entity<RevenueReconciliationRun>();
        modelBuilder.Entity<RevenueReconciliationDiscrepancy>();
        modelBuilder.Entity<RevenueAnomalyAlert>();
        base.OnModelCreating(modelBuilder);
    }
}
