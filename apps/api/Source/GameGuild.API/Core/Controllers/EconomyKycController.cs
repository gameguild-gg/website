using System.Globalization;
using Asp.Versioning;
using GameGuild.Compliance.KYC;
using GameGuild.CQRS;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.API.Controllers;

public sealed record StartMyKycRequest(string IdempotencyKey);
public sealed record CreateMyKycAccessTokenRequest(int LifetimeSeconds);
public sealed record EconomyKycStatusDto(
    bool HasEvidence,
    ComplianceEvidenceResult? Result,
    long? Version,
    DateTimeOffset? IssuedAt,
    DateTimeOffset? ExpiresAt,
    bool IsCurrent);

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/economy/kyc")]
[Tags("economy-kyc")]
[Authorize]
public sealed class EconomyKycController(
    ISender sender,
    IComplianceEvidenceReader evidence,
    IActorContextAccessor actorContextAccessor,
    TimeProvider timeProvider) : BaseApiController
{
    [HttpPost("onboarding")]
    [ProducesResponseType(typeof(KycAmlOnboarding), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Start(
        [FromBody] StartMyKycRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out var actorId)) return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        var onboarding = await sender.Send(
            new StartKycOnboardingEndpointCommand(new StartKycAmlRequest(
                tenantId,
                EconomySubjectReference.ForUser(tenantId, actorId),
                request.IdempotencyKey,
                timeProvider.GetUtcNow())),
            cancellationToken).ConfigureAwait(false);
        return StatusCode(StatusCodes.Status201Created, onboarding);
    }

    [HttpPost("access-token")]
    [ProducesResponseType(typeof(KycAmlAccessToken), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateAccessToken(
        [FromBody] CreateMyKycAccessTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out var actorId)) return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var token = await sender.Send(new CreateKycAccessTokenEndpointCommand(
                tenantId,
                EconomySubjectReference.ForUser(tenantId, actorId),
                request.LifetimeSeconds),
                cancellationToken).ConfigureAwait(false);
            return Ok(token);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(EconomyKycStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out var actorId)) return Forbid();
        var current = await evidence.ReadLatestAsync(
            tenantId,
            EconomySubjectReference.ForUser(tenantId, actorId),
            ComplianceEvidenceKinds.KycAml,
            cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        return Ok(current is null
            ? new EconomyKycStatusDto(false, null, null, null, null, false)
            : new EconomyKycStatusDto(
                true,
                current.Result,
                current.Version,
                current.IssuedAt,
                current.ExpiresAt,
                current.SignatureVerified && current.ExpiresAt > now));
    }

    private bool TryActor(out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.TenantId.HasValue || !actor.SubjectIdAsGuid.HasValue)
            return false;
        tenantId = actor.TenantId.Value;
        actorId = actor.SubjectIdAsGuid.Value;
        return true;
    }
}

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/integrations/economy/sumsub")]
[Tags("economy-integrations")]
[ApiController]
public sealed class EconomySumSubWebhookController(
    ISender sender,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SumSubWebhookIngestionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Ingest(CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("X-Payload-Digest", out var digest) ||
            !Request.Headers.TryGetValue("X-Payload-Digest-Alg", out var algorithm) ||
            !Request.Headers.TryGetValue("X-Payload-Issued-At", out var issuedAtHeader) ||
            !TryParseIssuedAt(issuedAtHeader.ToString(), out var issuedAt))
            return BadRequest("SumSub signature and timestamp headers are required.");

        await using var payload = new MemoryStream();
        await Request.Body.CopyToAsync(payload, cancellationToken).ConfigureAwait(false);
        var result = await sender.Send(new IngestSumSubWebhookEndpointCommand(
            payload.ToArray(),
            digest.ToString(),
            algorithm.ToString(),
            issuedAt,
            timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    private static bool TryParseIssuedAt(string value, out DateTimeOffset issuedAt)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            try
            {
                issuedAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                issuedAt = default;
                return false;
            }
        }
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out issuedAt);
    }
}
