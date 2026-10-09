using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public class SuspiciousLoginAlertPublisherTests
{
    private readonly Mock<IDurableEventProducer> _producer = new();

    [Fact]
    public async Task RecordAsync_DoesNotRecord_WhenNotificationsAreDisabled()
    {
        var sut = BuildSut(enabled: false);

        await sut.RecordAsync(Guid.NewGuid(), null, SecurityAlertKinds.BruteForceDetected, RiskLevel.Critical, 90);

        _producer.Verify(producer => producer.RecordAsync(It.IsAny<IDurableIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordAsync_DoesNotRecord_WhenUserIdIsEmpty()
    {
        var sut = BuildSut();

        await sut.RecordAsync(Guid.Empty, null, SecurityAlertKinds.BruteForceDetected, RiskLevel.Critical, 90);

        _producer.Verify(producer => producer.RecordAsync(It.IsAny<IDurableIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordAsync_DoesNotRecord_WhenAlertKindIsBlank()
    {
        var sut = BuildSut();

        await sut.RecordAsync(Guid.NewGuid(), null, "  ", RiskLevel.Critical, 90);

        _producer.Verify(producer => producer.RecordAsync(It.IsAny<IDurableIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordAsync_RecordsNamedSignal_BelowHighAssessment_WithDefaultMinimum()
    {
        // Brute force alone assesses Medium; the named signals are at least High severity,
        // so the default High minimum must keep notifying.
        var sut = BuildSut();
        var userId = Guid.NewGuid();

        await sut.RecordAsync(userId, null, SecurityAlertKinds.BruteForceDetected, RiskLevel.Medium, 40);

        var @event = RecordedEvent();
        @event.Should().BeOfType<SuspiciousLoginDetectedV1>();
        var alert = (SuspiciousLoginDetectedV1)@event;
        alert.UserId.Should().Be(userId);
        alert.AlertKind.Should().Be(SecurityAlertKinds.BruteForceDetected);
        alert.RiskLevel.Should().Be(nameof(RiskLevel.Medium));
        alert.RiskScore.Should().Be(40);
        alert.EventName.Should().Be("identity.authentication.suspicious-login-detected.v1");
    }

    [Fact]
    public async Task RecordAsync_DoesNotRecord_WhenMinimumRaisedToCriticalAndAssessmentStaysHigh()
    {
        var sut = BuildSut(minimumRiskLevel: "Critical");

        await sut.RecordAsync(Guid.NewGuid(), null, SecurityAlertKinds.LoginStepUpRequired, RiskLevel.High, 60);

        _producer.Verify(producer => producer.RecordAsync(It.IsAny<IDurableIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordAsync_Records_WhenAssessmentClearsRaisedMinimum()
    {
        var sut = BuildSut(minimumRiskLevel: "Critical");
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await sut.RecordAsync(userId, tenantId, SecurityAlertKinds.ImpossibleTravel, RiskLevel.Critical, 85);

        var alert = (SuspiciousLoginDetectedV1)RecordedEvent();
        alert.TenantId.Should().Be(tenantId);
        alert.RiskLevel.Should().Be(nameof(RiskLevel.Critical));
        alert.ActorId.Should().Be(DurableIntegrationEventActors.System);
        alert.AggregateType.Should().Be("User");
        alert.AggregateId.Should().Be(userId.ToString());
    }

    [Fact]
    public async Task RecordAsync_UsesPlatformTenantSentinel_WhenTenantIsMissing()
    {
        var sut = BuildSut();

        await sut.RecordAsync(Guid.NewGuid(), null, SecurityAlertKinds.BruteForceDetected, RiskLevel.High, 60);

        RecordedEvent().TenantId.Should().Be(DurableIntegrationEventTenants.Platform);
    }

    [Fact]
    public async Task RecordAsync_SwallowsProducerFailure()
    {
        _producer
            .Setup(producer => producer.RecordAsync(It.IsAny<IDurableIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("outbox unavailable"));
        var sut = BuildSut();

        var act = () => sut.RecordAsync(Guid.NewGuid(), null, SecurityAlertKinds.BruteForceDetected, RiskLevel.High, 60);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecordAsync_SwallowsMissingProducerRegistration()
    {
        // Hosts without the durable transport must degrade to a logged no-op, not break sign-in.
        var sut = new SuspiciousLoginAlertPublisher(
            new NullProducerScopeFactory(),
            BuildConfiguration(enabled: true, minimumRiskLevel: "High"),
            NullLogger<SuspiciousLoginAlertPublisher>.Instance);

        var act = () => sut.RecordAsync(Guid.NewGuid(), null, SecurityAlertKinds.BruteForceDetected, RiskLevel.High, 60);

        await act.Should().NotThrowAsync();
    }

    private SuspiciousLoginAlertPublisher BuildSut(bool enabled = true, string minimumRiskLevel = "High") =>
        new(new ProducerScopeFactory(_producer.Object), BuildConfiguration(enabled, minimumRiskLevel),
            NullLogger<SuspiciousLoginAlertPublisher>.Instance);

    private static IConfiguration BuildConfiguration(bool enabled, string minimumRiskLevel) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:SecurityNotifications:Enabled"] = enabled.ToString(),
                ["Authentication:SecurityNotifications:MinimumRiskLevel"] = minimumRiskLevel
            })
            .Build();

    private IDurableIntegrationEvent RecordedEvent()
    {
        var recorded = new List<IDurableIntegrationEvent>();
        _producer.Verify(producer => producer.RecordAsync(Capture.In(recorded), It.IsAny<CancellationToken>()), Times.Once);
        return recorded.Single();
    }

    /// <summary>
    ///     The publisher records through an independent scope so the alert outbox entry commits
    ///     outside the (rolled-back) sign-in transaction; this fake supplies that scope.
    /// </summary>
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

    private sealed class NullProducerScopeFactory : IServiceScopeFactory
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
