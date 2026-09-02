using Asp.Versioning;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Authentication;

public sealed record CreateStepUpChallengeRequest(
    string OperationType,
    string TargetReference,
    string PayloadHash);

public sealed record VerifyStepUpChallengeRequest(MfaMethod Method, string Evidence);

[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/auth/step-up")]
[Microsoft.AspNetCore.Http.Tags("auth/step-up")]
[Authorize]
public sealed class StepUpController(ISender sender) : AuthControllerBase
{
    [HttpPost("challenges")]
    [ProducesResponseType<StepUpChallengeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateChallenge(
        [FromBody] CreateStepUpChallengeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var response = await sender.Send(
                new CreateStepUpChallengeCommand(new StepUpOperationBinding(
                    request.OperationType,
                    request.TargetReference,
                    request.PayloadHash)),
                cancellationToken).ConfigureAwait(false);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (ArgumentException exception)
        {
            return InvalidRequest(exception.Message);
        }
        catch (StepUpContextUnavailableException exception)
        {
            return UnauthorizedProblem(exception.Message);
        }
    }

    [HttpPost("challenges/{challengeId:guid}:webauthn-options")]
    [ProducesResponseType<WebAuthnAuthenticationOptionsResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BeginWebAuthn(
        Guid challengeId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await sender.Send(
                new BeginStepUpWebAuthnCommand(challengeId),
                cancellationToken).ConfigureAwait(false));
        }
        catch (StepUpChallengeUnavailableException exception)
        {
            return ConflictProblem(exception.Message);
        }
        catch (StepUpVerificationFailedException exception)
        {
            return ConflictProblem(exception.Message);
        }
    }

    [HttpPost("challenges/{challengeId:guid}:verify")]
    [ProducesResponseType<StepUpReceiptResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> VerifyChallenge(
        Guid challengeId,
        [FromBody] VerifyStepUpChallengeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            return Ok(await sender.Send(
                new VerifyStepUpChallengeCommand(
                    challengeId,
                    new StepUpVerification(request.Method, request.Evidence)),
                cancellationToken).ConfigureAwait(false));
        }
        catch (ArgumentException exception)
        {
            return InvalidRequest(exception.Message);
        }
        catch (StepUpChallengeUnavailableException exception)
        {
            return ConflictProblem(exception.Message);
        }
        catch (StepUpVerificationFailedException exception)
        {
            return InvalidRequest(exception.Message);
        }
    }

    private ObjectResult InvalidRequest(string detail) => Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Step-up request is invalid",
        detail: detail);

    private ObjectResult ConflictProblem(string detail) => Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Step-up challenge is unavailable",
        detail: detail);

    private ObjectResult UnauthorizedProblem(string detail) => Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Step-up context is unavailable",
        detail: detail);
}
