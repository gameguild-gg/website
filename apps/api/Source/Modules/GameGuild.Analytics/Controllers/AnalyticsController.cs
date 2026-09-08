using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GameGuild.CQRS;

namespace GameGuild.Analytics;

[Authorize]
[ApiController]
[Route("api/analytics")]
[Microsoft.AspNetCore.Http.Tags("analytics")]
public class AnalyticsController(
    ISender sender,
    IAnalyticsDataWarehouseService warehouseService) : ControllerBase
{
    [HttpPost("events")]
    public async Task<IActionResult> TrackEvent([FromBody] TrackAnalyticsEventCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return Ok(result);
    }

    [HttpGet("timeseries")]
    public async Task<IActionResult> GetTimeSeries(
        [FromQuery] string eventName,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] TimeSeriesGranularity granularity = TimeSeriesGranularity.Day,
        [FromQuery] Guid? tenantId = null,
        CancellationToken ct = default)
    {
        var query = new GetTimeSeriesQuery(eventName, startDate, endDate, granularity, tenantId);
        var result = await sender.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("kpi/{kpiName}")]
    public async Task<IActionResult> CalculateKpi(
        string kpiName,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] Guid? tenantId = null,
        CancellationToken ct = default)
    {
        var query = new CalculateKpiQuery(kpiName, startDate, endDate, tenantId);
        var result = await sender.Send(query, ct);
        return Ok(result);
    }

    [HttpPost("funnel")]
    public async Task<IActionResult> AnalyzeFunnel([FromBody] AnalyzeFunnelQuery query, CancellationToken ct)
    {
        var result = await sender.Send(query, ct);
        return Ok(result);
    }

    [HttpPost("warehouse/run")]
    public async Task<ActionResult<AnalyticsWarehouseRunResponse>> RunWarehouse(
        [FromBody] AnalyticsWarehouseRunRequest request,
        CancellationToken ct)
    {
        return Ok(await sender.Send(new RunAnalyticsWarehouseCommand(request), ct));
    }

    [HttpGet("warehouse/facts")]
    public async Task<ActionResult<IReadOnlyList<AnalyticsWarehouseFactDto>>> GetWarehouseFacts(
        [FromQuery] DateTime? startUtc,
        [FromQuery] DateTime? endUtc,
        [FromQuery] Guid? tenantId,
        [FromQuery] string? factName,
        [FromQuery] int? take,
        CancellationToken ct)
    {
        var facts = await warehouseService.GetFactsAsync(
            new AnalyticsWarehouseExportRequest(startUtc, endUtc, tenantId, factName, take),
            ct);

        return Ok(facts);
    }

    [HttpGet("warehouse/export")]
    public async Task<IActionResult> ExportWarehouseFacts(
        [FromQuery] DateTime? startUtc,
        [FromQuery] DateTime? endUtc,
        [FromQuery] Guid? tenantId,
        [FromQuery] string? factName,
        [FromQuery] int? take,
        CancellationToken ct)
    {
        var facts = await warehouseService.GetFactsAsync(
            new AnalyticsWarehouseExportRequest(startUtc, endUtc, tenantId, factName, take),
            ct);

        var csv = warehouseService.BuildCsv(facts);
        return File(
            Encoding.UTF8.GetBytes(csv),
            "text/csv",
            $"analytics-warehouse-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }
}
