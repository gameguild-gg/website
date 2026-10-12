using Asp.Versioning;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Orchestrated authentication API — exposes the multi-step authentication flow
///     (risk-based challenge ladder over <see cref="IAuthenticationOrchestrationService"/>):
///     initiation with the primary credential, step-by-step challenge satisfaction,
///     token-issuing completion, state lookup and abandonment.
/// </summary>
/// <remarks>
///     Rate limited to 10 requests per minute per client to prevent brute-force attacks.
/// </remarks>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("auth")]
[EnableRateLimiting(RateLimitPolicies.Authentication)]
[Authorize]
public sealed class OrchestratedAuthController(ISender sender) : BaseApiController
{
    /// <summary>
    ///     Initiate an orchestrated authentication flow
    /// </summary>
    /// <param name="body">Identifier and password for the primary credential, with optional tenant/device context</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Flow state with the required challenge steps</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/orchestrated:initiate")]
    [EndpointSummary("Initiate an orchestrated authentication flow")]
    [EndpointDescription("Validates the local primary credential (identifier + password), assesses sign-in risk and returns the persisted flow state with the required challenge ladder (MFA verification, device trust, risk challenge). Expired, inactive or unknown credentials fail closed.")]
    [ProducesResponseType<OrchestratedFlowStateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Initiate([FromBody] InitiateOrchestratedAuthenticationRequestDto body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = new InitiateOrchestratedAuthenticationCommand
        {
            Identifier = body.Identifier,
            Password = body.Password,
            TenantId = body.TenantId,
            DeviceFingerprint = body.DeviceFingerprint
        };

        return Ok(await sender.Send(command, ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Satisfy one challenge step of an orchestrated authentication flow
    /// </summary>
    /// <param name="flowId">The flow whose step is being satisfied</param>
    /// <param name="body">The step and exactly one verification proof (TOTP code, backup code or WebAuthn assertion)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated flow state</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/orchestrated/{flowId}:process-step")]
    [EndpointSummary("Satisfy an orchestrated authentication challenge step")]
    [EndpointDescription("Verifies one challenge step (TOTP code, backup code or WebAuthn assertion) against the persisted flow and returns the updated state. Out-of-order, repeated, abandoned, expired or locked-out steps fail closed.")]
    [ProducesResponseType<OrchestratedFlowStateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ProcessStep(Guid flowId, [FromBody] ProcessOrchestratedAuthenticationStepRequestDto body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = new ProcessOrchestratedAuthenticationStepCommand
        {
            FlowId = flowId,
            Step = body.Step,
            TotpCode = body.TotpCode,
            BackupCode = body.BackupCode,
            WebAuthnAssertion = body.WebAuthnAssertion
        };

        return Ok(await sender.Send(command, ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Complete an orchestrated authentication flow and issue tokens
    /// </summary>
    /// <param name="flowId">The flow to complete</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Authentication tokens on success</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/orchestrated/{flowId}:complete")]
    [EndpointSummary("Complete an orchestrated authentication flow")]
    [EndpointDescription("Issues access and refresh tokens once every required step of the flow is satisfied. Pending steps, abandoned, expired or already-unknown flows fail closed.")]
    [ProducesResponseType<SignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Complete(Guid flowId, CancellationToken ct)
    {
        var command = new CompleteOrchestratedAuthenticationCommand { FlowId = flowId };
        return Ok(await sender.Send(command, ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Get the current state of an orchestrated authentication flow
    /// </summary>
    /// <param name="flowId">The flow to look up</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Flow state, or 404 when the flow is unknown, abandoned or expired</returns>
    [AllowAnonymous]
    [HttpGet("v{version:apiVersion}/auth/orchestrated/{flowId}")]
    [EndpointSummary("Get an orchestrated authentication flow state")]
    [EndpointDescription("Returns the current state of the flow. Abandoned, expired and unknown flows are not observable and return 404.")]
    [ProducesResponseType<OrchestratedFlowStateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFlowState(Guid flowId, CancellationToken ct)
    {
        var query = new GetOrchestratedAuthenticationFlowStateQuery { FlowId = flowId };
        var state = await sender.Send(query, ct).ConfigureAwait(false);

        return state is null ? NotFound() : Ok(state);
    }

    /// <summary>
    ///     Abandon an in-flight orchestrated authentication flow
    /// </summary>
    /// <param name="flowId">The flow to abandon</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/orchestrated/{flowId}:abandon")]
    [EndpointSummary("Abandon an orchestrated authentication flow")]
    [EndpointDescription("Terminates the flow; later step, completion and lookup calls fail closed.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Abandon(Guid flowId, CancellationToken ct)
    {
        var command = new AbandonOrchestratedAuthenticationCommand { FlowId = flowId };
        await sender.Send(command, ct).ConfigureAwait(false);
        return NoContent();
    }
}
