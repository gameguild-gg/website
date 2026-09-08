using FluentAssertions;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Analytics.UnitTests.Controllers;

public class AnalyticsControllerTests
{
    private readonly Mock<IAnalyticsDataWarehouseService> _warehouseService = new();

    [Fact]
    public async Task TrackEvent_ShouldReturnOk_WithTrackedEvent()
    {
        var sender = new Mock<ISender>();
        var command = new TrackAnalyticsEventCommand("page_view", "{\"path\":\"/\"}", Guid.NewGuid(), Guid.NewGuid());
        var expected = new AnalyticsEventDto(Guid.NewGuid(), command.EventName, command.PropertiesJson, command.UserId, null, DateTime.UtcNow);

        sender
            .Setup(s => s.Send(It.Is<TrackAnalyticsEventCommand>(request =>
                request.EventName == command.EventName &&
                request.PropertiesJson == command.PropertiesJson &&
                request.UserId == command.UserId &&
                request.TenantId == command.TenantId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = new AnalyticsController(sender.Object, _warehouseService.Object);

        var result = await controller.TrackEvent(command, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetTimeSeries_ShouldBuildQuery_AndReturnOk()
    {
        var sender = new Mock<ISender>();
        var eventName = "session_started";
        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        var tenantId = Guid.NewGuid();
        var expected = new List<TimeSeriesDataPointDto>
        {
            new(startDate, 12),
            new(endDate, 18)
        };

        sender
            .Setup(s => s.Send(It.Is<GetTimeSeriesQuery>(query =>
                query.EventName == eventName &&
                query.StartDate == startDate &&
                query.EndDate == endDate &&
                query.Granularity == TimeSeriesGranularity.Week &&
                query.TenantId == tenantId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = new AnalyticsController(sender.Object, _warehouseService.Object);

        var result = await controller.GetTimeSeries(eventName, startDate, endDate, TimeSeriesGranularity.Week, tenantId, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task CalculateKpi_ShouldBuildQuery_AndReturnOk()
    {
        var sender = new Mock<ISender>();
        var startDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc);
        var tenantId = Guid.NewGuid();
        var expected = new KpiResultDto("conversion_rate", 0.42, startDate, endDate, DateTime.UtcNow);

        sender
            .Setup(s => s.Send(It.Is<CalculateKpiQuery>(query =>
                query.KpiName == expected.KpiName &&
                query.StartDate == startDate &&
                query.EndDate == endDate &&
                query.TenantId == tenantId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = new AnalyticsController(sender.Object, _warehouseService.Object);

        var result = await controller.CalculateKpi(expected.KpiName, startDate, endDate, tenantId, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task AnalyzeFunnel_ShouldSendProvidedQuery_AndReturnOk()
    {
        var sender = new Mock<ISender>();
        var startDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc);
        var tenantId = Guid.NewGuid();
        var query = new AnalyzeFunnelQuery(["landing", "signup", "purchase"], startDate, endDate, tenantId);
        var expected = new FunnelAnalysisResultDto(
            [
                new FunnelStepDto("landing", 100, 0),
                new FunnelStepDto("signup", 55, 0.45),
                new FunnelStepDto("purchase", 20, 0.64)
            ],
            startDate,
            endDate,
            100);

        sender
            .Setup(s => s.Send(It.Is<AnalyzeFunnelQuery>(request =>
                request.Steps.SequenceEqual(query.Steps) &&
                request.StartDate == query.StartDate &&
                request.EndDate == query.EndDate &&
                request.TenantId == query.TenantId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = new AnalyticsController(sender.Object, _warehouseService.Object);

        var result = await controller.AnalyzeFunnel(query, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task RunWarehouse_ShouldMaterializeFacts_AndReturnOk()
    {
        var sender = new Mock<ISender>();
        var request = new AnalyticsWarehouseRunRequest(DateTime.UtcNow, 14, Guid.NewGuid());
        var expected = new AnalyticsWarehouseRunResponse(
            Guid.NewGuid(),
            request.TenantId,
            DateTime.UtcNow.AddDays(-13),
            DateTime.UtcNow,
            3,
            new Dictionary<string, int> { ["warehouse.property.inventory"] = 3 });

        _warehouseService
            .Setup(service => service.MaterializeAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = new AnalyticsController(new CommandHandlerSender(_warehouseService.Object), _warehouseService.Object);

        var result = await controller.RunWarehouse(request, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task ExportWarehouseFacts_ShouldReturnCsvFile()
    {
        var sender = new Mock<ISender>();
        var fact = new AnalyticsWarehouseFactDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "warehouse.property.inventory",
            DateTime.UtcNow,
            Guid.NewGuid(),
            "properties",
            2,
            500000m,
            new Dictionary<string, string?> { ["status"] = "Listed" });
        var facts = new List<AnalyticsWarehouseFactDto> { fact };
        const string csv = "id,tenantId,factName\n";

        _warehouseService
            .Setup(service => service.GetFactsAsync(It.IsAny<AnalyticsWarehouseExportRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(facts);
        _warehouseService
            .Setup(service => service.BuildCsv(facts))
            .Returns(csv);

        var controller = new AnalyticsController(sender.Object, _warehouseService.Object);

        var result = await controller.ExportWarehouseFacts(null, null, null, null, null, CancellationToken.None);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("text/csv");
        file.FileDownloadName.Should().StartWith("analytics-warehouse-");
        System.Text.Encoding.UTF8.GetString(file.FileContents).Should().Be(csv);
    }
}
