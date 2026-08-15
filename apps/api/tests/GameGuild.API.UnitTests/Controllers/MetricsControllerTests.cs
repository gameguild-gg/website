using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using GameGuild.API.Controllers;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

public class MetricsControllerTests
{
    private readonly Mock<ILogger<MetricsController>> _loggerMock = new();

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        var act = () => new MetricsController(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetMetrics_ShouldReturnPrometheusTextWithUnixLineEndings()
    {
        var controller = new MetricsController(_loggerMock.Object);

        var result = controller.GetMetrics() as ContentResult;

        result.Should().NotBeNull();
        result!.ContentType.Should().Be("text/plain; version=0.0.4; charset=utf-8");
        result.Content.Should().Contain("# HELP process_cpu_seconds_total");
        result.Content.Should().Contain("# TYPE process_cpu_seconds_total counter\n");
        result.Content.Should().Contain("process_virtual_memory_bytes");
        result.Content.Should().Contain("process_working_set_bytes");
        result.Content.Should().Contain("process_start_time_seconds");
        result.Content.Should().Contain("process_uptime_seconds");
        result.Content.Should().Contain("process_num_threads");
        result.Content.Should().Contain("dotnet_gc_collections_total");
        result.Content.Should().Contain("generation=\"0\"");
        result.Content.Should().Contain("generation=\"1\"");
        result.Content.Should().Contain("generation=\"2\"");
        result.Content.Should().Contain("app_info");
        result.Content.Should().NotContain("\r");
    }
}
