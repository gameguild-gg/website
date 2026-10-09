using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Compliance.Audit;

/// <summary>
/// Tenant-admin retention policy configuration, pre-built compliance templates and what-if forecasts;
/// no operation changes stored record retention.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/audit/retention-simulation")]
[Route("api/audit/retention-simulation")]
[Route("v{version:apiVersion}/audit/retention-policies")]
[Route("api/audit/retention-policies")]
[Tags("compliance/audit/retention-simulation")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Api)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class AuditRetentionSimulationController(IAuditRetentionSimulationService service, ISender sender) : ControllerBase
{
    /// <summary>
    /// Lists the pre-built retention policy templates (platform baseline plus SOC 2, ISO 27001, GDPR,
    /// HIPAA, PCI DSS and FedRAMP presets) with their inheritance chain and sensitivity retention floors.
    /// </summary>
    [HttpGet("templates")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditRetentionPolicyTemplate>), StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<AuditRetentionPolicyTemplate>>> GetPolicyTemplates(CancellationToken cancellationToken) =>
        Execute(async () => await service.GetPolicyTemplatesAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Gets the tenant retention policy configuration. With <paramref name="includeInherited"/>, a tenant
    /// without an explicit configuration receives the platform baseline template the policy tree inherits from.
    /// </summary>
    [HttpGet("configuration")]
    [ProducesResponseType(typeof(AuditRetentionConfigurationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<AuditRetentionConfigurationResponse>> GetConfiguration(
        [FromQuery(Name = "includeInherited")] bool includeInherited = false,
        CancellationToken cancellationToken) =>
        Execute(async () => await service.GetConfigurationAsync(includeInherited, cancellationToken).ConfigureAwait(false));

    [HttpPut("configuration")]
    [ProducesResponseType(typeof(AuditRetentionConfigurationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<ActionResult<AuditRetentionConfigurationResponse>> Configure(
        [FromBody] ConfigureAuditRetentionRequest request, CancellationToken cancellationToken) =>
        Execute(async () => await sender.Send(new ConfigureAuditRetentionCommand(request), cancellationToken).ConfigureAwait(false));

    [HttpPost]
    [ProducesResponseType(typeof(AuditRetentionSimulationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<AuditRetentionSimulationResponse>> Simulate(
        [FromBody] RunAuditRetentionSimulationRequest request, CancellationToken cancellationToken) =>
        Execute(async () => await sender.Send(new RunAuditRetentionSimulationCommand(request), cancellationToken).ConfigureAwait(false));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AuditRetentionSimulationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<AuditRetentionSimulationResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Execute(async () => await service.GetRunAsync(id, cancellationToken).ConfigureAwait(false));

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AuditRetentionSimulationSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<IReadOnlyList<AuditRetentionSimulationSummary>>> List(
        [FromQuery] AuditRetentionSimulationListRequest request, CancellationToken cancellationToken) =>
        Execute(async () => await service.GetRunsAsync(request.Skip, request.Take, cancellationToken).ConfigureAwait(false));

    private async Task<ActionResult<T>> Execute<T>(Func<Task<T?>> operation) where T : class
    {
        try
        {
            var response = await operation().ConfigureAwait(false);
            return response is null ? NotFound() : Ok(response);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (AuditRetentionValidationException exception)
        {
            return BadRequest(new ValidationProblemDetails(exception.Errors.ToDictionary(pair => pair.Key, pair => pair.Value))
                { Status = StatusCodes.Status400BadRequest });
        }
        catch (AuditRetentionConcurrencyException exception)
        {
            return Conflict(new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = exception.Message });
        }
    }
}
