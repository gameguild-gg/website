using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

public sealed class StartMfaSignInEnrollmentRequest
{
    [Required, StringLength(43, MinimumLength = 43)]
    public string MfaToken { get; init; } = string.Empty;
}
