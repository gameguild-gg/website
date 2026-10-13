namespace GameGuild.Identity.Provisioning;

/// <summary>
///     Flattened, read-side projection of a provisioned user used by the SCIM list and
///     filter surface. ExternalId is null when a platform user has no SCIM mapping
///     (unmapped users are never returned by the SCIM surface; the projection keeps the
///     join shape simple). Declared with init properties (not a positional record) so
///     EF Core can bind composed Where/OrderBy/Skip/Take through the projection:
///     member assignments translate, positional-constructor projections do not.
/// </summary>
public sealed record ScimUserView
{
    public Guid UserId { get; init; }

    public string? ExternalId { get; init; }

    public string? UserName { get; init; }

    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? PhoneNumber { get; init; }

    public bool Active { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}

/// <summary>
///     Flattened, read-side projection of a provisioned group (tenant role). Same
///     init-property shape as <see cref="ScimUserView"/> for EF translatability.
/// </summary>
public sealed record ScimGroupView
{
    public Guid RoleId { get; init; }

    public string? ExternalId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool Active { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}
