using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Request to disable MFA
/// </summary>
public class DisableMfaRequest
{
    /// <summary>
    ///     TOTP confirmation code
    /// </summary>
    [Required]
    public string Code { get; set; } = string.Empty;
}
