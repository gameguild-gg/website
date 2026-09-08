using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Controller for service account lifecycle operations: secret rotation, lock/unlock,
///     activate/deactivate, scope management, and audit logging.
/// </summary>
[Microsoft.AspNetCore.Http.Tags("auth/service-accounts")]
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/auth/service-accounts")]
[Produces("application/json")]
public class ServiceAccountOperationsController(
    IServiceAccountService serviceAccountService,
    ISender sender) : AuthControllerBase
{
    /// <summary>
    ///     Rotates the client secret for a service account.
    /// </summary>
    /// <remarks>
    ///     The new client secret is only returned once. Store it securely.
    ///     The old secret is immediately invalidated.
    /// </remarks>
    [HttpPost("{serviceAccountId:guid}:rotate-secret")]
    [Authorize(Policy = "RequireAdminRole")]
    [ProducesResponseType(typeof(SecretRotationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RotateSecret(Guid serviceAccountId, CancellationToken cancellationToken)
    {
        try
        {
            var newSecret = await sender.Send(
                new RotateServiceAccountSecretCommand(serviceAccountId), cancellationToken).ConfigureAwait(false);
            return Ok(new SecretRotationResponse
            {
                ClientSecret = newSecret,
                Warning = "Store the new client_secret securely. It will not be shown again."
            });
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    ///     Unlocks a locked service account.
    /// </summary>
    [HttpPost("{serviceAccountId:guid}:unlock")]
    [Authorize(Policy = "RequireAdminRole")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unlock(Guid serviceAccountId, CancellationToken cancellationToken)
    {
        try
        {
            await sender.Send(new UnlockServiceAccountCommand(serviceAccountId), cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    ///     Locks a service account to prevent authentication.
    /// </summary>
    /// <param name="serviceAccountId">Service account ID</param>
    /// <param name="request">Lock request with reason</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("{serviceAccountId:guid}:lock")]
    [Authorize(Policy = "RequireAdminRole")]
    [EndpointSummary("Lock service account")]
    [EndpointDescription("Locks a service account to prevent it from authenticating.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Lock(Guid serviceAccountId, [FromBody] LockServiceAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await serviceAccountService.GetByIdAsync(serviceAccountId, cancellationToken).ConfigureAwait(false);
        if (account == null)
        {
            return NotFound();
        }

        await sender.Send(new LockServiceAccountCommand(serviceAccountId, request.Reason), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    ///     Gets the audit log for a service account.
    /// </summary>
    /// <param name="serviceAccountId">Service account ID</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 20, max: 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated audit log entries</returns>
    [HttpGet("{serviceAccountId:guid}/audit-log")]
    [Authorize(Policy = "RequireAdminRole")]
    [EndpointSummary("Get service account audit log")]
    [EndpointDescription("Retrieves the audit log of actions performed on or by a service account.")]
    [ProducesResponseType(typeof(ServiceAccountAuditLogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuditLog(
        Guid serviceAccountId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var account = await serviceAccountService.GetByIdAsync(serviceAccountId, cancellationToken).ConfigureAwait(false);
        if (account == null)
        {
            return NotFound();
        }

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var auditLog = await serviceAccountService.GetAuditLogAsync(
            serviceAccountId,
            (page - 1) * pageSize,
            pageSize,
            cancellationToken).ConfigureAwait(false);

        return Ok(new ServiceAccountAuditLogResponse
        {
            ServiceAccountId = serviceAccountId,
            Entries = auditLog.Items,
            TotalCount = auditLog.TotalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    ///     Deactivates a service account.
    /// </summary>
    [HttpPost("{serviceAccountId:guid}:deactivate")]
    [Authorize(Policy = "RequireAdminRole")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid serviceAccountId, CancellationToken cancellationToken)
    {
        try
        {
            await sender.Send(new DeactivateServiceAccountCommand(serviceAccountId), cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    ///     Reactivates a deactivated service account.
    /// </summary>
    [HttpPost("{serviceAccountId:guid}:reactivate")]
    [Authorize(Policy = "RequireAdminRole")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reactivate(Guid serviceAccountId, CancellationToken cancellationToken)
    {
        try
        {
            await sender.Send(new ReactivateServiceAccountCommand(serviceAccountId), cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    ///     Updates the scopes for a service account.
    /// </summary>
    [HttpPatch("{serviceAccountId:guid}/scopes")]
    [Authorize(Policy = "RequireAdminRole")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateScopes(Guid serviceAccountId, [FromBody] UpdateScopesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await sender.Send(
                new UpdateServiceAccountScopesCommand(serviceAccountId, request.Scopes),
                cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }
}
