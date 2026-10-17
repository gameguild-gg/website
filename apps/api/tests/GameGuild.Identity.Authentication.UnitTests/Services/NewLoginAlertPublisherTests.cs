using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public class NewLoginAlertPublisherTests
{
    private readonly Mock<IDurableEventProducer> _producer = new();

    [Fact]
    public async Task RecordAsync_DoesNotRecord_WhenNotificationsAreDisabled()
    {
        var sut = BuildSut(enabled: false);

        await sut.RecordAsync(Guid.NewGuid(), null, Guid.NewGuid(), "Chrome on Windows");

        _producer.Verify(producer => producer.RecordAsync(It.IsAny<IDurableIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordAsync_DoesNotRecord_WhenUserIdIsEmpty()
    {
        var sut = BuildSut();

        await sut.RecordAsync(Guid.Empty, null, Guid.NewGuid(), "Chrome on Windows");

        _producer.Verify(producer => producer.RecordAsync(It.IsAny<IDurableIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordAsync_RecordsTheNewLoginEvent()
    {
        var sut = BuildSut();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await sut.RecordAsync(userId, tenantId, sessionId, "Chrome 120 on Windows");

        var @event = RecordedEvent();
        @event.Should().BeOfType<NewLoginDetectedV1>();
        var login = (NewLoginDetectedV1)@event;
        login.UserId.Should().Be(userId);
        login.SessionId.Should().Be(sessionId);
        login.DeviceSummary.Should().Be("Chrome 120 on Windows");
        login.EventName.Should().Be("identity.authentication.new-login-detected.v1");
        login.TenantId.Should().Be(tenantId);
        login.ActorId.Should().Be(DurableIntegrationEventActors.System);
        login.AggregateType.Should().Be("User");
        login.AggregateId.Should().Be(userId.ToString());
    }

    [Fact]
    public async Task RecordAsync_UsesPlatformTenantSentinel_WhenTenantIsMissing()
    {
        var sut = BuildSut();

        await sut.RecordAsync(Guid.NewGuid(), null, Guid.NewGuid(), null);

        RecordedEvent().TenantId.Should().Be(DurableIntegrationEventTenants.Platform);
    }

    [Fact]
    public async Task RecordAsync_BlankDeviceSummary_IsStoredAsEmpty()
    {
        var sut = BuildSut();

        await sut.RecordAsync(Guid.NewGuid(), null, Guid.NewGuid(), "   ");

        ((NewLoginDetectedV1)RecordedEvent()).DeviceSummary.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordAsync_SwallowsProducerFailure()
    {
        _producer
            .Setup(producer => producer.RecordAsync(It.IsAny<IDurableIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("outbox unavailable"));
        var sut = BuildSut();

        var act = () => sut.RecordAsync(Guid.NewGuid(), null, Guid.NewGuid(), "Chrome on Windows");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecordAsync_SwallowsMissingProducerRegistration()
    {
        var sut = new NewLoginAlertPublisher(
            new NullScopeFactory(),
            BuildConfiguration(enabled: true),
            NullLogger<NewLoginAlertPublisher>.Instance);

        var act = () => sut.RecordAsync(Guid.NewGuid(), null, Guid.NewGuid(), "Chrome on Windows");

        await act.Should().NotThrowAsync();
    }

    private NewLoginAlertPublisher BuildSut(bool enabled = true) =>
        new(new ProducerScopeFactory(_producer.Object), BuildConfiguration(enabled),
            NullLogger<NewLoginAlertPublisher>.Instance);

    private static IConfiguration BuildConfiguration(bool enabled) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:NewLoginNotifications:Enabled"] = enabled.ToString()
            })
            .Build();

    private IDurableIntegrationEvent RecordedEvent()
    {
        var recorded = new List<IDurableIntegrationEvent>();
        _producer.Verify(producer => producer.RecordAsync(Capture.In(recorded), It.IsAny<CancellationToken>()), Times.Once);
        return recorded.Single();
    }

    private sealed class ProducerScopeFactory(IDurableEventProducer producer) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new ProducerScope(producer);

        private sealed class ProducerScope(IDurableEventProducer producer) : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = new ProducerProvider(producer);

            public void Dispose()
            {
            }

            private sealed class ProducerProvider(IDurableEventProducer producer) : IServiceProvider
            {
                public object? GetService(Type serviceType) =>
                    serviceType == typeof(IDurableEventProducer) ? producer : null;
            }
        }
    }

    private sealed class NullScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new EmptyScope();

        private sealed class EmptyScope : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = new NullProvider();

            public void Dispose()
            {
            }

            private sealed class NullProvider : IServiceProvider
            {
                public object? GetService(Type serviceType) => null;
            }
        }
    }
}
