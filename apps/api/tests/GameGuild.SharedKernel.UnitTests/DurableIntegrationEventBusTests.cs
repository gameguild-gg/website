using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameGuild.SharedKernel.UnitTests;

public sealed class DurableIntegrationEventBusTests
{
    [Fact]
    public async Task PublishAsync_WhenEventIsDurable_RequiresTransactionalOutbox()
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        var bus = new InMemoryIntegrationEventBus(
            services,
            NullLogger<InMemoryIntegrationEventBus>.Instance);

        var act = () => bus.PublishAsync(new UseCaseOperationOccurredV1
        {
            AggregateType = "test",
            AggregateId = Guid.NewGuid().ToString("N"),
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*transactional outbox*");
    }
}
