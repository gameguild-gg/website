using FluentAssertions;
using GameGuild.Finance.Economy.AdRewards.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GameGuild.Finance.Economy.AdRewards.UnitTests;

public sealed class AdRewardsPersistenceModelTests
{
    [Fact]
    public void Configure_MapsDurableAdRewardFactsAndReplayGuards()
    {
        using var context = new ModelContext();
        var entities = context.GetService<IDesignTimeModel>().Model.GetEntityTypes().ToArray();

        entities.Select(entity => entity.GetTableName()).Should().BeEquivalentTo(
            "economy_ad_network_policy_versions",
            "economy_ad_reward_sessions",
            "economy_ad_reward_playback_milestones",
            "economy_ad_reward_session_events",
            "economy_ad_reward_provider_proof_inbox",
            "economy_ad_reward_pending_claims",
            "economy_ad_reward_cap_consumptions",
            "economy_ad_reward_completions",
            "economy_ad_reward_accumulators",
            "economy_ad_reward_budget_consumptions",
            "economy_ad_reward_attributions",
            "economy_ad_provider_reports",
            "economy_ad_reward_provider_batch_claims",
            "economy_ad_reward_reconciliations");

        Entity(entities, "economy_ad_reward_completions").GetIndexes()
            .Should().Contain(index => index.IsUnique &&
                index.Properties.Select(property => property.Name).SequenceEqual(new[] { "IdempotencyKey" }));
        Entity(entities, "economy_ad_provider_reports").GetIndexes()
            .Should().Contain(index => index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(new[] { "TenantId", "Network", "ReportId", "Version" }));
        Entity(entities, "economy_ad_reward_reconciliations").GetCheckConstraints()
            .Should().Contain(constraint => constraint.Name == "ck_economy_ad_reward_reconciliations_conservation");
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
            new AdRewardsModelConfiguration().Configure(modelBuilder);
    }
}
