using Microsoft.AspNetCore.Mvc;
using cms.Modules.Tenant.Models;
using cms.Modules.Tenant.Services;
using cms.Modules.Tenant.Dtos;
using cms.Modules.User.Dtos;

namespace cms.Modules.Tenant.Controllers;

/// <summary>
/// REST API controller for managing tenants
/// </summary>
[ApiController]
[Route("[controller]")]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;

    public TenantsController(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    /// <summary>
    /// Get all tenants
    /// </summary>
    /// <returns>List of tenants</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TenantResponseDto>>> GetTenants()
    {
        var tenants = await _tenantService.GetAllTenantsAsync();
        var response = tenants.Select(MapToResponseDto);

        return Ok(response);
    }

    /// <summary>
    /// Get a specific tenant by ID
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <returns>Tenant details</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<TenantResponseDto>> GetTenant(Guid id)
    {
        Models.Tenant? tenant = await _tenantService.GetTenantByIdAsync(id);

        if (tenant == null)
        {
            return NotFound($"Tenant with ID {id} not found");
        }

        return Ok(MapToResponseDto(tenant));
    }

    /// <summary>
    /// Get a tenant by name
    /// </summary>
    /// <param name="name">Tenant name</param>
    /// <returns>Tenant details</returns>
    [HttpGet("by-name/{name}")]
    public async Task<ActionResult<TenantResponseDto>> GetTenantByName(string name)
    {
        Models.Tenant? tenant = await _tenantService.GetTenantByNameAsync(name);

        if (tenant == null)
        {
            return NotFound($"Tenant with name '{name}' not found");
        }

        return Ok(MapToResponseDto(tenant));
    }

    /// <summary>
    /// Create a new tenant
    /// </summary>
    /// <param name="createDto">Tenant data</param>
    /// <returns>Created tenant</returns>
    [HttpPost]
    public async Task<ActionResult<TenantResponseDto>> CreateTenant([FromBody] CreateTenantDto createDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var tenant = new Models.Tenant(
            new
            {
                Name = createDto.Name, Description = createDto.Description, IsActive = createDto.IsActive
            }
        );

        Models.Tenant createdTenant = await _tenantService.CreateTenantAsync(tenant);
        TenantResponseDto response = MapToResponseDto(createdTenant);

        return CreatedAtAction(
            nameof(GetTenant),
            new
            {
                id = createdTenant.Id
            },
            response
        );
    }

    /// <summary>
    /// Update an existing tenant
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <param name="updateDto">Updated tenant data</param>
    /// <returns>Updated tenant</returns>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TenantResponseDto>> UpdateTenant(Guid id, [FromBody] UpdateTenantDto updateDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        Models.Tenant? existingTenant = await _tenantService.GetTenantByIdAsync(id);
        if (existingTenant == null)
        {
            return NotFound($"Tenant with ID {id} not found");
        }

        // Update the tenant properties
        existingTenant.Name = updateDto.Name;
        existingTenant.Description = updateDto.Description;
        existingTenant.IsActive = updateDto.IsActive;

        try
        {
            Models.Tenant updatedTenant = await _tenantService.UpdateTenantAsync(existingTenant);
            TenantResponseDto response = MapToResponseDto(updatedTenant);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Soft delete a tenant
    /// </summary>
    /// <param name="id">Tenant ID to delete</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> SoftDeleteTenant(Guid id)
    {
        bool result = await _tenantService.SoftDeleteTenantAsync(id);

        if (!result)
        {
            return NotFound($"Tenant with ID {id} not found");
        }

        return NoContent();
    }

    /// <summary>
    /// Restore a soft-deleted tenant
    /// </summary>
    /// <param name="id">Tenant ID to restore</param>
    /// <returns>No content if successful</returns>
    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> RestoreTenant(Guid id)
    {
        bool result = await _tenantService.RestoreTenantAsync(id);

        if (!result)
        {
            return NotFound($"Deleted tenant with ID {id} not found");
        }

        return NoContent();
    }

    /// <summary>
    /// Permanently delete a tenant
    /// </summary>
    /// <param name="id">Tenant ID to delete</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{id:guid}/hard")]
    public async Task<IActionResult> HardDeleteTenant(Guid id)
    {
        bool result = await _tenantService.HardDeleteTenantAsync(id);

        if (!result)
        {
            return NotFound($"Tenant with ID {id} not found");
        }

        return NoContent();
    }

    /// <summary>
    /// Activate a tenant
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <returns>No content if successful</returns>
    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> ActivateTenant(Guid id)
    {
        bool result = await _tenantService.ActivateTenantAsync(id);

        if (!result)
        {
            return NotFound($"Tenant with ID {id} not found");
        }

        return NoContent();
    }

    /// <summary>
    /// Deactivate a tenant
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <returns>No content if successful</returns>
    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateTenant(Guid id)
    {
        bool result = await _tenantService.DeactivateTenantAsync(id);

        if (!result)
        {
            return NotFound($"Tenant with ID {id} not found");
        }

        return NoContent();
    }

    /// <summary>
    /// Get soft-deleted tenants
    /// </summary>
    /// <returns>List of soft-deleted tenants</returns>
    [HttpGet("deleted")]
    public async Task<ActionResult<IEnumerable<TenantResponseDto>>> GetDeletedTenants()
    {
        var tenants = await _tenantService.GetDeletedTenantsAsync();
        var response = tenants.Select(MapToResponseDto);

        return Ok(response);
    }

    /// <summary>
    /// Add a user to a tenant
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <param name="userId">User ID</param>
    /// <returns>Created UserTenant relationship</returns>
    [HttpPost("{id:guid}/users/{userId:guid}")]
    public async Task<ActionResult<UserTenantResponseDto>> AddUserToTenant(Guid id, Guid userId)
    {
        try
        {
            UserTenant userTenant = await _tenantService.AddUserToTenantAsync(userId, id);
            UserTenantResponseDto response = MapUserTenantToResponseDto(userTenant);

            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest($"Failed to add user to tenant: {ex.Message}");
        }
    }

    /// <summary>
    /// Remove a user from a tenant
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <param name="userId">User ID</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{id}/users/{userId}")]
    public async Task<IActionResult> RemoveUserFromTenant(Guid id, Guid userId)
    {
        bool result = await _tenantService.RemoveUserFromTenantAsync(userId, id);

        if (!result)
        {
            return NotFound($"User {userId} not found in tenant {id}");
        }

        return NoContent();
    }

    /// <summary>
    /// Get users in a tenant
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <returns>List of UserTenant relationships</returns>
    [HttpGet("{id}/users")]
    public async Task<ActionResult<IEnumerable<UserTenantResponseDto>>> GetUsersInTenant(Guid id)
    {
        var userTenants = await _tenantService.GetUsersInTenantAsync(id);
        var response = userTenants.Select(MapUserTenantToResponseDto);

        return Ok(response);
    }

    /// <summary>
    /// Map Tenant entity to response DTO
    /// </summary>
    /// <param name="tenant">Tenant entity</param>
    /// <returns>Tenant response DTO</returns>
    private static TenantResponseDto MapToResponseDto(Models.Tenant tenant)
    {
        return new TenantResponseDto
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Description = tenant.Description,
            IsActive = tenant.IsActive,
            Version = tenant.Version,
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt,
            DeletedAt = tenant.DeletedAt,
            UserTenants = tenant.UserTenants?.Select(MapUserTenantToResponseDto).ToList() ?? new List<UserTenantResponseDto>()
        };
    }

    /// <summary>
    /// Map UserTenant entity to response DTO
    /// </summary>
    /// <param name="userTenant">UserTenant entity</param>
    /// <returns>UserTenant response DTO</returns>
    private static UserTenantResponseDto MapUserTenantToResponseDto(UserTenant userTenant)
    {
        return new UserTenantResponseDto
        {
            Id = userTenant.Id,
            UserId = userTenant.UserId,
            TenantId = userTenant.TenantId,
            IsActive = userTenant.IsActive,
            JoinedAt = userTenant.JoinedAt,
            Version = userTenant.Version,
            CreatedAt = userTenant.CreatedAt,
            UpdatedAt = userTenant.UpdatedAt,
            DeletedAt = userTenant.DeletedAt,
            User = userTenant.User != null
                ? new UserResponseDto
                {
                    Id = userTenant.User.Id,
                    Name = userTenant.User.Name,
                    Email = userTenant.User.Email,
                    IsActive = userTenant.User.IsActive,
                    Version = userTenant.User.Version,
                    CreatedAt = userTenant.User.CreatedAt,
                    UpdatedAt = userTenant.User.UpdatedAt,
                    DeletedAt = userTenant.User.DeletedAt
                }
                : null
        };
    }
}
