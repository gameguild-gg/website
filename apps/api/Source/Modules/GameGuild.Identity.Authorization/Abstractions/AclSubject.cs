namespace GameGuild.Identity.Authorization;

/// <summary>
///     Represents the subject (who) in an ACL evaluation.
///     Supports multiple principal types: User, Role, Group, and Anonymous.
/// </summary>
public sealed record AclSubject
{
    /// <summary>
    ///     Gets whether the subject is authenticated.
    /// </summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>
    ///     Gets the user ID, if authenticated.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    ///     Gets the role IDs the user belongs to.
    /// </summary>
    public IReadOnlyCollection<Guid> RoleIds { get; init; } = [];

    /// <summary>
    ///     Gets the group IDs the user belongs to.
    /// </summary>
    public IReadOnlyCollection<Guid> GroupIds { get; init; } = [];

    /// <summary>
    ///     Creates an anonymous (unauthenticated) subject.
    /// </summary>
    public static AclSubject Anonymous => new() { IsAuthenticated = false };

    /// <summary>
    ///     Creates an authenticated subject for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="roleIds">Optional role IDs.</param>
    /// <param name="groupIds">Optional group IDs.</param>
    /// <returns>An authenticated ACL subject.</returns>
    public static AclSubject ForUser(
        Guid userId,
        IReadOnlyCollection<Guid>? roleIds = null,
        IReadOnlyCollection<Guid>? groupIds = null)
    {
        return new AclSubject
        {
            IsAuthenticated = true,
            UserId = userId,
            RoleIds = roleIds ?? [],
            GroupIds = groupIds ?? []
        };
    }

    /// <summary>
    ///     Gets all principal identifiers that this subject matches.
    ///     Used for ACL evaluation.
    /// </summary>
    /// <returns>List of (PrincipalType, PrincipalId) tuples.</returns>
    public IEnumerable<(AclPrincipalType Type, Guid? Id)> GetPrincipals()
    {
        // Anonymous principal (for public resources)
        yield return (AclPrincipalType.Anonymous, null);

        if (!IsAuthenticated)
        {
            yield break;
        }

        // User principal
        if (UserId.HasValue)
        {
            yield return (AclPrincipalType.User, UserId.Value);
        }

        // Role principals
        foreach (var roleId in RoleIds)
        {
            yield return (AclPrincipalType.Role, roleId);
        }

        // Group principals
        foreach (var groupId in GroupIds)
        {
            yield return (AclPrincipalType.Group, groupId);
        }
    }
}

/// <summary>
///     Types of principals that can be granted access via ACL.
/// </summary>
public enum AclPrincipalType
{
    /// <summary>
    ///     Anonymous/unauthenticated users (for public resources).
    /// </summary>
    Anonymous = 0,

    /// <summary>
    ///     A specific user.
    /// </summary>
    User = 1,

    /// <summary>
    ///     A role (all users with this role).
    /// </summary>
    Role = 2,

    /// <summary>
    ///     A group (all users in this group).
    /// </summary>
    Group = 3
}
