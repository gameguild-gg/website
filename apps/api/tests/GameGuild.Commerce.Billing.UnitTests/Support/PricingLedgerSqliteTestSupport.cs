using System.Data.Common;
using GameGuild.Commerce;
using GameGuild.Commerce.Billing.Data.Configurations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Commerce.Billing.UnitTests.Support;

/// <summary>
/// Relational (SQLite in-memory) test support for the pricing-decision ledger: exposes
/// the real <see cref="PricingDecisionLedgerEntryConfiguration"/> model (unique
/// idempotency index included) through an <see cref="IApplicationDbContext"/> backed by
/// one private in-memory connection.
/// </summary>
public static class PricingLedgerSqliteTestSupport
{
    /// <summary>Creates a context backed by a private in-memory SQLite database.</summary>
    public static PricingLedgerTestContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var context = new PricingLedgerTestContext(connection);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>Test DbContext exposing the pricing-decision ledger model subset.</summary>
    public sealed class PricingLedgerTestContext : DbContext, IApplicationDbContext
    {
        private readonly DbConnection _connection;

        public PricingLedgerTestContext(DbConnection connection)
        {
            _connection = connection;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(_connection);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new PricingDecisionLedgerEntryConfiguration());
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Database.BeginTransactionAsync(cancellationToken);
    }
}
