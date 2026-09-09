using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Commerce.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Tenants;

/// <summary>
///     Tenants API Controller - RESTful API for tenant CRUD and collection operations
/// </summary>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("tenants")]
[Authorize]
public sealed class TenantsController(ISender sender) : BaseApiController
{
    #region Collection Operations - /v1/tenants

    /// <summary>
    ///     Create a new tenant organization
    /// </summary>
    /// <param name="body">Tenant creation request containing name, slug, admin email, and optional description</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created tenant with unique identifier and basic information</returns>
    [HttpPost("v{version:apiVersion}/tenants")]
    [EndpointSummary("Create a new tenant organization")]
    [EndpointDescription("Creates a new tenant organization within the GameGuild platform.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var id = await sender.Send(new CreateTenantCommand(body.Name, body.Slug, body.AdminEmail, body.Description), ct).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetTenantById), new { tenantId = id }, new { id, body.Name, body.Slug });
    }

    /// <summary>
    ///     Get tenants with pagination, search, and sorting
    /// </summary>
    /// <param name="page">Page number for pagination (default: 1)</param>
    /// <param name="pageSize">Number of tenants per page (default: 20, max: 500)</param>
    /// <param name="status">Optional status filter: 'active', 'inactive', 'archived', or null (all statuses)</param>
    /// <param name="searchTerm">Optional search term to filter tenants by name or slug</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated list of tenant organizations with metadata</returns>
    [HttpGet("v{version:apiVersion}/tenants")]
    [EndpointSummary("Get tenants with pagination, search, and sorting")]
    [EndpointDescription("Retrieves a paginated list of all tenant organizations accessible to the requesting user.")]
    [ProducesResponseType<PagedResult<Tenant>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTenants([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null, [FromQuery] string? searchTerm = null, CancellationToken ct = default)
    {
        // Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 500) pageSize = 500;

        var normalizedStatus = status?.Trim().ToLowerInvariant();
        bool? isActiveFilter = normalizedStatus switch
        {
            "active" => true,
            "inactive" => false,
            _ => null
        };
        bool? isArchivedFilter = normalizedStatus switch
        {
            "active" => false,
            "inactive" => false,
            "archived" => true,
            _ => null
        };

        var tenants = await sender.Send(
                new GetTenantsPageQuery(
                    page,
                    pageSize,
                    isActiveFilter,
                    isArchivedFilter,
                    searchTerm
                ),
                ct
            )
            ;

        return Ok(tenants);
    }

    /// <summary>
    ///     Get payment history for tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="startDate">Optional start date filter for payment history</param>
    /// <param name="endDate">Optional end date filter for payment history</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Payment history</returns>
    [HttpGet("v{version:apiVersion}/tenants/{tenantId:guid}/payments")]
    [EndpointSummary("Get payment history for tenant")]
    [EndpointDescription("Retrieves payment history for a specific tenant with optional date filtering.")]
    [ProducesResponseType<IEnumerable<PaymentResult>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaymentHistory(Guid tenantId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, CancellationToken ct)
    {
        return Ok(await sender.Send(new GetPaymentHistoryQuery(null, tenantId, startDate, endDate), ct));
    }

    /// <summary>
    ///     Validate tenant data before creation
    /// </summary>
    /// <param name="body">Tenant data to validate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Validation result with errors, warnings, and suggestions</returns>
    /// <remarks>
    ///     Validates tenant data without creating the tenant. Useful for:
    ///     - Checking if a slug is available
    ///     - Validating email format
    ///     - Checking for naming conflicts
    ///     - Getting alternative slug suggestions
    /// </remarks>
    [HttpPost("v{version:apiVersion}/tenants:validate")]
    [NoBusinessMutationEndpoint("Tenant validation is read-only and never creates or updates a tenant.")]
    [EndpointSummary("Validate tenant data before creation")]
    [EndpointDescription("Validates tenant data without creating. Returns errors, warnings, and suggestions.")]
    [ProducesResponseType<TenantValidationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateTenant([FromBody] ValidateTenantRequest body, CancellationToken ct)
    {
        var validationResult = await sender.Send(
            new ValidateTenantCommand(body.Name, body.Slug, body.AdminEmail),
            ct
        ).ConfigureAwait(false);

        return Ok(validationResult);
    }

    #endregion

    #region Individual Item Operations - /v1/tenants/{tenantId}

    /// <summary>
    ///     Check if tenant exists by ID
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>200 if exists, 404 if not</returns>
    [HttpHead("v{version:apiVersion}/tenants/{tenantId:guid}")]
    [EndpointSummary("Check if tenant exists by ID")]
    [EndpointDescription("Checks if a tenant exists by ID without returning the body.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckTenantExistsById(Guid tenantId, CancellationToken ct)
    {
        var tenant = await sender.Send(new GetTenantByIdQuery(tenantId), ct).ConfigureAwait(false);

        return tenant is null ? NotFound() : Ok();
    }

    /// <summary>
    ///     Get tenant by ID
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tenant details</returns>
    [HttpGet("v{version:apiVersion}/tenants/{tenantId:guid}")]
    [EndpointSummary("Get tenant by ID")]
    [EndpointDescription("Retrieves detailed information for a specific tenant by their unique identifier.")]
    [ProducesResponseType<Tenant>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTenantById(Guid tenantId, CancellationToken ct)
    {
        var tenant = await sender.Send(new GetTenantByIdQuery(tenantId), ct).ConfigureAwait(false);

        return tenant is null ? NotFound() : Ok(tenant);
    }

    /// <summary>
    ///     Partially update tenant by ID
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="body">Partial update data</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPatch("v{version:apiVersion}/tenants/{tenantId:guid}")]
    [EndpointSummary("Partially update tenant by ID")]
    [EndpointDescription("Updates specific fields of a tenant by ID.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PatchTenantById(Guid tenantId, [FromBody] UpdateTenantRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await sender.Send(new UpdateTenantCommand(tenantId, body.Name, body.Description), ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Update tenant by ID
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="body">Complete tenant data</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPut("v{version:apiVersion}/tenants/{tenantId:guid}")]
    [EndpointSummary("Update tenant by ID")]
    [EndpointDescription("Fully updates a tenant by ID with complete tenant data.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateTenantById(Guid tenantId, [FromBody] UpdateTenantRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await sender.Send(new UpdateTenantCommand(tenantId, body.Name, body.Description), ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Soft delete tenant by ID
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="body">Archive request containing reason and optional metadata</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("v{version:apiVersion}/tenants/{tenantId:guid}")]
    [EndpointSummary("Soft delete tenant by ID")]
    [EndpointDescription("Soft deletes a tenant by ID (can be restored).")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTenantById(Guid tenantId, [FromBody] ArchiveRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await sender.Send(new ArchiveTenantCommand(tenantId, body.Reason), ct).ConfigureAwait(false);

        return NoContent();
    }

    #endregion
}
