using GameGuild.Modules.User.GraphQL;
using GameGuild.Modules.UserProfile.Services;

namespace GameGuild.Modules.UserProfile.GraphQL;

/// <summary>
/// GraphQL mutations for UserProfile module
/// </summary>
[ExtendObjectType<Mutation>]
public class UserProfileMutations
{
    /// <summary>
    /// Create a new user profile
    /// </summary>
    public async Task<Models.UserProfile> CreateUserProfile(
        [Service] IUserProfileService userProfileService,
        CreateUserProfileInput input)
    {
        var userProfile = new Models.UserProfile
        {
            GivenName = input.GivenName,
            FamilyName = input.FamilyName,
            DisplayName = input.DisplayName,
            Title = input.Title ?? string.Empty,
            Description = input.Description,
        };

        // Handle tenant assignment if provided
        if (input.TenantId.HasValue)
        {
            // We would need to get the tenant from the context and assign it
            // For now, we'll leave the tenant assignment to be handled by the service layer
        }

        return await userProfileService.CreateUserProfileAsync(userProfile);
    }

    /// <summary>
    /// Update an existing user profile
    /// </summary>
    public async Task<Models.UserProfile?> UpdateUserProfile(
        [Service] IUserProfileService userProfileService,
        UpdateUserProfileInput input)
    {
        var userProfile = new Models.UserProfile
        {
            Id = input.Id,
            GivenName = input.GivenName,
            FamilyName = input.FamilyName,
            DisplayName = input.DisplayName,
            Title = input.Title ?? string.Empty,
            Description = input.Description,

        };

        // Handle tenant assignment if provided
        if (input.TenantId.HasValue)
        {
            // We would need to get the tenant from the context and assign it
            // For now, we'll leave the tenant assignment to be handled by the service layer
        }

        return await userProfileService.UpdateUserProfileAsync(input.Id, userProfile);
    }

    /// <summary>
    /// Soft delete a user profile
    /// </summary>
    public async Task<bool> SoftDeleteUserProfile(
        [Service] IUserProfileService userProfileService,
        Guid id)
    {
        return await userProfileService.SoftDeleteUserProfileAsync(id);
    }

    /// <summary>
    /// Restore a soft-deleted user profile
    /// </summary>
    public async Task<bool> RestoreUserProfile(
        [Service] IUserProfileService userProfileService,
        Guid id)
    {
        return await userProfileService.RestoreUserProfileAsync(id);
    }

    /// <summary>
    /// Hard delete a user profile
    /// </summary>
    public async Task<bool> DeleteUserProfile(
        [Service] IUserProfileService userProfileService,
        Guid id)
    {
        return await userProfileService.DeleteUserProfileAsync(id);
    }
}
