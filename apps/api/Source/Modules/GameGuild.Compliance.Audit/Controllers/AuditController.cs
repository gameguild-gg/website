using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Asp.Versioning;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using GameGuild.CQRS;
using Microsoft.Net.Http.Headers;

namespace GameGuild.Compliance.Audit;

/// <summary>
/// Controller for audit log management (admin only)
/// </summary>
[Microsoft.AspNetCore.Http.Tags("compliance/audit")]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/admin/audit-logs")]
[Authorize(Policy = Policies.SystemAdmin)]
public class AuditController(
    IAuditService auditService,
    IActorContextAccessor actorContextAccessor,
    ILogger<AuditController> _logger,
    ISender sender,
    IAuditExportProgressTracker exportProgressTracker,
    IAuditExportWebhookNotifier exportWebhookNotifier,
    IScheduledAuditExportService scheduledExportService) : BaseApiController
{
    /// <summary>
    /// Gets the current user ID from the actor context
    /// </summary>
    protected Guid? GetCurrentUserId()
    {
        return actorContextAccessor.ActorContext.SubjectIdAsGuid;
    }

    /// <summary>
    /// Get audit logs with filtering and pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<AuditLogResponse>> GetAuditLogs([FromQuery] AuditLogQueryRequest request)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue) throw new UnauthorizedAccessException("User not authenticated");

        _logger.LogInformation("Admin {AdminUserId} querying audit logs: ActionType={ActionType}, RiskLevel={RiskLevel}", adminUserId.Value, LogRedaction.Sanitize(request.ActionType), request.RiskLevel);

        // Log admin access to audit logs
        await auditService.LogAdminActionAsync(adminUserId.Value, "ViewAuditLogs", "Admin accessed audit logs", new { Filters = request, RequestedBy = adminUserId.Value }).ConfigureAwait(false);

        var query = new AuditLogQuery
        {
            UserId = request.UserId,
            TenantId = request.TenantId,
            ActionType = request.ActionType,
            ResourceType = request.ResourceType,
            Category = request.Category,
            RiskLevel = request.RiskLevel,
            Success = request.Success,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IpAddress = request.IpAddress,
            Skip = request.Skip,
            Take = Math.Min(request.Take, 1000) // Cap at 1000 records
        };

        var logs = await auditService.GetAuditLogsAsync(query).ConfigureAwait(false);
        var totalCount = await auditService.GetAuditLogCountAsync(query).ConfigureAwait(false);

        var response = new AuditLogResponse { Logs = logs.Select(MapToDto).ToList(), TotalCount = totalCount, Skip = request.Skip, Take = request.Take };

        return Ok(response);
    }

    /// <summary>
    /// Searches audit records over an explicit or relative date range and returns matching events with a time histogram.
    /// </summary>
    /// <remarks>
    /// Use <c>start</c>/<c>end</c> with ISO-8601 timestamps, Unix seconds or milliseconds, or relative expressions
    /// such as <c>now-7d</c> and <c>now</c>. Alternatively use <c>period=last24h|last7d|last30d|today|thisWeek|thisMonth</c>.
    /// Offset-free values are interpreted in <c>timeZoneId</c> (UTC by default). Date-only end values include that
    /// calendar day. Hourly or daily histogram buckets include both UTC and local timestamps.
    /// </remarks>
    [HttpGet("search/by-date-range")]
    [ProducesResponseType(typeof(AuditDateRangeSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuditDateRangeSearchResponse>> SearchAuditLogsByDateRange(
        [FromQuery] AuditDateRangeSearchRequest request,
        CancellationToken cancellationToken)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue) { throw new UnauthorizedAccessException("User not authenticated"); }

        var requestValidation = ValidateRequest(request);
        if (requestValidation is not null) { return requestValidation; }

        var resolution = AuditDateRangeResolver.Resolve(
            request.Start,
            request.End,
            request.Period,
            request.TimeZoneId,
            SystemClock.UtcNow);
        if (!resolution.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(resolution.Errors.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal))
            {
                Status = StatusCodes.Status400BadRequest
            });
        }

        var range = resolution.Range!;
        if (request.BucketSize.HasValue && !Enum.IsDefined(request.BucketSize.Value))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.BucketSize)] = ["BucketSize must be Hourly or Daily."]
            })
            {
                Status = StatusCodes.Status400BadRequest
            });
        }

        var bucketSize = request.BucketSize ??
            (range.EndUtc - range.StartUtc <= TimeSpan.FromDays(3)
                ? AuditActivityBucketSize.Hourly
                : AuditActivityBucketSize.Daily);

        await auditService.LogAdminActionAsync(
            adminUserId.Value,
            "SearchAuditLogsByDateRange",
            "Admin searched audit logs by date range",
            new { StartDateUtc = range.StartUtc, EndDateUtc = range.EndUtc, range.TimeZone.Id, Filters = request })
            .ConfigureAwait(false);

        var query = new AuditLogQuery
        {
            UserId = request.UserId,
            TenantId = request.TenantId,
            ActionType = request.ActionType,
            ResourceType = request.ResourceType,
            Category = request.Category,
            RiskLevel = request.RiskLevel,
            Success = request.Success,
            StartDate = range.StartUtc,
            EndDate = range.EndUtc,
            IpAddress = request.IpAddress,
            Skip = request.Skip,
            Take = request.Take
        };

        var logs = await auditService.GetAuditLogsAsync(query).ConfigureAwait(false);
        var totalCount = await auditService.GetAuditLogCountAsync(query).ConfigureAwait(false);
        var activity = await auditService.GetAuditActivityAsync(query, bucketSize, cancellationToken).ConfigureAwait(false);

        return Ok(new AuditDateRangeSearchResponse
        {
            StartDateUtc = new DateTimeOffset(range.StartUtc, TimeSpan.Zero),
            EndDateUtc = new DateTimeOffset(range.EndUtc, TimeSpan.Zero),
            TimeZoneId = range.TimeZone.Id,
            BucketSize = bucketSize,
            Results = new AuditLogResponse
            {
                Logs = logs.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                Skip = request.Skip,
                Take = request.Take
            },
            Activity = activity.Select(bucket =>
            {
                var utcStart = new DateTimeOffset(bucket.StartUtc, TimeSpan.Zero);
                return new AuditActivityBucketResponse
                {
                    StartUtc = utcStart,
                    StartLocal = TimeZoneInfo.ConvertTime(utcStart, range.TimeZone),
                    EventCount = bucket.EventCount
                };
            }).ToList()
        });
    }

    /// <summary>Searches audit events by multiple action types, groups, and taxonomy categories.</summary>
    [HttpGet("search/by-action-type")]
    [ProducesResponseType(typeof(AuditActionTypeSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuditActionTypeSearchResponse>> SearchAuditLogsByActionType(
        [FromQuery] AuditActionTypeSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryValidateModel(request))
        {
            return ValidationProblem(ModelState);
        }

        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue) throw new UnauthorizedAccessException("User not authenticated");

        await auditService.LogAdminActionAsync(
            adminUserId.Value,
            "SearchAuditLogsByActionType",
            "Admin searched audit logs by action type",
            new { Filters = request })
            .ConfigureAwait(false);

        var result = await sender.Send(new SearchAuditActionTypesQuery(request), cancellationToken).ConfigureAwait(false);
        return Ok(new AuditActionTypeSearchResponse
        {
            Results = new AuditLogResponse
            {
                Logs = result.Logs.Select(MapToDto).ToList(),
                TotalCount = result.TotalCount,
                Skip = result.Skip,
                Take = result.Take
            },
            Frequency = result.Frequency,
            Trends = result.Trends,
            RelatedActions = result.RelatedActions
        });
    }

    /// <summary>Lists the hierarchical action type taxonomy and predefined investigation groups.</summary>
    [HttpGet("search/by-action-type/taxonomy")]
    [ProducesResponseType(typeof(AuditActionTypeTaxonomyResponse), StatusCodes.Status200OK)]
    public ActionResult<AuditActionTypeTaxonomyResponse> GetAuditActionTypeTaxonomy() => Ok(AuditActionTypeTaxonomy.GetTaxonomy());

    /// <summary>Exports matching action-type audit events as CSV or JSON.</summary>
    [HttpGet("search/by-action-type/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> ExportAuditLogsByActionType(
        [FromQuery] string format,
        [FromQuery] AuditActionTypeSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryValidateModel(request))
        {
            return ValidationProblem(ModelState);
        }

        if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(format), "Format must be csv or json.");
            return ValidationProblem(ModelState);
        }

        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue) throw new UnauthorizedAccessException("User not authenticated");

        var export = await sender.Send(new ExportAuditActionTypesQuery(request, 10_000), cancellationToken).ConfigureAwait(false);
        if (export.ExceedsLimit)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new ProblemDetails
            {
                Status = StatusCodes.Status413PayloadTooLarge,
                Title = "Audit export exceeds the limit",
                Detail = $"The query matches {export.TotalCount} records. Narrow the filters to export at most 10,000 records."
            });
        }

        await auditService.LogAdminActionAsync(
            adminUserId.Value,
            "ExportAuditLogsByActionType",
            "Admin exported action-type audit logs",
            new { format, export.TotalCount, Filters = request })
            .ConfigureAwait(false);

        var timestamp = SystemClock.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var records = export.Logs.Select(MapToDto).ToArray();
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            return File(AuditActionTypeExportFormatter.ToJson(records), "application/json", $"audit-action-search-{timestamp}.json");
        }

        return File(AuditActionTypeExportFormatter.ToCsv(records), "text/csv; charset=utf-8", $"audit-action-search-{timestamp}.csv");
    }

    /// <summary>
    /// Get audit log statistics
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<AuditStatisticsResponse>> GetAuditStatistics([FromQuery] AuditStatisticsRequest request)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue) throw new UnauthorizedAccessException("User not authenticated");

        await auditService.LogAdminActionAsync(adminUserId.Value, "ViewAuditStatistics", "Admin accessed audit statistics").ConfigureAwait(false);

        var startDate = request.StartDate ?? SystemClock.UtcNow.AddDays(-30);
        var endDate = request.EndDate ?? SystemClock.UtcNow;

        // Get statistics for different categories
        var authenticationQuery = new AuditLogQuery { Category = AuditCategory.Authentication, StartDate = startDate, EndDate = endDate };

        var permissionQuery = new AuditLogQuery { Category = AuditCategory.Permission, StartDate = startDate, EndDate = endDate };

        var securityQuery = new AuditLogQuery { Category = AuditCategory.Security, StartDate = startDate, EndDate = endDate };

        var failedQuery = new AuditLogQuery { Success = false, StartDate = startDate, EndDate = endDate };

        var highRiskQuery = new AuditLogQuery { RiskLevel = AuditRiskLevel.High, StartDate = startDate, EndDate = endDate };

        var totalEvents = await auditService.GetAuditLogCountAsync(new AuditLogQuery { StartDate = startDate, EndDate = endDate }).ConfigureAwait(false);
        var authenticationEvents = await auditService.GetAuditLogCountAsync(authenticationQuery).ConfigureAwait(false);
        var permissionEvents = await auditService.GetAuditLogCountAsync(permissionQuery).ConfigureAwait(false);
        var securityEvents = await auditService.GetAuditLogCountAsync(securityQuery).ConfigureAwait(false);
        var failedEvents = await auditService.GetAuditLogCountAsync(failedQuery).ConfigureAwait(false);
        var highRiskEvents = await auditService.GetAuditLogCountAsync(highRiskQuery).ConfigureAwait(false);

        var response = new AuditStatisticsResponse
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalEvents = totalEvents,
            AuthenticationEvents = authenticationEvents,
            PermissionEvents = permissionEvents,
            SecurityEvents = securityEvents,
            FailedEvents = failedEvents,
            HighRiskEvents = highRiskEvents
        };

        return Ok(response);
    }

    /// <summary>
    /// Export audit logs (admin only)
    /// </summary>
    [HttpPost(":export")]
    [HttpPost("export/csv")]
    [HttpPost("/api/audit/export/csv")]
    [EnableRateLimiting(RateLimitPolicies.ExpensiveOperations)]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "text/csv")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> ExportAuditLogs([FromBody] AuditExportRequest request)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue) throw new UnauthorizedAccessException("User not authenticated");

        var mediaValidation = ValidateExportMediaType("text/csv");
        if (mediaValidation is not null) { return mediaValidation; }

        var requestValidation = ValidateExportRequest(request);
        if (requestValidation is not null) { return requestValidation; }

        if (!AuditCsvExporter.TryResolveColumns(request.Columns, out var columns, out var columnError))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid CSV export columns",
                Detail = columnError,
                Status = StatusCodes.Status400BadRequest
            });
        }

        var exportId = Guid.NewGuid();
        var cancellationToken = HttpContext.RequestAborted;
        var export = await BeginExportAsync(adminUserId.Value, exportId, request, "csv", cancellationToken).ConfigureAwait(false);

        var fileName = $"audit-logs-{SystemClock.UtcNow:yyyy-MM-dd-HH-mm-ss}.csv";
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
        Response.Headers["X-Audit-Export-Id"] = exportId.ToString("D");
        Response.Headers["X-Audit-Total-Records"] = export.TotalCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (export.PageNumber.HasValue)
        {
            Response.Headers["X-Audit-Page"] = export.PageNumber.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Response.Headers["X-Audit-Page-Size"] = export.PageSize!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        try
        {
            await AuditCsvExporter.WriteAsync(
                Response.Body,
                TrackExportProgressAsync(export, adminUserId.Value, cancellationToken),
                columns,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await NotifyExportWebhookAsync(request.WebhookUrl, adminUserId.Value, export, "csv", "cancelled", null).ConfigureAwait(false);
            return new EmptyResult();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "CSV audit export {ExportId} failed", exportId);
            await MarkExportFailedAsync(export, adminUserId.Value).ConfigureAwait(false);
            await NotifyExportWebhookAsync(request.WebhookUrl, adminUserId.Value, export, "csv", "failed", "audit_export_failed").ConfigureAwait(false);
            if (Response.HasStarted)
            {
                HttpContext.Abort();
                return new EmptyResult();
            }

            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Audit export failed",
                detail: "The audit export could not be completed. Use the export ID when contacting support.");
        }

        await NotifyExportWebhookAsync(request.WebhookUrl, adminUserId.Value, export, "csv", "completed", null).ConfigureAwait(false);
        return new EmptyResult();
    }

    /// <summary>
    /// Streams a versioned JSON audit export with pagination metadata.
    /// </summary>
    [HttpPost("export/json")]
    [HttpPost("/api/audit/export/json")]
    [EnableRateLimiting(RateLimitPolicies.ExpensiveOperations)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(AuditJsonExportDocument), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> ExportAuditLogsJson([FromBody] AuditExportRequest request)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue) throw new UnauthorizedAccessException("User not authenticated");

        var mediaValidation = ValidateExportMediaType("application/json");
        if (mediaValidation is not null) { return mediaValidation; }

        var requestValidation = ValidateExportRequest(request);
        if (requestValidation is not null) { return requestValidation; }

        request.PageNumber ??= 1;
        request.PageSize ??= 500;

        var exportId = Guid.NewGuid();
        var cancellationToken = HttpContext.RequestAborted;
        var export = await BeginExportAsync(adminUserId.Value, exportId, request, "json", cancellationToken).ConfigureAwait(false);

        var pageSize = request.PageSize.Value;
        var totalPages = export.TotalCount == 0 ? 0 : (int)Math.Ceiling(export.TotalCount / (double)pageSize);
        Response.ContentType = "application/json; charset=utf-8";
        Response.Headers["Content-Disposition"] = $"attachment; filename=\"audit-logs-{SystemClock.UtcNow:yyyy-MM-dd-HH-mm-ss}.json\"";
        Response.Headers["X-Audit-Export-Id"] = exportId.ToString("D");
        Response.Headers["X-Audit-Total-Records"] = export.TotalCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Response.Headers["X-Audit-Page"] = request.PageNumber.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Response.Headers["X-Audit-Page-Size"] = pageSize.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var document = new AuditJsonExportDocument(
            "1.0",
            new AuditJsonExportPagination(request.PageNumber.Value, pageSize, export.TotalCount, totalPages),
            MapJsonRecordsAsync(TrackExportProgressAsync(export, adminUserId.Value, cancellationToken), cancellationToken));

        try
        {
            await Response.WriteAsJsonAsync(document, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await NotifyExportWebhookAsync(request.WebhookUrl, adminUserId.Value, export, "json", "cancelled", null).ConfigureAwait(false);
            return new EmptyResult();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "JSON audit export {ExportId} failed", exportId);
            await MarkExportFailedAsync(export, adminUserId.Value).ConfigureAwait(false);
            await NotifyExportWebhookAsync(request.WebhookUrl, adminUserId.Value, export, "json", "failed", "audit_export_failed").ConfigureAwait(false);
            if (Response.HasStarted)
            {
                HttpContext.Abort();
                return new EmptyResult();
            }

            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Audit export failed",
                detail: "The audit export could not be completed. Use the export ID when contacting support.");
        }

        await NotifyExportWebhookAsync(request.WebhookUrl, adminUserId.Value, export, "json", "completed", null).ConfigureAwait(false);
        return new EmptyResult();
    }

    /// <summary>Returns the current state of an export started by the authenticated administrator.</summary>
    [HttpGet("export/{exportId:guid}/progress")]
    [ProducesResponseType(typeof(AuditExportProgressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditExportProgressResponse>> GetAuditExportProgress(Guid exportId)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue) throw new UnauthorizedAccessException("User not authenticated");

        var progress = await exportProgressTracker.GetAsync(exportId, adminUserId.Value, HttpContext.RequestAborted).ConfigureAwait(false);
        return progress is null ? NotFound() : Ok(progress);
    }

    /// <summary>Creates a recurring audit export delivered to the tenant's configured storage.</summary>
    /// <remarks>
    /// Uses a five-field cron expression and the supplied timezone. During a repeated local time at the end of daylight
    /// saving, the first UTC occurrence is used. Files are removed after the configured retention period while their
    /// execution history remains available.
    /// </remarks>
    [HttpPost("scheduled-exports")]
    [EnableRateLimiting(RateLimitPolicies.ExpensiveOperations)]
    [ProducesResponseType(typeof(ScheduledAuditExportResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ScheduledAuditExportResponse>> CreateScheduledAuditExport(
        [FromBody] CreateScheduledAuditExportRequest request)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue) { throw new UnauthorizedAccessException("User not authenticated"); }

        var validation = ValidateRequest(request);
        if (validation is not null) { return validation; }

        try
        {
            var created = await sender.Send(
                new CreateScheduledAuditExportCommand(request, adminUserId.Value),
                HttpContext.RequestAborted).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetScheduledAuditExports), new { tenantId = created.TenantId }, created);
        }
        catch (Exception exception) when (exception is FormatException or TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid audit export schedule",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    /// <summary>Lists recurring audit exports for a tenant.</summary>
    [HttpGet("scheduled-exports")]
    [ProducesResponseType(typeof(IReadOnlyList<ScheduledAuditExportResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ScheduledAuditExportResponse>>> GetScheduledAuditExports(
        [FromQuery] Guid tenantId)
    {
        if (tenantId == Guid.Empty) { return BadRequest(new ProblemDetails { Title = "TenantId is required." }); }

        var exports = await scheduledExportService.GetForTenantAsync(tenantId, HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(exports);
    }

    /// <summary>Disables a recurring audit export without deleting its execution history.</summary>
    [HttpDelete("scheduled-exports/{exportId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DisableScheduledAuditExport(Guid exportId, [FromQuery] Guid tenantId)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue) { throw new UnauthorizedAccessException("User not authenticated"); }
        if (tenantId == Guid.Empty) { return BadRequest(new ProblemDetails { Title = "TenantId is required." }); }

        var disabled = await sender.Send(
            new DisableScheduledAuditExportCommand(exportId, tenantId, adminUserId.Value),
            HttpContext.RequestAborted).ConfigureAwait(false);
        return disabled ? NoContent() : NotFound();
    }

    /// <summary>Lists recent executions for a scheduled audit export.</summary>
    [HttpGet("scheduled-exports/{exportId:guid}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditExportHistoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AuditExportHistoryResponse>>> GetScheduledAuditExportHistory(
        Guid exportId,
        [FromQuery] Guid tenantId)
    {
        if (tenantId == Guid.Empty || await scheduledExportService.GetAsync(exportId, tenantId, HttpContext.RequestAborted).ConfigureAwait(false) is null)
        {
            return NotFound();
        }

        var history = await scheduledExportService.GetHistoryAsync(exportId, tenantId, HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(history);
    }

    /// <summary>Downloads a completed scheduled export stored for its tenant.</summary>
    [HttpGet("scheduled-export-history/{historyId:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DownloadScheduledAuditExport(Guid historyId, [FromQuery] Guid tenantId)
    {
        if (tenantId == Guid.Empty) { return NotFound(); }

        var download = await scheduledExportService.OpenDownloadAsync(historyId, tenantId, HttpContext.RequestAborted).ConfigureAwait(false);
        return download is null
            ? NotFound()
            : File(download.Content, download.ContentType, download.FileName, enableRangeProcessing: true);
    }

    private ActionResult? ValidateExportMediaType(string mediaType)
    {
        var accept = Request.Headers.Accept;
        if (accept.Count == 0 || string.IsNullOrWhiteSpace(accept.ToString())) { return null; }

        if (!MediaTypeHeaderValue.TryParseStrictList(accept.Select(value => value ?? string.Empty).ToArray(), out var ranges)
            || ranges.SelectMany(range => range.Parameters)
                .Where(parameter => parameter.Name.Equals("q", StringComparison.OrdinalIgnoreCase))
                .Any(parameter => !double.TryParse(parameter.Value.Value,
                    System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture,
                    out var quality) || quality is < 0 or > 1))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Accept header", detail: "Supply a valid Accept media range.");
        }

        var produced = MediaTypeHeaderValue.Parse(mediaType + "; charset=utf-8");
        var preferred = ranges
            .Where(range => produced.IsSubsetOf(range))
            .OrderByDescending(range => range.MatchesAllTypes ? 0 : range.MatchesAllSubTypes ? 1 : 2)
            .ThenByDescending(range => range.Parameters.TakeWhile(parameter =>
                !parameter.Name.Equals("q", StringComparison.OrdinalIgnoreCase)).Count())
            .ThenByDescending(range => range.Quality ?? 1)
            .FirstOrDefault();
        if (preferred is not null && (preferred.Quality ?? 1) > 0) { return null; }

        return Problem(statusCode: StatusCodes.Status406NotAcceptable,
            title: "Audit export format is not acceptable",
            detail: "This route produces " + mediaType + " with UTF-8 encoding.");
    }

    private ActionResult? ValidateExportRequest(AuditExportRequest request)
    {
        var requestValidation = ValidateRequest(request);
        if (requestValidation is not null) { return requestValidation; }

        var webhookValidationError = exportWebhookNotifier.ValidateWebhookUrl(request.WebhookUrl);
        return webhookValidationError is null
            ? null
            : BadRequest(new ProblemDetails
            {
                Title = "Invalid audit export webhook URL",
                Detail = webhookValidationError,
                Status = StatusCodes.Status400BadRequest
            });
    }

    private async Task<AuditLogExportData> BeginExportAsync(
        Guid adminUserId,
        Guid exportId,
        AuditExportRequest request,
        string format,
        CancellationToken cancellationToken)
    {
        try
        {
            var export = await sender.Send(
                new ExportAuditLogsCommand(adminUserId, exportId, request),
                cancellationToken).ConfigureAwait(false);
            await exportProgressTracker.BeginAsync(exportId, adminUserId, export.TotalCount, cancellationToken).ConfigureAwait(false);
            return export;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await NotifyExportWebhookAsync(request.WebhookUrl, adminUserId, exportId, format, "cancelled", 0, 0, null).ConfigureAwait(false);
            throw;
        }
        catch (Exception)
        {
            await NotifyExportWebhookAsync(request.WebhookUrl, adminUserId, exportId, format, "failed", 0, 0, "audit_export_failed").ConfigureAwait(false);
            throw;
        }
    }

    private async Task MarkExportFailedAsync(AuditLogExportData export, Guid ownerUserId)
    {
        try
        {
            var progress = await exportProgressTracker.GetAsync(export.ExportId, ownerUserId, CancellationToken.None).ConfigureAwait(false);
            if (progress is null) { return; }

            await exportProgressTracker.ReportAsync(
                export.ExportId,
                ownerUserId,
                progress.RecordsWritten,
                AuditExportProgressStatus.Failed,
                "The export could not be completed.",
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Updating progress for failed audit export {ExportId} failed", export.ExportId);
        }
    }

    private async Task NotifyExportWebhookAsync(
        string? webhookUrl,
        Guid ownerUserId,
        AuditLogExportData export,
        string format,
        string status,
        string? errorCode)
    {
        await NotifyExportWebhookAsync(
            webhookUrl,
            ownerUserId,
            export.ExportId,
            format,
            status,
            export.TotalCount,
            status == "completed" ? export.TotalCount : 0,
            errorCode).ConfigureAwait(false);
    }

    private async Task NotifyExportWebhookAsync(
        string? webhookUrl,
        Guid ownerUserId,
        Guid exportId,
        string format,
        string status,
        int totalRecords,
        int recordsWritten,
        string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl)) { return; }

        var effectiveTotalRecords = totalRecords;
        var effectiveRecordsWritten = recordsWritten;
        try
        {
            var progress = await exportProgressTracker.GetAsync(exportId, ownerUserId, CancellationToken.None).ConfigureAwait(false);
            if (progress is not null)
            {
                effectiveTotalRecords = progress.TotalRecords;
                effectiveRecordsWritten = progress.RecordsWritten;
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not read final progress for audit export {ExportId}", exportId);
        }

        var eventType = status switch
        {
            "completed" => "audit.export.completed",
            "cancelled" => "audit.export.cancelled",
            _ => "audit.export.failed"
        };
        var notification = new AuditExportWebhookNotification(
            $"{exportId:N}:{status}",
            eventType,
            DateTimeOffset.UtcNow,
            exportId,
            format,
            status,
            Math.Max(effectiveTotalRecords, 0),
            Math.Clamp(effectiveRecordsWritten, 0, Math.Max(effectiveTotalRecords, 0)),
            errorCode);

        try
        {
            await exportWebhookNotifier.NotifyAsync(webhookUrl, notification, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Delivering webhook for audit export {ExportId} failed", exportId);
        }
    }

    private ActionResult? ValidateRequest(object request)
    {
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        if (Validator.TryValidateObject(request, new ValidationContext(request), validationResults, validateAllProperties: true))
        {
            return null;
        }

        var errors = validationResults
            .SelectMany(result => result.MemberNames.DefaultIfEmpty("request"), (result, member) => new { member, result.ErrorMessage })
            .GroupBy(error => error.member, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage ?? "Invalid value.").ToArray(), StringComparer.Ordinal);

        return BadRequest(new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest
        });
    }

    private async IAsyncEnumerable<AuditJsonExportRecord> MapJsonRecordsAsync(
        IAsyncEnumerable<AuditLog> auditLogs,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var auditLog in auditLogs.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return AuditJsonExportMapper.Map(auditLog);
        }
    }

    private async IAsyncEnumerable<AuditLog> TrackExportProgressAsync(
        AuditLogExportData export,
        Guid ownerUserId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var recordsWritten = 0;
        var completed = false;
        var lastProgressReportTimestamp = Stopwatch.GetTimestamp();
        await using var enumerator = export.Records.GetAsyncEnumerator(cancellationToken);

        try
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Reading audit export {ExportId} failed", export.ExportId);
                    throw;
                }

                if (!hasNext) { break; }

                recordsWritten++;
                if (recordsWritten % 100 == 0 || Stopwatch.GetElapsedTime(lastProgressReportTimestamp) >= TimeSpan.FromSeconds(2))
                {
                    await exportProgressTracker.ReportAsync(
                        export.ExportId,
                        ownerUserId,
                        recordsWritten,
                        AuditExportProgressStatus.InProgress,
                        null,
                        cancellationToken).ConfigureAwait(false);
                    lastProgressReportTimestamp = Stopwatch.GetTimestamp();
                }

                yield return enumerator.Current;
            }

            await exportProgressTracker.ReportAsync(
                export.ExportId,
                ownerUserId,
                recordsWritten,
                AuditExportProgressStatus.Completed,
                null,
                CancellationToken.None).ConfigureAwait(false);
            completed = true;
        }
        finally
        {
            if (!completed)
            {
                var status = cancellationToken.IsCancellationRequested
                    ? AuditExportProgressStatus.Cancelled
                    : AuditExportProgressStatus.Failed;
                var message = status == AuditExportProgressStatus.Failed
                    ? "The export stopped before all records were written."
                    : null;

                try
                {
                    await exportProgressTracker.ReportAsync(
                        export.ExportId,
                        ownerUserId,
                        recordsWritten,
                        status,
                        message,
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception progressException)
                {
                    _logger.LogError(progressException, "Updating progress for audit export {ExportId} failed", export.ExportId);
                }
            }
        }
    }

    private AuditLogDto MapToDto(AuditLog log)
    {
        return new AuditLogDto
        {
            Id = log.Id,
            ActionType = log.ActionType,
            ResourceType = log.ResourceType,
            ResourceId = log.ResourceId,
            UserId = log.UserId,
            TenantId = log.TenantId,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            SessionId = log.SessionId,
            Description = log.Description,
            Success = log.Success,
            ErrorMessage = log.ErrorMessage,
            RiskLevel = log.RiskLevel,
            Category = log.Category,
            CorrelationId = log.CorrelationId,
            CreatedAt = log.CreatedAt
        };
    }

    private string GenerateCsv(List<AuditLog> logs)
    {
        var csv = new System.Text.StringBuilder();

        // Header
        csv.AppendLine("Id,ActionType,ResourceType,ResourceId,UserId,TenantId,IpAddress,Description,Success,RiskLevel,Category,CreatedAt");

        // Data rows
        foreach (var log in logs)
        {
            csv.AppendLine(
                $"{log.Id},{log.ActionType},{log.ResourceType},{log.ResourceId},{log.UserId},{log.TenantId},{log.IpAddress},\"{log.Description}\",{log.Success},{log.RiskLevel},{log.Category},{log.CreatedAt:yyyy-MM-dd HH:mm:ss}"
            );
        }

        return csv.ToString();
    }
}

// Request/Response DTOs
