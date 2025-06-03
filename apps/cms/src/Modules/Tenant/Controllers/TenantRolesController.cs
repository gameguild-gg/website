using Microsoft.AspNetCore.Mvc;
using cms.Modules.Tenant.Models;
using cms.Modules.Tenant.Services;
using cms.Modules.Tenant.Dtos;

namespace cms.Modules.Tenant.Controllers;

/// <summary>
/// REST API controller for managing tenant roles
/// </summary>
[ApiController]
[Route("[controller]")]
public class TenantRolesController : ControllerBase
{
    private readonly ITenantRoleService _tenantRoleService;
    private readonly ITenantService _tenantService;

    public TenantRolesController(ITenantRoleService tenantRoleService, ITenantService tenantService)
    {
        _tenantRoleService = tenantRoleService;
        _tenantService = tenantService;
    }

    /// <summary>
    /// Get all roles for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <returns>List of tenant roles</returns>
    [HttpGet("tenant/{tenantId}")]
    public async Task<ActionResult<IEnumerable<TenantRoleResponseDto>>> GetRolesByTenantId(Guid tenantId)
    {
        var roles = await _tenantRoleService.GetRolesByTenantIdAsync(tenantId);
        var response = roles.Select(MapToResponseDto);
        return Ok(response);
    }

    /// <summary>
    /// Get a specific role by ID
    /// </summary>
    /// <param name="id">Role ID</param>
    /// <returns>Tenant role details</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<TenantRoleResponseDto>> GetRole(Guid id)
    {
        TenantRole? role = await _tenantRoleService.GetRoleByIdAsync(id);
        
        if (role == null)
        {
            return NotFound($"Tenant role with ID {id} not found");
        }

        return Ok(MapToResponseDto(role));
    }

    /// <summary>
    /// Get a role by tenant ID and name
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="name">Role name</param>
    /// <returns>Tenant role details</returns>
    [HttpGet("tenant/{tenantId}/name/{name}")]
    public async Task<ActionResult<TenantRoleResponseDto>> GetRoleByTenantIdAndName(Guid tenantId, string name)
    {
        TenantRole? role = await _tenantRoleService.GetRoleByTenantIdAndNameAsync(tenantId, name);
        
        if (role == null)
        {
            return NotFound($"Role '{name}' not found in tenant {tenantId}");
        }

        return Ok(MapToResponseDto(role));
    }

    /// <summary>
    /// Create a new tenant role
    /// </summary>
    /// <param name="createDto">Role data</param>
    /// <returns>Created role</returns>
    [HttpPost]
    public async Task<ActionResult<TenantRoleResponseDto>> CreateRole([FromBody] CreateTenantRoleDto createDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Verify that the tenant exists
        Models.Tenant? tenant = await _tenantService.GetTenantByIdAsync(createDto.TenantId);
        if (tenant == null)
        {
            return BadRequest($"Tenant with ID {createDto.TenantId} not found");
        }

        var role = new TenantRole(new
        {
            TenantId = createDto.TenantId,
            Name = createDto.Name,
            Description = createDto.Description,
            Permissions = createDto.Permissions,
            IsActive = createDto.IsActive
        });

        TenantRole createdRole = await _tenantRoleService.CreateRoleAsync(role);
        TenantRoleResponseDto response = MapToResponseDto(createdRole);

        return CreatedAtAction(
            nameof(GetRole),
            new { id = createdRole.Id },
            response);
    }

    /// <summary>
    /// Update an existing tenant role
    /// </summary>
    /// <param name="id">Role ID</param>
    /// <param name="updateDto">Updated role data</param>
    /// <returns>Updated role</returns>
    [HttpPut("{id}")]
    public async Task<ActionResult<TenantRoleResponseDto>> UpdateRole(Guid id, [FromBody] UpdateTenantRoleDto updateDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        TenantRole? existingRole = await _tenantRoleService.GetRoleByIdAsync(id);
        if (existingRole == null)
        {
            return NotFound($"Tenant role with ID {id} not found");
        }

        // Update the role properties
        existingRole.Name = updateDto.Name;
        existingRole.Description = updateDto.Description;
        existingRole.Permissions = updateDto.Permissions;
        existingRole.IsActive = updateDto.IsActive;

        try
        {
            TenantRole updatedRole = await _tenantRoleService.UpdateRoleAsync(existingRole);
            TenantRoleResponseDto response = MapToResponseDto(updatedRole);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Soft delete a tenant role
    /// </summary>
    /// <param name="id">Role ID to delete</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> SoftDeleteRole(Guid id)
    {
        bool result = await _tenantRoleService.SoftDeleteRoleAsync(id);
        
        if (!result)
        {
            return NotFound($"Tenant role with ID {id} not found");
        }

        return NoContent();
    }

    /// <summary>
    /// Restore a soft-deleted tenant role
    /// </summary>
    /// <param name="id">Role ID to restore</param>
    /// <returns>No content if successful</returns>
    [HttpPost("{id}/restore")]
    public async Task<IActionResult> RestoreRole(Guid id)
    {
        bool result = await _tenantRoleService.RestoreRoleAsync(id);
        
        if (!result)
        {
            return NotFound($"Deleted tenant role with ID {id} not found");
        }

        return NoContent();
    }

    /// <summary>
    /// Permanently delete a tenant role
    /// </summary>
    /// <param name="id">Role ID to delete</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{id}/hard")]
    public async Task<IActionResult> HardDeleteRole(Guid id)
    {
        bool result = await _tenantRoleService.HardDeleteRoleAsync(id);
        
        if (!result)
        {
            return NotFound($"Tenant role with ID {id} not found");
        }

        return NoContent();
    }

    /// <summary>
    /// Assign a role to a user in a tenant
    /// </summary>
    /// <param name="id">Role ID</param>
    /// <param name="userTenantId">UserTenant ID</param>
    /// <param name="expiresAt">Optional expiration date</param>
    /// <returns>Created UserTenantRole assignment</returns>
    [HttpPost("{id}/assign/{userTenantId}")]
    public async Task<ActionResult<UserTenantRoleResponseDto>> AssignRoleToUser(Guid id, Guid userTenantId, [FromQuery] DateTime? expiresAt = null)
    {
        try
        {
            UserTenantRole userTenantRole = await _tenantRoleService.AssignRoleToUserAsync(userTenantId, id, expiresAt);
            UserTenantRoleResponseDto response = MapUserTenantRoleToResponseDto(userTenantRole);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest($"Failed to assign role to user: {ex.Message}");
        }
    }

    /// <summary>
    /// Remove a role from a user in a tenant
    /// </summary>
    /// <param name="id">Role ID</param>
    /// <param name="userTenantId">UserTenant ID</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{id}/assign/{userTenantId}")]
    public async Task<IActionResult> RemoveRoleFromUser(Guid id, Guid userTenantId)
    {
        bool result = await _tenantRoleService.RemoveRoleFromUserAsync(userTenantId, id);
        
        if (!result)
        {
            return NotFound($"Role assignment not found");
        }

        return NoContent();
    }

    /// <summary>
    /// Get roles for a user in a tenant
    /// </summary>
    /// <param name="userTenantId">UserTenant ID</param>
    /// <returns>List of UserTenantRole assignments</returns>
    [HttpGet("user-tenant/{userTenantId}")]
    public async Task<ActionResult<IEnumerable<UserTenantRoleResponseDto>>> GetUserRolesInTenant(Guid userTenantId)
    {
        var userRoles = await _tenantRoleService.GetUserRolesInTenantAsync(userTenantId);
        var response = userRoles.Select(MapUserTenantRoleToResponseDto);
        return Ok(response);
    }

    /// <summary>
    /// Get users with a specific role
    /// </summary>
    /// <param name="id">Role ID</param>
    /// <returns>List of UserTenantRole assignments</returns>
    [HttpGet("{id}/users")]
    public async Task<ActionResult<IEnumerable<UserTenantRoleResponseDto>>> GetUsersWithRole(Guid id)
    {
        var userRoles = await _tenantRoleService.GetUsersWithRoleAsync(id);
        var response = userRoles.Select(MapUserTenantRoleToResponseDto);
        return Ok(response);
    }

    /// <summary>
    /// Map TenantRole entity to response DTO
    /// </summary>
    /// <param name="role">TenantRole entity</param>
    /// <returns>TenantRole response DTO</returns>
    private static TenantRoleResponseDto MapToResponseDto(TenantRole role)
    {
        return new TenantRoleResponseDto
        {
            Id = role.Id,
            TenantId = role.TenantId,
            Name = role.Name,
            Description = role.Description,
            Permissions = role.Permissions,
            IsActive = role.IsActive,
            Version = role.Version,
            CreatedAt = role.CreatedAt,
            UpdatedAt = role.UpdatedAt,
            DeletedAt = role.DeletedAt,
            Tenant = role.Tenant != null ? new TenantResponseDto
            {
                Id = role.Tenant.Id,
                Name = role.Tenant.Name,
                Description = role.Tenant.Description,
                IsActive = role.Tenant.IsActive,
                Version = role.Tenant.Version,
                CreatedAt = role.Tenant.CreatedAt,
                UpdatedAt = role.Tenant.UpdatedAt,
                DeletedAt = role.Tenant.DeletedAt
            } : null
        };
    }

    /// <summary>
    /// Map UserTenantRole entity to response DTO
    /// </summary>
    /// <param name="userTenantRole">UserTenantRole entity</param>
    /// <returns>UserTenantRole response DTO</returns>
    private static UserTenantRoleResponseDto MapUserTenantRoleToResponseDto(UserTenantRole userTenantRole)
    {
        return new UserTenantRoleResponseDto
        {
            Id = userTenantRole.Id,
            UserTenantId = userTenantRole.UserTenantId,
            TenantRoleId = userTenantRole.TenantRoleId,
            IsActive = userTenantRole.IsActive,
            AssignedAt = userTenantRole.AssignedAt,
            ExpiresAt = userTenantRole.ExpiresAt,
            Version = userTenantRole.Version,
            CreatedAt = userTenantRole.CreatedAt,
            UpdatedAt = userTenantRole.UpdatedAt,
            DeletedAt = userTenantRole.DeletedAt,
            TenantRole = userTenantRole.TenantRole != null ? new TenantRoleResponseDto
            {
                Id = userTenantRole.TenantRole.Id,
                TenantId = userTenantRole.TenantRole.TenantId,
                Name = userTenantRole.TenantRole.Name,
                Description = userTenantRole.TenantRole.Description,
                Permissions = userTenantRole.TenantRole.Permissions,
                IsActive = userTenantRole.TenantRole.IsActive,
                Version = userTenantRole.TenantRole.Version,
                CreatedAt = userTenantRole.TenantRole.CreatedAt,
                UpdatedAt = userTenantRole.TenantRole.UpdatedAt,
                DeletedAt = userTenantRole.TenantRole.DeletedAt
            } : null
        };
    }
}
