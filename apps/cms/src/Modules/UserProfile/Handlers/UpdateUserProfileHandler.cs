using MediatR;
using Microsoft.EntityFrameworkCore;
using GameGuild.Data;
using GameGuild.Modules.UserProfile.Commands;

namespace GameGuild.Modules.UserProfile.Handlers;

/// <summary>
/// Handler for updating user profile with business logic
/// </summary>
public class UpdateUserProfileHandler : IRequestHandler<UpdateUserProfileCommand, Models.UserProfile>
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UpdateUserProfileHandler> _logger;

    public UpdateUserProfileHandler(ApplicationDbContext context, ILogger<UpdateUserProfileHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Models.UserProfile> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        Models.UserProfile? userProfile = await _context.Resources
            .OfType<Models.UserProfile>()
            .FirstOrDefaultAsync(up => up.Id == request.UserProfileId && !up.IsDeleted, cancellationToken);

        if (userProfile == null)
        {
            throw new InvalidOperationException($"User profile with ID {request.UserProfileId} not found");
        }

        // Update profile properties - only the ones that exist in UserProfile model
        if (request.GivenName != null) userProfile.GivenName = request.GivenName;
        if (request.FamilyName != null) userProfile.FamilyName = request.FamilyName;
        if (request.DisplayName != null) userProfile.DisplayName = request.DisplayName;

        // Update timestamps
        userProfile.Touch();

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User profile {UserProfileId} updated successfully", request.UserProfileId);

        return userProfile;
    }
}
