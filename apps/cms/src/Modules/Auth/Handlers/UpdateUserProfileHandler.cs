using MediatR;
using Microsoft.EntityFrameworkCore;
using GameGuild.Data;
using GameGuild.Modules.Auth.Commands;

namespace GameGuild.Modules.Auth.Handlers;

/// <summary>
/// Handler for updating user profile with business logic and validation
/// </summary>
public class UpdateUserProfileHandler : IRequestHandler<UpdateUserProfileCommand, User.Models.User>
{
    private readonly ApplicationDbContext _context;

    public UpdateUserProfileHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<User.Models.User> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        // Find the user
        User.Models.User? user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted, cancellationToken);

        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {request.UserId} not found");
        }

        // Business logic: Check if email is already taken by another user
        if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
        {
            bool emailExists = await _context.Users
                .AnyAsync(u => u.Email == request.Email && u.Id != request.UserId && !u.IsDeleted, cancellationToken);

            if (emailExists)
            {
                throw new InvalidOperationException($"Email '{request.Email}' is already taken by another user");
            }
        }

        // Update user properties
        if (!string.IsNullOrEmpty(request.Name))
        {
            user.Name = request.Name;
        }

        if (!string.IsNullOrEmpty(request.Email))
        {
            user.Email = request.Email;
        }

        if (request.IsActive.HasValue)
        {
            user.IsActive = request.IsActive.Value;
        }

        // Update the entity (triggers BaseEntity.Touch())
        user.Touch();

        await _context.SaveChangesAsync(cancellationToken);

        return user;
    }
}
