using FluentAssertions;
using GameGuild.API.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

/// <summary>
/// Unit tests for HealthController
/// </summary>
public class HealthControllerTests
{
    private readonly Mock<HealthCheckService> _healthCheckServiceMock;
    private readonly Mock<ILogger<HealthController>> _loggerMock;
    private readonly HealthController _controller;

    public HealthControllerTests()
    {
        _healthCheckServiceMock = new Mock<HealthCheckService>();
        _loggerMock = new Mock<ILogger<HealthController>>();
        _controller = new HealthController(_healthCheckServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void Constructor_WithNullHealthCheckService_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new HealthController(null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("healthCheckService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new HealthController(_healthCheckServiceMock.Object, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task GetHealth_WhenAllChecksHealthy_ShouldReturn200WithHealthyStatus()
    {
        // Arrange
        var healthReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["Database"] = new HealthReportEntry(
                    HealthStatus.Healthy,
                    "Database is healthy",
                    TimeSpan.FromMilliseconds(10),
                    null,
                    null)
            },
            TimeSpan.FromMilliseconds(10)
        );

        _healthCheckServiceMock
            .Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthReport);

        // Act
        var result = await _controller.GetHealth();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        
        var response = okResult.Value.Should().BeOfType<HealthinessResponse>().Subject;
        response.Status.Should().Be("Healthy");
        response.Checks.Should().ContainKey("Database");
    }

    [Fact]
    public async Task GetHealth_WhenCheckUnhealthy_ShouldReturn503WithUnhealthyStatus()
    {
        // Arrange
        var healthReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["Database"] = new HealthReportEntry(
                    HealthStatus.Unhealthy,
                    "Database connection failed",
                    TimeSpan.FromMilliseconds(100),
                    new Exception("Connection timeout"),
                    null)
            },
            TimeSpan.FromMilliseconds(100)
        );

        _healthCheckServiceMock
            .Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthReport);

        // Act
        var result = await _controller.GetHealth();

        // Assert
        result.Should().NotBeNull();
        var serviceUnavailableResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        serviceUnavailableResult.StatusCode.Should().Be(503);
        
        var response = serviceUnavailableResult.Value.Should().BeOfType<HealthinessResponse>().Subject;
        response.Status.Should().Be("Unhealthy");
    }

    [Fact]
    public async Task GetHealth_WhenCheckThrowsException_ShouldReturn503WithErrorMessage()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Health check failed");
        _healthCheckServiceMock
            .Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        var result = await _controller.GetHealth();

        // Assert
        result.Should().NotBeNull();
        var serviceUnavailableResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        serviceUnavailableResult.StatusCode.Should().Be(503);
        
        var response = serviceUnavailableResult.Value.Should().BeOfType<HealthinessResponse>().Subject;
        response.Status.Should().Be("Unhealthy");
        response.Error.Should().Be("Health check failed");
    }

    [Fact]
    public async Task GetReadiness_ShouldReturnOkWithReadyStatus()
    {
        // Arrange
        var healthReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>(),
            TimeSpan.FromMilliseconds(5)
        );

        _healthCheckServiceMock
            .Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthReport);

        // Act
        var result = await _controller.GetReadiness();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        
        var response = okResult.Value.Should().BeOfType<ReadinessResponse>().Subject;
        response.Status.Should().Be("Healthy");
        response.Ready.Should().BeTrue();
    }

    [Fact]
    public async Task GetReadiness_WhenAServiceIsUnhealthy_ShouldReturnServiceUnavailable()
    {
        var healthReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["Database"] = new HealthReportEntry(
                    HealthStatus.Unhealthy,
                    "Database unavailable",
                    TimeSpan.FromMilliseconds(10),
                    new InvalidOperationException("offline"),
                    null)
            },
            TimeSpan.FromMilliseconds(10));

        _healthCheckServiceMock
            .Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthReport);

        var result = await _controller.GetReadiness();

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        var response = objectResult.Value.Should().BeOfType<ReadinessResponse>().Subject;
        response.Ready.Should().BeFalse();
        response.Services.Should().Contain("Database", false);
    }

    [Fact]
    public async Task HealthEndpoints_WhenDependencyIsDegraded_ShouldRemainAvailable()
    {
        var healthReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["Database"] = new HealthReportEntry(
                    HealthStatus.Degraded,
                    "Pending migrations",
                    TimeSpan.FromMilliseconds(10),
                    null,
                    null)
            },
            TimeSpan.FromMilliseconds(10));

        _healthCheckServiceMock
            .Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthReport);

        var health = await _controller.GetHealth();
        var readiness = await _controller.GetReadiness();
        var dependencies = await _controller.GetDependencyHealth();

        health.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(200);
        readiness.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(200);
        readiness.Result.Should().BeOfType<ObjectResult>().Which.Value
            .Should().BeOfType<ReadinessResponse>().Which.Ready.Should().BeTrue();
        dependencies.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetDependencyHealth_WhenDependencyIsUnhealthy_ShouldReturnServiceUnavailable()
    {
        var healthReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["Database"] = new HealthReportEntry(
                    HealthStatus.Unhealthy,
                    "Database unavailable",
                    TimeSpan.FromMilliseconds(10),
                    new InvalidOperationException("offline"),
                    null)
            },
            TimeSpan.FromMilliseconds(10));

        _healthCheckServiceMock
            .Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthReport);

        var result = await _controller.GetDependencyHealth();

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        var dependency = objectResult.Value.Should().BeOfType<DependencyHealthResponse>().Subject
            .Dependencies.Should().ContainSingle().Subject;
        dependency.Tags.Should().BeEmpty();
        dependency.Data.Should().BeEmpty();
        dependency.Exception.Should().Be("offline");
    }

    [Fact]
    public async Task GetDependencyHealth_ShouldFilterAndMapDependencyDetails()
    {
        Func<HealthCheckRegistration, bool>? capturedPredicate = null;
        var healthReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["Database"] = new HealthReportEntry(
                    HealthStatus.Healthy,
                    "Database ready",
                    TimeSpan.FromMilliseconds(5),
                    null,
                    new Dictionary<string, object>
                    {
                        ["provider"] = "postgres",
                        ["optional"] = null!
                    },
                    ["dependency", "ready"])
            },
            TimeSpan.FromMilliseconds(5));

        _healthCheckServiceMock
            .Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .Callback<Func<HealthCheckRegistration, bool>, CancellationToken>((predicate, _) =>
                capturedPredicate = predicate)
            .ReturnsAsync(healthReport);

        var result = await _controller.GetDependencyHealth();

        capturedPredicate.Should().NotBeNull();
        capturedPredicate!(CreateRegistration("dependency", "dependency")).Should().BeTrue();
        capturedPredicate(CreateRegistration("ready", "ready")).Should().BeTrue();
        capturedPredicate(CreateRegistration("other", "other")).Should().BeFalse();

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        var response = objectResult.Value.Should().BeOfType<DependencyHealthResponse>().Subject;
        response.HealthyCount.Should().Be(1);
        response.UnhealthyCount.Should().Be(0);
        var dependency = response.Dependencies.Should().ContainSingle().Subject;
        dependency.Tags.Should().Equal("dependency", "ready");
        dependency.Data.Should().Contain("provider", "postgres");
        dependency.Data.Should().Contain("optional", string.Empty);
    }

    [Fact]
    public void OptionalHealthErrors_ShouldRoundTrip()
    {
        new ReadinessResponse { Error = "readiness failed" }.Error.Should().Be("readiness failed");
        new DependencyHealthResponse { Error = "dependency failed" }.Error.Should().Be("dependency failed");
    }

    [Fact]
    public Task GetLiveness_ShouldReturnOkWithAliveStatus()
    {
        // Act
        var result = _controller.GetLiveness();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        
        var response = okResult.Value.Should().BeOfType<LivenessResponse>().Subject;
        response.Status.Should().Be("Healthy");
        response.Alive.Should().BeTrue();
        
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetHealth_ShouldIncludeTimestamp()
    {
        // Arrange
        var beforeCall = DateTime.UtcNow;
        var healthReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>(),
            TimeSpan.FromMilliseconds(5)
        );

        _healthCheckServiceMock
            .Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthReport);

        // Act
        var result = await _controller.GetHealth();
        var afterCall = DateTime.UtcNow;

        // Assert
        var okResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<HealthinessResponse>().Subject;
        
        response.Timestamp.Should().BeOnOrAfter(beforeCall);
        response.Timestamp.Should().BeOnOrBefore(afterCall);
    }

    private static HealthCheckRegistration CreateRegistration(string name, params string[] tags) =>
        new(name, Mock.Of<IHealthCheck>(), failureStatus: null, tags);
}
