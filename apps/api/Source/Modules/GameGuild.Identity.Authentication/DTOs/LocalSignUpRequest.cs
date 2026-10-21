using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Request DTO for local sign-up
/// </summary>
public class LocalSignUpRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    ///     Optional tenant ID to use for the sign-up. If not provided, a default tenant may be assigned
    /// </summary>
    public Guid? TenantId { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    [Phone]
    public string? PhoneNumber { get; set; }

    /// <summary>
    ///     Optional guest bearer token presented by an anonymous visitor registering
    ///     (issue #213). A valid guest session is upgraded into the issued authenticated
    ///     session (session continuity); an invalid token never blocks registration.
    /// </summary>
    public string? GuestAccessToken { get; set; }
}
