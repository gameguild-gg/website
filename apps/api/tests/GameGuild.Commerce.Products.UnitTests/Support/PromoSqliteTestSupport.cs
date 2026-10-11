using System.Data.Common;
using GameGuild.Commerce.Products;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Tests.Commerce.Products.Unit.Support;

/// <summary>
/// Relational (SQLite in-memory) test support for promo engine persistence semantics:
/// guarded conditional updates, unique idempotency index, and transaction behavior.
/// A shared in-memory database allows multiple concurrent contexts/connections for
/// race-condition tests.
/// </summary>
public static class PromoSqliteTestSupport
{
    /// <summary>Create a connection to a private in-memory database (single connection only).</summary>
    public static DbConnection CreatePrivateConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Create a connection to a temporary-file SQLite database. Distinct connections to the
    /// same file see the same data and writers serialize through file locks (with busy retry),
    /// enabling realistic multi-connection concurrency tests.
    /// </summary>
    public static DbConnection CreateFileConnection(string path)
    {
        var connection = new SqliteConnection($"DataSource={path}");
        connection.Open();
        return connection;
    }

    /// <summary>A test DbContext exposing only the promo model subset, implementing <see cref="IApplicationDbContext"/>.</summary>
    public sealed class PromoTestContext : DbContext, IApplicationDbContext
    {
        private readonly DbConnection _connection;

        public PromoTestContext(DbConnection connection)
        {
            _connection = connection;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                .UseSqlite(_connection)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PromoCode>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Code).HasMaxLength(50);
                b.HasIndex(x => x.Code).IsUnique();
                b.Ignore(x => x.Product);
                b.Ignore(x => x.CreatedByUser);
                b.Ignore(x => x.PromoCodeUses);
            });

            modelBuilder.Entity<PromoCodeUse>(b =>
            {
                b.HasKey(x => x.Id);
                b.HasIndex(x => x.IdempotencyKey).IsUnique();
                b.HasIndex(x => x.PromoCodeId);
                b.HasIndex(x => x.UserId);
                b.Property(x => x.DiscountApplied).HasPrecision(10, 2);
                b.Ignore(x => x.PromoCode);
                b.Ignore(x => x.User);
            });

            modelBuilder.Entity<PromoCodeUserUsageCounter>(b =>
            {
                b.HasKey(x => new { x.PromoCodeId, x.UserId });
            });

            // Product enters the subset model through the PromoCode.ProductId foreign-key
            // pairing; its two bundle collections (BundleItems/IncludedInBundles) are
            // ambiguous by convention, so every Product navigation is ignored to keep the
            // promo-only model valid under the relational model validator.
            modelBuilder.Entity<Product>(b =>
            {
                b.HasKey(x => x.Id);
                b.Ignore(x => x.Creator);
                b.Ignore(x => x.Pricing);
                b.Ignore(x => x.SubscriptionPlans);
                b.Ignore(x => x.UserProducts);
                b.Ignore(x => x.PromoCodes);
                b.Ignore(x => x.CommissionConfig);
                b.Ignore(x => x.BundleItems);
                b.Ignore(x => x.IncludedInBundles);
            });

            modelBuilder.Entity<PromoStackingRule>(b =>
            {
                b.HasKey(x => x.Id);
                b.HasIndex(x => x.Name).IsUnique();
            });
        }

        Task<IDbContextTransaction> IApplicationDbContext.BeginTransactionAsync(CancellationToken cancellationToken) =>
            Database.BeginTransactionAsync(cancellationToken);
    }

    /// <summary>Create a context and (optionally) ensure the promo schema exists.</summary>
    public static PromoTestContext CreateContext(DbConnection connection, bool ensureCreated = true)
    {
        var context = new PromoTestContext(connection);
        if (ensureCreated)
        {
            context.Database.EnsureCreated();
        }

        return context;
    }

    /// <summary>Seed one active promo code and return it.</summary>
    public static PromoCode SeedPromoCode(DbConnection connection, Action<PromoCode>? configure = null)
    {
        using var context = CreateContext(connection);
        var promo = new PromoCode
        {
            Id = Guid.NewGuid(),
            Code = "TESTCODE",
            Name = "Test code",
            Type = PromoCodeType.PercentageOff,
            DiscountPercentage = 10m,
            Currency = "USD",
            IsActive = true
        };
        configure?.Invoke(promo);
        context.Set<PromoCode>().Add(promo);
        context.SaveChanges();
        return promo;
    }
}
