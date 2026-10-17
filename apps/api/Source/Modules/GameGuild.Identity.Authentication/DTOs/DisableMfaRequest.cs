using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Request to disable MFA
/// </summary>
public class DisableMfaRequest
{
    /// <summary>
    ///     The account password, re-verified before MFA is disabled (issue #244).
    /// </summary>
    [Required]
    public string Password { get; set; } = string.Empty;
}
