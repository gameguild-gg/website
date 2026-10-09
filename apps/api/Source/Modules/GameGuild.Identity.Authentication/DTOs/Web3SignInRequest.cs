using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Request for Web3 sign-in
/// </summary>
public class Web3SignInRequest
{
    [Required]
    public string WalletAddress { get; set; } = string.Empty;

    [Required]
    public string Signature { get; set; } = string.Empty;

    [Required]
    public string Nonce { get; set; } = string.Empty;

    public string ChainId { get; set; } = "1";

    public Guid? TenantId { get; set; }

    /// <summary>When true, issues a persistent ("remember me") refresh token using the persistent lifetime.</summary>
    public bool? RememberMe { get; set; }
}
