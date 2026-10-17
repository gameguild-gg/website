using FluentAssertions;
using GameGuild.Identity.Authorization;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.GatewayMetrics;

/// <summary>
///     The per-gateway metrics endpoint's query layer (issue #413 follow-up): window
///     aggregates merged with circuit-breaker snapshots, the payments:admin permission
///     contract, and the empty-state shape.
/// </summary>
public class GetGatewayMetricsQueryTests
{
    private readonly Mock<IGatewayMetricsService> _gatewayMetrics = new();
    private readonly Mock<IGatewayCircuitBreaker> _circuitBreaker = new();

    [Fact]
    public async Task Handler_MergesWindowAggregates_WithCircuitState()
    {
        var window = new GatewayWindowMetrics(
            "stripe",
            "24h",
            SystemClock.UtcNow.AddHours(-24),
            Attempts: 10,
            Succeeded: 8,
            Failed: 2,
            Unresolved: 0,
            SuccessRate: 0.8,
            AverageProcessingLatencyMs: 1200.5);
        var circuit = new GatewayCircuitSnapshot(
            "stripe",
            GatewayCircuitState.Closed,
            ConsecutiveFailures: 1,
            OpenedAtUtc: null,
            FailureThreshold: 5);
        _gatewayMetrics
            .Setup(service => service.GetWindowMetricsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { window });
        _circuitBreaker
            .Setup(breaker => breaker.GetAllSnapshots())
            .Returns(new[] { circuit });

        var report = await new GetGatewayMetricsQueryHandler(_gatewayMetrics.Object, _circuitBreaker.Object)
            .Handle(new GetGatewayMetricsQuery(), CancellationToken.None);

        report.Windows.Should().ContainSingle().Which.Should().BeEquivalentTo(window);
        report.CircuitBreakers.Should().ContainSingle().Which.Should().BeEquivalentTo(circuit);
        report.GeneratedAtUtc.Should().BeOnOrAfter(SystemClock.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public async Task Handler_ForwardsTenantScope_ToTheWindowAggregates()
    {
        var tenantId = Guid.NewGuid();
        _gatewayMetrics
            .Setup(service => service.GetWindowMetricsAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GatewayWindowMetrics>());
        _circuitBreaker
            .Setup(breaker => breaker.GetAllSnapshots())
            .Returns(Array.Empty<GatewayCircuitSnapshot>());

        await new GetGatewayMetricsQueryHandler(_gatewayMetrics.Object, _circuitBreaker.Object)
            .Handle(new GetGatewayMetricsQuery(tenantId), CancellationToken.None);

        _gatewayMetrics.Verify(
            service => service.GetWindowMetricsAsync(tenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EmptyState_ReturnsEmptyWindows_AndStillReportsCircuits()
    {
        _gatewayMetrics
            .Setup(service => service.GetWindowMetricsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GatewayWindowMetrics>());
        var openCircuit = new GatewayCircuitSnapshot(
            "paypal",
            GatewayCircuitState.Open,
            ConsecutiveFailures: 5,
            OpenedAtUtc: SystemClock.UtcNow.AddMinutes(-3),
            FailureThreshold: 5);
        _circuitBreaker
            .Setup(breaker => breaker.GetAllSnapshots())
            .Returns(new[] { openCircuit });

        var report = await new GetGatewayMetricsQueryHandler(_gatewayMetrics.Object, _circuitBreaker.Object)
            .Handle(new GetGatewayMetricsQuery(), CancellationToken.None);

        report.Windows.Should().BeEmpty();
        report.CircuitBreakers.Should().ContainSingle().Which.State.Should().Be(GatewayCircuitState.Open);
    }

    [Fact]
    public void Query_IsGuardedByThePaymentsAdminPermission()
    {
        var attribute = typeof(GetGatewayMetricsQuery)
            .GetCustomAttributes(typeof(AuthorizeRequestAttribute), inherit: true)
            .Cast<AuthorizeRequestAttribute>()
            .Single();

        attribute.Permission.Should().Be(PaymentsPermission.Keys.Admin);
    }

    [Fact]
    public void PaymentsAdminPermission_IsRegisteredInThePermissionRegistry()
    {
        PaymentsPermission.Keys.Admin.Should().Be("payments:admin");
        PermissionRegistry.GetByKey(PaymentsPermission.Keys.Admin).Should().NotBeNull(
            "the payments:admin permission must stay registered so the guard fails closed on a known key");
    }
}
