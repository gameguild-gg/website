using Asp.Versioning;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Revenue event auditing API (issue #404): external statement reconciliation with
///     discrepancy detection, automated anomaly alerts, compliance reporting, historical
///     trend analysis and CSV/JSON exports for accounting and ERP systems.
///     Queries are tenant-scoped for non-admin actors; mutations require the SystemAdmin role.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/billing/revenue/auditing")]
[Microsoft.AspNetCore.Http.Tags("billing/revenue/auditing")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Api)]
public sealed class RevenueAuditingController(
    ISender sender,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    /// <summary>
    ///     Run a reconciliation comparing an external accounting/ERP statement against internal
    ///     revenue events for an inclusive period. Requires the SystemAdmin role.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.ExpensiveOperations)]
    [EndpointSummary("Run a revenue reconciliation against an external statement")]
    [EndpointDescription(
        "Compares external accounting/ERP statement lines against internal revenue events for an inclusive period, records an immutable reconciliation run and persists every discrepancy (missing internal/external references, amount and currency mismatches, duplicate external references). When no inline lines are supplied, lines are read from the configured external statement source.")]
    [ProducesResponseType<RevenueReconciliationRun>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RunReconciliation([FromBody] RunRevenueReconciliationRequest request, CancellationToken ct)
    {
        var adminGuard = RequireSystemAdmin("run a revenue reconciliation");
        if (adminGuard is not null)
        {
            return adminGuard;
        }

        var run = await sender.Send(
            new Commands.RunRevenueReconciliation.RunRevenueReconciliationCommand(
                TenantId: request.TenantId,
                Source: request.Source,
                PeriodStartUtc: request.PeriodStartUtc.UtcDateTime,
                PeriodEndUtc: request.PeriodEndUtc.UtcDateTime,
                Lines: request.Lines?
                    .Select(line => new ExternalRevenueStatementLine(
                        line.ReferenceId,
                        line.Amount,
                        line.Currency,
                        line.OccurredAtUtc.UtcDateTime))
                    .ToList(),
                ExternalStatementId: request.ExternalStatementId),
            ct).ConfigureAwait(false);

        return CreatedAtRoute(nameof(GetRunById), new { runId = run.Id, version = HttpContext.GetRequestedApiVersion()?.ToString() }, run);
    }

    /// <summary>
    ///     List reconciliation runs, newest first. Tenant-scoped for non-admin actors.
    /// </summary>
    [HttpGet]
    [EndpointSummary("List revenue reconciliation runs")]
    [EndpointDescription(
        "Returns a paged list of revenue reconciliation runs, newest first. Non-admin actors only see runs of their own tenant.")]
    [ProducesResponseType<PagedResult<RevenueReconciliationRun>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRuns(
        [FromQuery] Guid? tenantId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        NormalizePaging(ref skip, ref take);
        var result = await sender.Send(new Queries.RevenueAuditing.GetRevenueReconciliationRunsQuery(tenantId, skip, take), ct).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Get one reconciliation run with its counters and summary.
    /// </summary>
    [HttpGet("runs/{runId:guid}", Name = nameof(GetRunById))]
    [EndpointSummary("Get a revenue reconciliation run")]
    [EndpointDescription(
        "Returns one reconciliation run, including matched/discrepancy counters and the machine-readable summary captured at completion.")]
    [ProducesResponseType<RevenueReconciliationRun>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRunById(Guid runId, CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(new Queries.RevenueAuditing.GetRevenueReconciliationRunByIdQuery(runId), ct).ConfigureAwait(false);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    ///     List the discrepancies recorded by a reconciliation run.
    /// </summary>
    [HttpGet("runs/{runId:guid}/discrepancies")]
    [EndpointSummary("List discrepancies of a revenue reconciliation run")]
    [EndpointDescription(
        "Returns a paged list of the discrepancies recorded by a reconciliation run, optionally filtered by kind (MissingInternal, MissingExternal, AmountMismatch, CurrencyMismatch, DuplicateExternalReference).")]
    [ProducesResponseType<PagedResult<RevenueReconciliationDiscrepancy>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRunDiscrepancies(
        Guid runId,
        [FromQuery] RevenueDiscrepancyKind? kind = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        NormalizePaging(ref skip, ref take);
        try
        {
            var result = await sender.Send(new Queries.RevenueAuditing.GetRevenueReconciliationDiscrepanciesQuery(runId, kind, skip, take), ct).ConfigureAwait(false);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    ///     Trigger anomaly detection for the trailing days and persist alerts. Requires the
    ///     SystemAdmin role; the periodic worker performs the same pass automatically when
    ///     <c>RevenueAuditing:WorkerEnabled</c> is set.
    /// </summary>
    [HttpPost("anomalies/detect")]
    [EnableRateLimiting(RateLimitPolicies.ExpensiveOperations)]
    [EndpointSummary("Run revenue anomaly detection")]
    [EndpointDescription(
        "Evaluates daily net revenue for the trailing days against the configured baseline window and persists anomaly alerts (spikes/drops at or above the z-score threshold). Detection is idempotent per kind and day.")]
    [ProducesResponseType<AnomalyDetectionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DetectAnomalies([FromBody] DetectRevenueAnomaliesRequest? request, CancellationToken ct)
    {
        var adminGuard = RequireSystemAdmin("run revenue anomaly detection");
        if (adminGuard is not null)
        {
            return adminGuard;
        }

        var created = await sender.Send(
            new Commands.RunRevenueAnomalyDetection.RunRevenueAnomalyDetectionCommand(
                TenantId: request?.TenantId,
                EvaluationDateUtc: request?.EvaluationDateUtc?.UtcDateTime),
            ct).ConfigureAwait(false);

        return Ok(new AnomalyDetectionResult(created));
    }

    /// <summary>
    ///     List revenue anomaly alerts, newest detection first. Tenant-scoped for non-admin actors.
    /// </summary>
    [HttpGet("anomaly-alerts")]
    [EndpointSummary("List revenue anomaly alerts")]
    [EndpointDescription(
        "Returns a paged list of revenue anomaly alerts, newest detection first, optionally filtered by status (Open, Acknowledged). Non-admin actors only see alerts of their own tenant.")]
    [ProducesResponseType<PagedResult<RevenueAnomalyAlert>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnomalyAlerts(
        [FromQuery] Guid? tenantId,
        [FromQuery] RevenueAnomalyStatus? status = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        NormalizePaging(ref skip, ref take);
        var result = await sender.Send(new Queries.RevenueAuditing.GetRevenueAnomalyAlertsQuery(tenantId, status, skip, take), ct).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Acknowledge an open anomaly alert, recording the reviewing operator. Requires the
    ///     SystemAdmin role.
    /// </summary>
    [HttpPost("anomaly-alerts/{alertId:guid}/acknowledge")]
    [EnableRateLimiting(RateLimitPolicies.ExpensiveOperations)]
    [EndpointSummary("Acknowledge a revenue anomaly alert")]
    [EndpointDescription(
        "Marks an open revenue anomaly alert as acknowledged, recording the reviewing operator and optional notes. The acting identity is taken from the authenticated actor, never from the request body.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcknowledgeAlert(Guid alertId, [FromBody] AcknowledgeRevenueAnomalyAlertRequest? request, CancellationToken ct)
    {
        var adminGuard = RequireSystemAdmin("acknowledge a revenue anomaly alert");
        if (adminGuard is not null)
        {
            return adminGuard;
        }

        try
        {
            await sender.Send(
                new Commands.AcknowledgeRevenueAnomalyAlert.AcknowledgeRevenueAnomalyAlertCommand(alertId, request?.Notes),
                ct).ConfigureAwait(false);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    ///     Compliance report for an inclusive period: totals by event type, source and status,
    ///     uncounted events, reconciliation coverage and an attestation statement.
    /// </summary>
    [HttpGet("compliance-report")]
    [EndpointSummary("Get the revenue compliance report for a period")]
    [EndpointDescription(
        "Builds a compliance-grade summary for the inclusive period: revenue totals grouped by event type, source and processing status, uncounted (pending/failed) events, reconciliation coverage across the period, and an attestation statement suitable for filings.")]
    [ProducesResponseType<RevenueComplianceReport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetComplianceReport(
        [FromQuery] DateTimeOffset fromUtc,
        [FromQuery] DateTimeOffset toUtc,
        [FromQuery] Guid? tenantId = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new Queries.RevenueAuditingReports.GetRevenueComplianceReportQuery(fromUtc.UtcDateTime, toUtc.UtcDateTime, tenantId),
            ct).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Historical daily net-revenue trend for an inclusive period, with zero-activity days included.
    /// </summary>
    [HttpGet("trends")]
    [EndpointSummary("Get the daily revenue trend for a period")]
    [EndpointDescription(
        "Returns one net-revenue point per UTC day in the inclusive period (credit total, debit total, net total and event count), including days without activity, plus range totals.")]
    [ProducesResponseType<RevenueTrendReport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTrendReport(
        [FromQuery] DateTimeOffset fromUtc,
        [FromQuery] DateTimeOffset toUtc,
        [FromQuery] Guid? tenantId = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new Queries.RevenueAuditingReports.GetRevenueTrendReportQuery(fromUtc.UtcDateTime, toUtc.UtcDateTime, tenantId),
            ct).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Export the audit report for a period as CSV (RFC 4180) or JSON for external
    ///     accounting and ERP systems.
    /// </summary>
    [HttpGet("export")]
    [EndpointSummary("Export the revenue audit report")]
    [EndpointDescription(
        "Serializes the compliance summary and daily trend for the inclusive period as RFC 4180 CSV or JSON, ready for delivery to external accounting and ERP systems.")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportReport(
        [FromQuery] DateTimeOffset fromUtc,
        [FromQuery] DateTimeOffset toUtc,
        [FromQuery] string format = "csv",
        [FromQuery] Guid? tenantId = null,
        CancellationToken ct = default)
    {
        var export = await sender.Send(
            new Queries.RevenueAuditingReports.ExportRevenueReportQuery(fromUtc.UtcDateTime, toUtc.UtcDateTime, format, tenantId),
            ct).ConfigureAwait(false);

        return File(System.Text.Encoding.UTF8.GetBytes(export.Content), export.ContentType, export.FileName);
    }

    private IActionResult? RequireSystemAdmin(string operation)
        => actorContextAccessor.ActorContext.IsSystemAdmin
            ? null
            : new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = $"Only system administrators may {operation}."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };

    private static void NormalizePaging(ref int skip, ref int take)
    {
        if (skip < 0)
        {
            skip = 0;
        }

        if (take < 1)
        {
            take = 20;
        }

        if (take > 100)
        {
            take = 100;
        }
    }

    /// <summary>Request body for <see cref="RunReconciliation" />.</summary>
    public sealed record RunRevenueReconciliationRequest(
        Guid? TenantId,
        string Source,
        DateTimeOffset PeriodStartUtc,
        DateTimeOffset PeriodEndUtc,
        IReadOnlyList<StatementLineRequest>? Lines,
        string? ExternalStatementId = null);

    /// <summary>One external statement line supplied inline.</summary>
    public sealed record StatementLineRequest(
        string ReferenceId,
        decimal Amount,
        string Currency,
        DateTimeOffset OccurredAtUtc);

    /// <summary>Request body for <see cref="DetectAnomalies" />.</summary>
    public sealed record DetectRevenueAnomaliesRequest(Guid? TenantId, DateTimeOffset? EvaluationDateUtc);

    /// <summary>Request body for <see cref="AcknowledgeAlert" />.</summary>
    public sealed record AcknowledgeRevenueAnomalyAlertRequest(string? Notes);

    /// <summary>Result of a manual anomaly detection pass.</summary>
    public sealed record AnomalyDetectionResult(int AlertsCreated);
}
