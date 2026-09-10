using FluentAssertions;
using GameGuild.Finance.Economy.Marketplace.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GameGuild.Finance.Economy.Marketplace.UnitTests;

public sealed class MarketplacePersistenceModelTests
{
    [Fact]
    public void Configure_MapsPoliciesAtomicSettlementsAndRefunds()
    {
        using var context = new ModelContext();
        var entities = context.GetService<IDesignTimeModel>().Model.GetEntityTypes().ToArray();

        entities.Select(entity => entity.GetTableName()).Should().BeEquivalentTo(
            "economy_marketplace_currency_policy_versions",
            "economy_marketplace_settlements",
            "economy_marketplace_settlement_legs",
            "economy_marketplace_funding_fragments",
            "economy_marketplace_settlement_credits",
            "economy_marketplace_refunds",
            "economy_marketplace_refund_legs",
            "economy_marketplace_refund_debts",
            "economy_marketplace_events",
            "economy_marketplace_outbox");

        Entity(entities, "economy_marketplace_settlements").GetIndexes()
            .Should().Contain(index => index.IsUnique &&
                index.Properties.Select(property => property.Name).SequenceEqual(new[] { "TenantId", "OrderId" }));
        Entity(entities, "economy_marketplace_refunds").GetIndexes()
            .Should().Contain(index => index.IsUnique &&
                index.Properties.Select(property => property.Name).SequenceEqual(new[] { "TenantId", "IdempotencyKey" }));
        Entity(entities, "economy_marketplace_settlement_legs").GetCheckConstraints()
            .Should().Contain(constraint => constraint.Name == "ck_economy_marketplace_settlement_legs_conservation");
        Entity(entities, "economy_marketplace_refund_legs").GetIndexes()
            .Should().Contain(index => !index.IsUnique &&
                index.Properties.Select(property => property.Name).SequenceEqual(new[] { "SettlementId", "Currency" }));
        Entity(entities, "economy_marketplace_refunds").GetCheckConstraints()
            .Should().Contain(constraint => constraint.Name == "ck_economy_marketplace_refunds_quantity");
    }

    [Fact]
    public void PersistenceRows_RoundTripEveryMappedProperty()
    {
        using var context = new ModelContext();
        var rows = context.GetService<IDesignTimeModel>().Model.GetEntityTypes()
            .Select(entity => Activator.CreateInstance(entity.ClrType, nonPublic: true)!)
            .ToArray();
        AssertPropertyRoundTrips(rows);
    }

    private static void AssertPropertyRoundTrips(params object[] rows)
    {
        foreach (var row in rows)
        foreach (var property in row.GetType().GetProperties())
        {
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            object value = type == typeof(string)
                ? "value"
                : type == typeof(Guid)
                    ? Guid.Parse("10000000-0000-0000-0000-000000000001")
                    : type == typeof(DateTimeOffset)
                        ? new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero)
                        : type == typeof(long)
                            ? 1L
                        : type == typeof(int)
                            ? 1
                            : type == typeof(decimal)
                                ? 1m
                                : type == typeof(bool)
                                    ? true
                                    : Enum.ToObject(type, 1);
            property.SetValue(row, value);
            property.GetValue(row).Should().Be(value);
        }
    }

    private static IReadOnlyEntityType Entity(IEnumerable<IReadOnlyEntityType> entities, string table) =>
        entities.Single(entity => entity.GetTableName() == table);

    private sealed class ModelContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder.UseNpgsql("Host=localhost;Database=model;Username=model;Password=model");

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new MarketplaceModelConfiguration().Configure(modelBuilder);
    }
}
