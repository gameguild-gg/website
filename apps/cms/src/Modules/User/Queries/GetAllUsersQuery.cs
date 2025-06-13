using MediatR;

namespace GameGuild.Modules.User.Queries;

/// <summary>
/// Query to get all active users
/// </summary>
public class GetAllUsersQuery : IRequest<IEnumerable<Models.User>>
{
    public bool IncludeDeleted { get; set; } = false;
}
