using FluentAssertions;
using GameGuild.CQRS;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Queries;

public class GetActiveSubscriptionPlansQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnActivePlans_ThroughTheListCache()
    {
        // Arrange
        var plans = new[]
        {
            CreatePlan("starter", 999),
            CreatePlan("pro", 1999)
        };

        var repository = new Mock<ISubscriptionPlanRepository>();
        repository
            .Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plans);

        var handler = new GetActiveSubscriptionPlansQueryHandler(repository.Object, CreateCache());

        // Act
        var result = await handler.Handle(new GetActiveSubscriptionPlansQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(plans);
        repository.Verify(r => r.GetActiveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldServeSubsequentReads_FromCacheWithoutHittingTheRepository()
    {
        // Arrange — issue #416: "Plan list queries cached for 5 minutes"
        var repository = new Mock<ISubscriptionPlanRepository>();
        repository
            .Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SubscriptionPlan>());

        var handler = new GetActiveSubscriptionPlansQueryHandler(repository.Object, CreateCache());

        // Act
        await handler.Handle(new GetActiveSubscriptionPlansQuery(), CancellationToken.None);
        await handler.Handle(new GetActiveSubscriptionPlansQuery(), CancellationToken.None);

        // Assert
        repository.Verify(r => r.GetActiveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void AddSubscriptionsModule_ShouldResolveActivePlansQueryHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddSubscriptionsModule();

        services.AddScoped<ISubscriptionPlanRepository>(_ => Mock.Of<ISubscriptionPlanRepository>());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Act
        var handler = scope.ServiceProvider.GetService<IRequestHandler<GetActiveSubscriptionPlansQuery, IEnumerable<SubscriptionPlan>>>();

        // Assert
        handler.Should().NotBeNull();
        handler.Should().BeOfType<GetActiveSubscriptionPlansQueryHandler>();
    }

    internal static ISubscriptionPlanListCache CreateCache()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var memoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            Microsoft.Extensions.Caching.Memory.MemoryCacheOptions.Default);
        return new SubscriptionPlanListCache(
            memoryCache,
            services.BuildServiceProvider().GetRequiredService<ILogger<SubscriptionPlanListCache>>());
    }

    private static SubscriptionPlan CreatePlan(string slug, long monthlyPriceInCents)
    {
        return new SubscriptionPlan(
            name: slug.ToUpperInvariant(),
            slug: slug,
            monthlyPriceInCents: monthlyPriceInCents,
            currency: "USD",
            description: $"{slug} plan");
    }
}
