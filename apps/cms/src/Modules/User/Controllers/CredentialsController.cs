using Microsoft.AspNetCore.Mvc;
using cms.Modules.User.Models;
using cms.Modules.User.Services;
using cms.Modules.User.Dtos;

namespace cms.Modules.User.Controllers;

/// <summary>
/// REST API controller for managing user credentials
/// </summary>
[ApiController]
[Route("[controller]")]
public class CredentialsController : ControllerBase
{
    private readonly ICredentialService _credentialService;

    private readonly IUserService _userService;

    public CredentialsController(ICredentialService credentialService, IUserService userService)
    {
        _credentialService = credentialService;
        _userService = userService;
    }

    /// <summary>
    /// Get all credentials
    /// </summary>
    /// <returns>List of credentials</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CredentialResponseDto>>> GetCredentials()
    {
        var credentials = await _credentialService.GetAllCredentialsAsync();
        var response = credentials.Select(MapToResponseDto);

        return Ok(response);
    }

    /// <summary>
    /// Get credentials by user ID
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of user credentials</returns>
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<CredentialResponseDto>>> GetCredentialsByUserId(Guid userId)
    {
        var credentials = await _credentialService.GetCredentialsByUserIdAsync(userId);
        var response = credentials.Select(MapToResponseDto);

        return Ok(response);
    }

    /// <summary>
    /// Get a specific credential by ID
    /// </summary>
    /// <param name="id">Credential ID</param>
    /// <returns>Credential details</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<CredentialResponseDto>> GetCredential(Guid id)
    {
        Credential? credential = await _credentialService.GetCredentialByIdAsync(id);

        if (credential == null)
        {
            return NotFound($"Credential with ID {id} not found");
        }

        return Ok(MapToResponseDto(credential));
    }

    /// <summary>
    /// Get a credential by user ID and type
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="type">Credential type</param>
    /// <returns>Credential details</returns>
    [HttpGet("user/{userId}/type/{type}")]
    public async Task<ActionResult<CredentialResponseDto>> GetCredentialByUserIdAndType(Guid userId, string type)
    {
        Credential? credential = await _credentialService.GetCredentialByUserIdAndTypeAsync(userId, type);

        if (credential == null)
        {
            return NotFound($"Credential of type '{type}' for user {userId} not found");
        }

        return Ok(MapToResponseDto(credential));
    }

    /// <summary>
    /// Create a new credential
    /// </summary>
    /// <param name="createDto">Credential data</param>
    /// <returns>Created credential</returns>
    [HttpPost]
    public async Task<ActionResult<CredentialResponseDto>> CreateCredential([FromBody] CreateCredentialDto createDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Verify that the user exists
        Models.User? user = await _userService.GetUserByIdAsync(createDto.UserId);
        if (user == null)
        {
            return BadRequest($"User with ID {createDto.UserId} not found");
        }

        var credential = new Credential(
            new
            {
                UserId = createDto.UserId,
                Type = createDto.Type,
                Value = createDto.Value,
                Metadata = createDto.Metadata,
                ExpiresAt = createDto.ExpiresAt,
                IsActive = createDto.IsActive
            }
        );

        Credential createdCredential = await _credentialService.CreateCredentialAsync(credential);
        CredentialResponseDto response = MapToResponseDto(createdCredential);

        return CreatedAtAction(
            nameof(GetCredential),
            new
            {
                id = createdCredential.Id
            },
            response
        );
    }

    /// <summary>
    /// Update an existing credential
    /// </summary>
    /// <param name="id">Credential ID</param>
    /// <param name="updateDto">Updated credential data</param>
    /// <returns>Updated credential</returns>
    [HttpPut("{id}")]
    public async Task<ActionResult<CredentialResponseDto>> UpdateCredential(Guid id, [FromBody] UpdateCredentialDto updateDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        Credential? existingCredential = await _credentialService.GetCredentialByIdAsync(id);
        if (existingCredential == null)
        {
            return NotFound($"Credential with ID {id} not found");
        }

        // Update the credential properties
        existingCredential.Type = updateDto.Type;
        existingCredential.Value = updateDto.Value;
        existingCredential.Metadata = updateDto.Metadata;
        existingCredential.ExpiresAt = updateDto.ExpiresAt;
        existingCredential.IsActive = updateDto.IsActive;

        try
        {
            Credential updatedCredential = await _credentialService.UpdateCredentialAsync(existingCredential);
            CredentialResponseDto response = MapToResponseDto(updatedCredential);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Soft delete a credential
    /// </summary>
    /// <param name="id">Credential ID to delete</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> SoftDeleteCredential(Guid id)
    {
        bool result = await _credentialService.SoftDeleteCredentialAsync(id);

        if (!result)
        {
            return NotFound($"Credential with ID {id} not found");
        }

        return NoContent();
    }

    /// <summary>
    /// Restore a soft-deleted credential
    /// </summary>
    /// <param name="id">Credential ID to restore</param>
    /// <returns>No content if successful</returns>
    [HttpPost("{id}/restore")]
    public async Task<IActionResult> RestoreCredential(Guid id)
    {
        bool result = await _credentialService.RestoreCredentialAsync(id);

        if (!result)
        {
            return NotFound($"Deleted credential with ID {id} not found");
        }

        return NoContent();
    }

    /// <summary>
    /// Permanently delete a credential
    /// </summary>
    /// <param name="id">Credential ID to delete</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{id}/hard")]
    public async Task<IActionResult> HardDeleteCredential(Guid id)
    {
        bool result = await _credentialService.HardDeleteCredentialAsync(id);

        if (!result)
        {
            return NotFound($"Credential with ID {id} not found");
        }

        return NoContent();
    }

    /// <summary>
    /// Mark a credential as used
    /// </summary>
    /// <param name="id">Credential ID</param>
    /// <returns>No content if successful</returns>
    [HttpPost("{id}/mark-used")]
    public async Task<IActionResult> MarkCredentialAsUsed(Guid id)
    {
        bool result = await _credentialService.MarkCredentialAsUsedAsync(id);

        if (!result)
        {
            return NotFound($"Credential with ID {id} not found");
        }

        return NoContent();
    }

    /// <summary>
    /// Deactivate a credential
    /// </summary>
    /// <param name="id">Credential ID</param>
    /// <returns>No content if successful</returns>
    [HttpPost("{id}/deactivate")]
    public async Task<IActionResult> DeactivateCredential(Guid id)
    {
        bool result = await _credentialService.DeactivateCredentialAsync(id);

        if (!result)
        {
            return NotFound($"Credential with ID {id} not found");
        }

        return NoContent();
    }

    /// <summary>
    /// Activate a credential
    /// </summary>
    /// <param name="id">Credential ID</param>
    /// <returns>No content if successful</returns>
    [HttpPost("{id}/activate")]
    public async Task<IActionResult> ActivateCredential(Guid id)
    {
        bool result = await _credentialService.ActivateCredentialAsync(id);

        if (!result)
        {
            return NotFound($"Credential with ID {id} not found");
        }

        return NoContent();
    }

    /// <summary>
    /// Get soft-deleted credentials
    /// </summary>
    /// <returns>List of soft-deleted credentials</returns>
    [HttpGet("deleted")]
    public async Task<ActionResult<IEnumerable<CredentialResponseDto>>> GetDeletedCredentials()
    {
        var credentials = await _credentialService.GetDeletedCredentialsAsync();
        var response = credentials.Select(MapToResponseDto);

        return Ok(response);
    }

    /// <summary>
    /// Map Credential entity to response DTO
    /// </summary>
    /// <param name="credential">Credential entity</param>
    /// <returns>Credential response DTO</returns>
    private static CredentialResponseDto MapToResponseDto(Credential credential)
    {
        return new CredentialResponseDto
        {
            Id = credential.Id,
            UserId = credential.UserId,
            Type = credential.Type,
            Value = "***REDACTED***", // Don't expose actual credential values
            Metadata = credential.Metadata,
            ExpiresAt = credential.ExpiresAt,
            IsActive = credential.IsActive,
            LastUsedAt = credential.LastUsedAt,
            Version = credential.Version,
            CreatedAt = credential.CreatedAt,
            UpdatedAt = credential.UpdatedAt,
            DeletedAt = credential.DeletedAt,
            User = credential.User != null
                ? new UserResponseDto
                {
                    Id = credential.User.Id,
                    Name = credential.User.Name,
                    Email = credential.User.Email,
                    IsActive = credential.User.IsActive,
                    Version = credential.User.Version,
                    CreatedAt = credential.User.CreatedAt,
                    UpdatedAt = credential.User.UpdatedAt,
                    DeletedAt = credential.User.DeletedAt
                }
                : null
        };
    }
}
