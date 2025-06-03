using Microsoft.AspNetCore.Mvc;
using cms.Modules.UserProfile.Services;
using cms.Modules.UserProfile.Dtos;

namespace cms.Modules.UserProfile.Controllers;

[ApiController]
[Route("[controller]")]
public class UserProfilesController : ControllerBase
{
    private readonly IUserProfileService _userProfileService;

    public UserProfilesController(IUserProfileService userProfileService)
    {
        _userProfileService = userProfileService;
    }

    // GET: user-profiles
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserProfileResponseDto>>> GetUserProfiles()
    {
        var userProfiles = await _userProfileService.GetAllUserProfilesAsync();
        var userProfileDtos = userProfiles.Select(up => new UserProfileResponseDto
            {
                Id = up.Id,
                Version = up.Version,
                GivenName = up.GivenName,
                FamilyName = up.FamilyName,
                DisplayName = up.DisplayName,
                Title = up.Title,
                Description = up.Description,
                CreatedAt = up.CreatedAt,
                UpdatedAt = up.UpdatedAt,
                DeletedAt = up.DeletedAt,
                IsDeleted = up.IsDeleted
            }
        );

        return Ok(userProfileDtos);
    }

    // GET: user-profiles/deleted
    [HttpGet("deleted")]
    public async Task<ActionResult<IEnumerable<UserProfileResponseDto>>> GetDeletedUserProfiles()
    {
        var userProfiles = await _userProfileService.GetDeletedUserProfilesAsync();
        var userProfileDtos = userProfiles.Select(up => new UserProfileResponseDto
            {
                Id = up.Id,
                Version = up.Version,
                GivenName = up.GivenName,
                FamilyName = up.FamilyName,
                DisplayName = up.DisplayName,
                Title = up.Title,
                Description = up.Description,
                CreatedAt = up.CreatedAt,
                UpdatedAt = up.UpdatedAt,
                DeletedAt = up.DeletedAt,
                IsDeleted = up.IsDeleted
            }
        );

        return Ok(userProfileDtos);
    }

    // GET: user-profiles/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<UserProfileResponseDto>> GetUserProfile(Guid id)
    {
        var userProfile = await _userProfileService.GetUserProfileByIdAsync(id);

        if (userProfile == null)
        {
            return NotFound();
        }

        var userProfileDto = new UserProfileResponseDto
        {
            Id = userProfile.Id,
            Version = userProfile.Version,
            GivenName = userProfile.GivenName,
            FamilyName = userProfile.FamilyName,
            DisplayName = userProfile.DisplayName,
            Title = userProfile.Title,
            Description = userProfile.Description,
            CreatedAt = userProfile.CreatedAt,
            UpdatedAt = userProfile.UpdatedAt,
            DeletedAt = userProfile.DeletedAt,
            IsDeleted = userProfile.IsDeleted
        };

        return Ok(userProfileDto);
    }

    // GET: user-profiles/user/{userId}
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<UserProfileResponseDto>> GetUserProfileByUserId(Guid userId)
    {
        var userProfile = await _userProfileService.GetUserProfileByUserIdAsync(userId);

        if (userProfile == null)
        {
            return NotFound();
        }

        var userProfileDto = new UserProfileResponseDto
        {
            Id = userProfile.Id,
            Version = userProfile.Version,
            GivenName = userProfile.GivenName,
            FamilyName = userProfile.FamilyName,
            DisplayName = userProfile.DisplayName,
            Title = userProfile.Title,
            Description = userProfile.Description,
            CreatedAt = userProfile.CreatedAt,
            UpdatedAt = userProfile.UpdatedAt,
            DeletedAt = userProfile.DeletedAt,
            IsDeleted = userProfile.IsDeleted
        };

        return Ok(userProfileDto);
    }

    // POST: user-profiles
    [HttpPost]
    public async Task<ActionResult<UserProfileResponseDto>> CreateUserProfile(CreateUserProfileDto createUserProfileDto)
    {
        var userProfile = new Models.UserProfile
        {
            GivenName = createUserProfileDto.GivenName,
            FamilyName = createUserProfileDto.FamilyName,
            DisplayName = createUserProfileDto.DisplayName,
            Title = createUserProfileDto.Title ?? string.Empty,
            Description = createUserProfileDto.Description,
        };

        var createdUserProfile = await _userProfileService.CreateUserProfileAsync(userProfile);

        var userProfileDto = new UserProfileResponseDto
        {
            Id = createdUserProfile.Id,
            Version = createdUserProfile.Version,
            GivenName = createdUserProfile.GivenName,
            FamilyName = createdUserProfile.FamilyName,
            DisplayName = createdUserProfile.DisplayName,
            Title = createdUserProfile.Title,
            Description = createdUserProfile.Description,
            CreatedAt = createdUserProfile.CreatedAt,
            UpdatedAt = createdUserProfile.UpdatedAt,
            DeletedAt = createdUserProfile.DeletedAt,
            IsDeleted = createdUserProfile.IsDeleted
        };

        return CreatedAtAction(
            nameof(GetUserProfile),
            new
            {
                id = createdUserProfile.Id
            },
            userProfileDto
        );
    }

    // PUT: user-profiles/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<UserProfileResponseDto>> UpdateUserProfile(Guid id, UpdateUserProfileDto updateUserProfileDto)
    {
        var userProfile = new Models.UserProfile
        {
            GivenName = updateUserProfileDto.GivenName,
            FamilyName = updateUserProfileDto.FamilyName,
            DisplayName = updateUserProfileDto.DisplayName,
            Title = updateUserProfileDto.Title ?? string.Empty,
            Description = updateUserProfileDto.Description,

        };

        var updatedUserProfile = await _userProfileService.UpdateUserProfileAsync(id, userProfile);

        if (updatedUserProfile == null)
        {
            return NotFound();
        }

        var userProfileDto = new UserProfileResponseDto
        {
            Id = updatedUserProfile.Id,
            Version = updatedUserProfile.Version,
            GivenName = updatedUserProfile.GivenName,
            FamilyName = updatedUserProfile.FamilyName,
            DisplayName = updatedUserProfile.DisplayName,
            Title = updatedUserProfile.Title,
            Description = updatedUserProfile.Description,
            CreatedAt = updatedUserProfile.CreatedAt,
            UpdatedAt = updatedUserProfile.UpdatedAt,
            DeletedAt = updatedUserProfile.DeletedAt,
            IsDeleted = updatedUserProfile.IsDeleted
        };

        return Ok(userProfileDto);
    }

    // DELETE: user-profiles/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUserProfile(Guid id)
    {
        var result = await _userProfileService.DeleteUserProfileAsync(id);

        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    // DELETE: user-profiles/{id}/soft
    [HttpDelete("{id}/soft")]
    public async Task<IActionResult> SoftDeleteUserProfile(Guid id)
    {
        var result = await _userProfileService.SoftDeleteUserProfileAsync(id);

        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    // POST: user-profiles/{id}/restore
    [HttpPost("{id}/restore")]
    public async Task<IActionResult> RestoreUserProfile(Guid id)
    {
        var result = await _userProfileService.RestoreUserProfileAsync(id);

        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }
}
