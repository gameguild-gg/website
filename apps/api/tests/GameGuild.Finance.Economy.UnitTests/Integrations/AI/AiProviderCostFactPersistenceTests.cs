using FluentAssertions;
using GameGuild.Finance.Economy.Integrations.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameGuild.Finance.Economy.UnitTests;

public sealed class AiProviderCostFactPersistenceTests
{
    [Fact]
    public void Entity_RoundTripsEveryExactProviderCostField()
    {
        var fact = Fact();

        var entity = AiProviderCostFactEntity.FromDomain(fact);

        entity.ToDomain().Should().Be(fact);
        FluentActions.Invoking(() => AiProviderCostFactEntity.FromDomain(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task EfStore_PersistsExactProviderCostFact()
    {
        await using var db = CreateDbContext();
        var store = new EfAiProviderCostFactStore(db);
        var fact = Fact();

        store.Save(fact);

        var persisted = await db.Set<AiProviderCostFactEntity>().AsNoTracking().SingleAsync();
        persisted.ToDomain().Should().Be(fact);
        FluentActions.Invoking(() => store.Save(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new EfAiProviderCostFactStore(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Model_UsesAppendOnlyIdentityAndReplayConstraints()
    {
        using var db = CreateDbContext();
        var entity = db.Model.FindEntityType(typeof(AiProviderCostFactEntity));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("ai_provider_cost_facts");
        entity!.FindPrimaryKey()!.Properties.Select(property => property.Name).Should().Equal("Id");
        entity!.FindProperty(nameof(AiProviderCostFactEntity.Id))!.ValueGenerated.Should().Be(ValueGenerated.Never);
        entity!.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "AuthorizationId" }));
        entity!.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "Provider", "ProviderUsageId" }));
    }

    [Fact]
    public void RelationalModel_EnforcesTokenCostAndChargeConservation()
    {
        using var db = CreateRelationalDbContext();
        var entity = db.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(AiProviderCostFactEntity));

        entity.Should().NotBeNull();
        entity!.GetCheckConstraints().Select(constraint => constraint.Name).Should().BeEquivalentTo(
            "ck_ai_provider_cost_facts_token_conservation",
            "ck_ai_provider_cost_facts_cost_conservation",
            "ck_ai_provider_cost_facts_charge_positive");
    }

    [Fact]
    public void EconomyModule_RegistersTheEfProviderCostFactStore()
    {
        var services = new ServiceCollection();
        services.AddEconomyCoreComposition(new ConfigurationBuilder().Build());

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IAiProviderCostFactStore) &&
            descriptor.ImplementationType == typeof(EfAiProviderCostFactStore) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    private static AiProviderCostFact Fact() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        Guid.Parse("55555555-5555-5555-5555-555555555555"),
        "ai.grade",
        AiProvider.OpenAi,
        "gpt-test",
        "usage-1",
        1_250,
        750,
        2_000,
        500_000,
        300_000,
        800_000,
        100_000,
        "rate-v1",
        new DateTimeOffset(2026, 8, 2, 12, 1, 0, TimeSpan.Zero));

    private static TestAiCostDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TestAiCostDbContext>()
            .UseInMemoryDatabase($"ai-cost-{Guid.NewGuid()}")
            .Options;
        return new TestAiCostDbContext(options);
    }

    private static TestAiCostDbContext CreateRelationalDbContext()
    {
        var options = new DbContextOptionsBuilder<TestAiCostDbContext>()
            .UseNpgsql("Host=localhost;Database=ai_cost_model;Username=test;Password=test")
            .Options;
        return new TestAiCostDbContext(options);
    }

    private sealed class TestAiCostDbContext(DbContextOptions<TestAiCostDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new AiProviderCostFactEntityConfiguration().Configure(
                modelBuilder.Entity<AiProviderCostFactEntity>());

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
