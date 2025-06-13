using MediatR;
using GameGuild.Modules.User.Queries;
using GameGuild.Modules.User.Services;

namespace GameGuild.Modules.User.Handlers;

/// <summary>
/// Handler for getting all users
/// </summary>
public class GetAllUsersHandler : IRequestHandler<GetAllUsersQuery, IEnumerable<Models.User>>
{
    private readonly IUserService _userService;

    public GetAllUsersHandler(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<IEnumerable<Models.User>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        if (request.IncludeDeleted)
        {
            return await _userService.GetDeletedUsersAsync();
        }

        return await _userService.GetAllUsersAsync();
    }
}
