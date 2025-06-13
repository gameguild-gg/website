using MediatR;
using Microsoft.EntityFrameworkCore;
using GameGuild.Data;
using GameGuild.Modules.Auth.Queries;

namespace GameGuild.Modules.Auth.Handlers;

/// <summary>
/// Handler for getting user by email query
/// </summary>
public class GetUserByEmailHandler : IRequestHandler<GetUserByEmailQuery, User.Models.User?>
{
    private readonly ApplicationDbContext _context;

    public GetUserByEmailHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<User.Models.User?> Handle(GetUserByEmailQuery request, CancellationToken cancellationToken)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted, cancellationToken);
    }
}
