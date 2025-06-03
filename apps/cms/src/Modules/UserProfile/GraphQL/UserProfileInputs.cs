namespace cms.Modules.UserProfile.GraphQL;

/// <summary>
/// Input for creating a user profile
/// </summary>
public class CreateUserProfileInput
{
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? DisplayName { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Slug { get; set; }
    public Guid? TenantId { get; set; }
}

/// <summary>
/// Input for updating a user profile
/// </summary>
public class UpdateUserProfileInput
{
    public Guid Id { get; set; }
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? DisplayName { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Slug { get; set; }
    public Guid? TenantId { get; set; }
}
