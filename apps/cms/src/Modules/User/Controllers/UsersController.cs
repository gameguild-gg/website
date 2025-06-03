using Microsoft.AspNetCore.Mvc;
using cms.Modules.User.Services;
using cms.Modules.User.Dtos;
using cms.Modules.User.Models;

namespace cms.Modules.User.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }    // GET: api/users
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetUsers()
    {
        var users = await _userService.GetAllUsersAsync();
        var userDtos = users.Select(u => new UserResponseDto
        {
            Id = u.Id,
            Version = u.Version,
            Name = u.Name,
            Email = u.Email,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt,
            DeletedAt = u.DeletedAt,
            IsDeleted = u.IsDeleted
        });

        return Ok(userDtos);
    }

    // GET: api/users/deleted
    [HttpGet("deleted")]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetDeletedUsers()
    {
        var users = await _userService.GetDeletedUsersAsync();
        var userDtos = users.Select(u => new UserResponseDto
        {
            Id = u.Id,
            Version = u.Version,
            Name = u.Name,
            Email = u.Email,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt,
            DeletedAt = u.DeletedAt,
            IsDeleted = u.IsDeleted
        });

        return Ok(userDtos);
    }

    // GET: api/users/{id}
    [HttpGet("{id}")]    public async Task<ActionResult<UserResponseDto>> GetUser(Guid id)
    {
        var user = await _userService.GetUserByIdAsync(id);

        if (user == null)
        {
            return NotFound();
        }

        var userDto = new UserResponseDto
        {
            Id = user.Id,
            Version = user.Version,
            Name = user.Name,
            Email = user.Email,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            DeletedAt = user.DeletedAt,
            IsDeleted = user.IsDeleted
        };

        return Ok(userDto);
    }

    // POST: api/users
    [HttpPost]    public async Task<ActionResult<UserResponseDto>> CreateUser(CreateUserDto createUserDto)
    {
        // Use BaseEntity.Create for consistent creation pattern
        var user = new Models.User(new
        {
            Name = createUserDto.Name,
            Email = createUserDto.Email,
            IsActive = true
        });

        var createdUser = await _userService.CreateUserAsync(user);

        var userDto = new UserResponseDto
        {
            Id = createdUser.Id,
            Version = createdUser.Version,
            Name = createdUser.Name,
            Email = createdUser.Email,
            IsActive = createdUser.IsActive,
            CreatedAt = createdUser.CreatedAt,
            UpdatedAt = createdUser.UpdatedAt,
            DeletedAt = createdUser.DeletedAt,
            IsDeleted = createdUser.IsDeleted
        };

        return CreatedAtAction(nameof(GetUser), new { id = createdUser.Id }, userDto);
    }

    // PUT: api/users/{id}
    [HttpPut("{id}")]    public async Task<ActionResult<UserResponseDto>> UpdateUser(Guid id, UpdateUserDto updateUserDto)
    {
        var existingUser = await _userService.GetUserByIdAsync(id);
        if (existingUser == null)
        {
            return NotFound();
        }

        // Update only provided properties
        if (!string.IsNullOrEmpty(updateUserDto.Name))
            existingUser.Name = updateUserDto.Name;
        
        if (!string.IsNullOrEmpty(updateUserDto.Email))
            existingUser.Email = updateUserDto.Email;

        var updatedUser = await _userService.UpdateUserAsync(id, existingUser);
        if (updatedUser == null)
        {
            return NotFound();
        }

        var userDto = new UserResponseDto
        {
            Id = updatedUser.Id,
            Version = updatedUser.Version,
            Name = updatedUser.Name,
            Email = updatedUser.Email,
            IsActive = updatedUser.IsActive,
            CreatedAt = updatedUser.CreatedAt,
            UpdatedAt = updatedUser.UpdatedAt,
            DeletedAt = updatedUser.DeletedAt,
            IsDeleted = updatedUser.IsDeleted
        };

        return Ok(userDto);
    }

    // DELETE: api/users/{id}
    [HttpDelete("{id}")]    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var result = await _userService.DeleteUserAsync(id);
        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    // DELETE: api/users/{id}/soft
    [HttpDelete("{id}/soft")]
    public async Task<IActionResult> SoftDeleteUser(Guid id)
    {
        var result = await _userService.SoftDeleteUserAsync(id);
        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    // POST: api/users/{id}/restore
    [HttpPost("{id}/restore")]
    public async Task<IActionResult> RestoreUser(Guid id)
    {
        var result = await _userService.RestoreUserAsync(id);
        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }
}
