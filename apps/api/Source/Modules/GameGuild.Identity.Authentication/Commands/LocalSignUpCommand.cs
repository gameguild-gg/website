using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Command to handle local user sign-up
/// </summary>
public class LocalSignUpCommand : ICommand<SignInResponse>
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public Guid? TenantId { get; set; }

    public string? PhoneNumber { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    /// <summary>Optional guest token to upgrade into the issued session (issue #213).</summary>
    public string? GuestAccessToken { get; set; }
}
