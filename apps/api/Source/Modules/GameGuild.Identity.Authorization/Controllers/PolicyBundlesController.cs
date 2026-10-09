using Asp.Versioning;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     API controller for the central policy bundle registry: create, list, sign, approve,
///     deploy and roll back signed policy bundles.
/// </summary>
/// <remarks>
///     <para>
///         Lifecycle guards (handler enforced): drafts can be created by tenant administrators
///         for their own tenant or by system administrators; signing, approval, deployment and
///         rollback are system-administrator only. Approved/active transitions and published
///         bundle reads fail closed without a valid signature from a trusted signing key.
///     </para>
///     <para>
///         Hidden from the OpenAPI surface like the other authorization administration
///         controllers — the typed client is not regenerated for admin-only surfaces.
///     </para>
/// </remarks>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/authorization/policy-bundles")]
[Microsoft.AspNetCore.Http.Tags("access-control/policy-bundles")]
[ApiExplorerSettings(IgnoreApi = true)]
[EnableRateLimiting(RateLimitPolicies.Authorization)]
[Authorize]
public sealed class PolicyBundlesController(
    ISender sender,
    ILogger<PolicyBundlesController> logger) : BaseApiController
{
    private readonly ILogger<PolicyBundlesController> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    ///     Creates a draft policy bundle.
    /// </summary>
    /// <param name="command">The create command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created bundle id.</returns>
    /// <response code="201">Draft bundle created.</response>
    /// <response code="400">Bundle inputs violate the signing contract.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Not authorized for the target scope.</response>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePolicyBundleCommand command,
        CancellationToken cancellationToken)
    {
        var bundleId = await sender.Send(command, cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(List), new { tenantId = command.TenantId }, new { bundleId });
    }

    /// <summary>
    ///     Lists policy bundles for a scope.
    /// </summary>
    /// <param name="tenantId">Optional tenant scope; null lists the global scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Bundle summaries visible to the acting administrator.</returns>
    /// <response code="200">Bundle summaries.</response>
    /// <response code="403">Not authorized for the requested scope.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PolicyBundleSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<PolicyBundleSummary>>> List(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var query = new ListPolicyBundlesQuery { TenantId = tenantId };
        var bundles = await sender.Send(query, cancellationToken).ConfigureAwait(false);

        return Ok(bundles);
    }

    /// <summary>
    ///     Signs a policy bundle with the active trusted signing key. System administrators only.
    /// </summary>
    /// <param name="bundleId">The bundle id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Signature metadata.</returns>
    /// <response code="200">Bundle signed.</response>
    /// <response code="403">Not a system administrator.</response>
    /// <response code="404">Bundle not found.</response>
    [HttpPost("{bundleId:guid}:sign")]
    [ProducesResponseType(typeof(PolicyBundleSignatureInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PolicyBundleSignatureInfo>> Sign(
        Guid bundleId,
        CancellationToken cancellationToken)
    {
        var command = new SignPolicyBundleCommand { BundleId = bundleId };
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Approves a signed policy bundle after fail-closed signature verification.
    ///     System administrators only.
    /// </summary>
    /// <param name="bundleId">The bundle id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The approved bundle id.</returns>
    /// <response code="200">Bundle approved.</response>
    /// <response code="400">Signature missing or invalid (fail closed).</response>
    /// <response code="403">Not a system administrator.</response>
    /// <response code="404">Bundle not found.</response>
    [HttpPost("{bundleId:guid}:approve")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(Guid bundleId, CancellationToken cancellationToken)
    {
        var command = new ApprovePolicyBundleCommand { BundleId = bundleId };
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);

        return Ok(new { bundleId = result });
    }

    /// <summary>
    ///     Deploys an approved policy bundle: materializes its verified policy definitions for
    ///     the dynamic authorization policy provider and invalidates policy caches.
    ///     System administrators only.
    /// </summary>
    /// <param name="bundleId">The bundle id.</param>
    /// <param name="command">Deployment options (environment, notes).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deployment id.</returns>
    /// <response code="200">Bundle deployed.</response>
    /// <response code="400">Bundle not approved or signature invalid (fail closed).</response>
    /// <response code="403">Not a system administrator.</response>
    /// <response code="404">Bundle not found.</response>
    [HttpPost("{bundleId:guid}:deploy")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deploy(
        Guid bundleId,
        [FromBody] DeployPolicyBundleOptions? command,
        CancellationToken cancellationToken)
    {
        var request = new DeployPolicyBundleCommand
        {
            BundleId = bundleId,
            Environment = command?.Environment ?? "Production",
            Notes = command?.Notes
        };
        var deploymentId = await sender.Send(request, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Deployed policy bundle {BundleId} as deployment {DeploymentId}", bundleId, deploymentId);

        return Ok(new { deploymentId });
    }

    /// <summary>
    ///     Rolls back an active policy bundle deployment and removes its materialized policy
    ///     definitions. System administrators only.
    /// </summary>
    /// <param name="deploymentId">The deployment id.</param>
    /// <param name="command">The rollback reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the rollback completed.</returns>
    /// <response code="200">Deployment rolled back.</response>
    /// <response code="400">Deployment is not active.</response>
    /// <response code="403">Not a system administrator.</response>
    /// <response code="404">Deployment not found.</response>
    [HttpPost("deployments/{deploymentId:guid}:rollback")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rollback(
        Guid deploymentId,
        [FromBody] RollbackPolicyBundleOptions command,
        CancellationToken cancellationToken)
    {
        var request = new RollbackPolicyBundleDeploymentCommand
        {
            DeploymentId = deploymentId,
            Reason = command.Reason ?? "Rolled back via policy bundle API"
        };
        var result = await sender.Send(request, cancellationToken).ConfigureAwait(false);

        return Ok(new { rolledBack = result });
    }
}

/// <summary>Deployment options for the deploy endpoint body.</summary>
public sealed record DeployPolicyBundleOptions
{
    /// <summary>Gets the target environment name.</summary>
    public string? Environment { get; init; }

    /// <summary>Gets the optional deployment notes.</summary>
    public string? Notes { get; init; }
}

/// <summary>Rollback options for the rollback endpoint body.</summary>
public sealed record RollbackPolicyBundleOptions
{
    /// <summary>Gets the rollback reason.</summary>
    public string? Reason { get; init; }
}
