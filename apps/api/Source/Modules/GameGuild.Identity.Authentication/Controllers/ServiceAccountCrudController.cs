using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Controller for service account CRUD operations (create, read, update, delete).
/// </summary>
[Microsoft.AspNetCore.Http.Tags("auth/service-accounts")]
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/auth/service-accounts")]
[Produces("application/json")]
public class ServiceAccountCrudController(
    IServiceAccountService serviceAccountService,
    ISender sender) : AuthControllerBase
{
    /// <summary>
    ///     Creates a new service account.
    /// </summary>
    /// <remarks>
    ///     The client secret is only returned once during creation. Store it securely.
    /// </remarks>
    /// <param name="request">The service account creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created service account with client credentials.</returns>
    [HttpPost]
    [Authorize(Policy = "RequireAdminRole")]
    [EnableRateLimiting(RateLimitPolicies.Authorization)]
    [ProducesResponseType(typeof(ServiceAccountCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateServiceAccount(
        [FromBody] CreateServiceAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Name is required" });
        }

        var createdBy = GetCurrentUserId().ToString();

        var (account, clientSecret) = await sender.Send(new CreateServiceAccountCommand(
            request.Name,
            request.Description,
            request.TenantId,
            request.Scopes ?? string.Empty,
            createdBy,
            request.AllowedIpAddresses,
            request.ExpiresAt),
            cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(
            nameof(GetServiceAccount),
            new { serviceAccountId = account.Id },
            new ServiceAccountCreatedResponse
            {
                Id = account.Id,
                ClientId = account.ClientId,
                ClientSecret = clientSecret, // Only returned once!
                Name = account.Name,
                Description = account.Description,
                TenantId = account.TenantId,
                Scopes = account.Scopes,
                CreatedAt = account.CreatedAt,
                ExpiresAt = account.ExpiresAt,
                Warning = "Store the client_secret securely. It will not be shown again."
            });
    }

    /// <summary>
    ///     Gets a service account by ID.
    /// </summary>
    [HttpGet("{serviceAccountId:guid}")]
    [Authorize(Policy = "RequireAdminRole")]
    [ProducesResponseType(typeof(ServiceAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServiceAccount(Guid serviceAccountId, CancellationToken cancellationToken)
    {
        var account = await serviceAccountService.GetByIdAsync(serviceAccountId, cancellationToken).ConfigureAwait(false);
        if (account == null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(account));
    }

    /// <summary>
    ///     Checks if a service account exists by ID.
    /// </summary>
    /// <param name="serviceAccountId">Service account ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>200 OK if exists, 404 Not Found if not</returns>
    [HttpHead("{serviceAccountId:guid}")]
    [Authorize(Policy = "RequireAdminRole")]
    [EndpointSummary("Check if service account exists")]
    [EndpointDescription("Checks if a service account exists without returning the body.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckServiceAccountExists(Guid serviceAccountId, CancellationToken cancellationToken)
    {
        var account = await serviceAccountService.GetByIdAsync(serviceAccountId, cancellationToken).ConfigureAwait(false);
        return account == null ? NotFound() : Ok();
    }

    /// <summary>
    ///     Partially updates a service account.
    /// </summary>
    /// <param name="serviceAccountId">Service account ID</param>
    /// <param name="request">Update request with optional fields</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPatch("{serviceAccountId:guid}")]
    [Authorize(Policy = "RequireAdminRole")]
    [EndpointSummary("Partially update service account")]
    [EndpointDescription("Updates specific fields of a service account. Only provided fields are updated.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PatchServiceAccount(
        Guid serviceAccountId,
        [FromBody] PatchServiceAccountRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await sender.Send(new PatchServiceAccountCommand(
            serviceAccountId,
            request.Name,
            request.Description,
            request.Scopes,
            request.ExpiresAt), cancellationToken).ConfigureAwait(false);
        return updated ? NoContent() : NotFound();
    }

    /// <summary>
    ///     Gets all service accounts with optional tenant filtering.
    /// </summary>
    /// <param name="tenantId">Optional tenant ID to filter service accounts</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of service accounts</returns>
    [HttpGet]
    [Authorize(Policy = "RequireAdminRole")]
    [ProducesResponseType(typeof(IEnumerable<ServiceAccountResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServiceAccounts([FromQuery] Guid? tenantId, CancellationToken cancellationToken)
    {
        if (tenantId.HasValue)
        {
            var accounts = await serviceAccountService.GetByTenantAsync(tenantId.Value, cancellationToken).ConfigureAwait(false);
            return Ok(accounts.Select(MapToResponse));
        }

        // Admin can list all service accounts across tenants
        var allAccounts = await serviceAccountService.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return Ok(allAccounts.Select(MapToResponse));
    }

    /// <summary>
    ///     Deletes a service account.
    /// </summary>
    [HttpDelete("{serviceAccountId:guid}")]
    [Authorize(Policy = "RequireAdminRole")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteServiceAccount(Guid serviceAccountId, CancellationToken cancellationToken)
    {
        var account = await serviceAccountService.GetByIdAsync(serviceAccountId, cancellationToken).ConfigureAwait(false);
        if (account == null)
        {
            return NotFound();
        }

        await sender.Send(new DeactivateServiceAccountCommand(serviceAccountId), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    internal static ServiceAccountResponse MapToResponse(ServiceAccount account) => new()
    {
        Id = account.Id,
        ClientId = account.ClientId,
        Name = account.Name,
        Description = account.Description,
        TenantId = account.TenantId,
        Scopes = account.Scopes,
        IsActive = account.IsActive,
        IsLocked = account.IsLocked,
        ExpiresAt = account.ExpiresAt,
        CreatedAt = account.CreatedAt,
        CreatedBy = account.CreatedBy,
        LastAuthenticatedAt = account.LastAuthenticatedAt,
        AuthenticationCount = account.AuthenticationCount,
        SecretRotationCount = account.SecretRotationCount
    };
}
