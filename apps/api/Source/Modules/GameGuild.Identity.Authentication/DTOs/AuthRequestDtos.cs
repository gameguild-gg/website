using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Request to verify email with token
/// </summary>
public sealed record VerifyEmailRequest
{
    /// <summary>
    ///     Verification token received via email
    /// </summary>
    [Required]
    public required string Token { get; init; }

    /// <summary>
    ///     Optional tenant context
    /// </summary>
    public Guid? TenantId { get; init; }
}

/// <summary>
///     Request to initiate password reset
/// </summary>
public sealed record RequestPasswordResetRequest
{
    /// <summary>
    ///     Email address to send reset link to
    /// </summary>
    [Required]
    [EmailAddress]
    public required string Email { get; init; }

    /// <summary>
    ///     Optional tenant context
    /// </summary>
    public Guid? TenantId { get; init; }
}

/// <summary>
///     Request to send a passwordless magic sign-in link.
/// </summary>
public sealed record RequestMagicLinkRequest
{
    /// <summary>
    ///     Email address to send the magic link to.
    /// </summary>
    [Required]
    [EmailAddress]
    public required string Email { get; init; }

    /// <summary>
    ///     Optional tenant context.
    /// </summary>
    public Guid? TenantId { get; init; }
}

/// <summary>
///     Request to consume a passwordless magic sign-in link.
/// </summary>
public sealed record ConsumeMagicLinkRequest
{
    /// <summary>
    ///     One-time token from the magic link.
    /// </summary>
    [Required]
    public required string Token { get; init; }

    /// <summary>
    ///     Optional tenant context.
    /// </summary>
    public Guid? TenantId { get; init; }

    /// <summary>
    ///     Optional device fingerprint for refresh-token session tracking.
    /// </summary>
    public string? DeviceFingerprint { get; init; }
}

/// <summary>
///     Request to complete password reset
/// </summary>
public sealed record CompletePasswordResetRequest
{
    /// <summary>
    ///     Reset token from email link
    /// </summary>
    [Required]
    public required string Token { get; init; }

    /// <summary>
    ///     New password
    /// </summary>
    [Required]
    [MinLength(8)]
    public required string NewPassword { get; init; }

    /// <summary>
    ///     Password confirmation
    /// </summary>
    [Required]
    [Compare(nameof(NewPassword))]
    public required string ConfirmPassword { get; init; }

    /// <summary>
    ///     Optional tenant context
    /// </summary>
    public Guid? TenantId { get; init; }
}

/// <summary>
///     Request to change password for authenticated user
/// </summary>
public sealed record PasswordChangeRequest
{
    /// <summary>
    ///     Current password for verification. Optional: omit or send empty when the account
    ///     has no password yet (OAuth-only) to set an initial password; any non-empty value
    ///     is always verified against the existing password hash.
    /// </summary>
    public string? CurrentPassword { get; init; }

    /// <summary>
    ///     New password
    /// </summary>
    [Required]
    [MinLength(8)]
    public required string NewPassword { get; init; }

    /// <summary>
    ///     Password confirmation
    /// </summary>
    [Required]
    [Compare(nameof(NewPassword))]
    public required string ConfirmPassword { get; init; }

    /// <summary>
    ///     Whether to revoke all other sessions
    /// </summary>
    public bool RevokeOtherSessions { get; init; } = true;
}

/// <summary>
///     Request to verify Web3 wallet signature
/// </summary>
public sealed record Web3VerifyRequest
{
    /// <summary>
    ///     Wallet address
    /// </summary>
    [Required]
    public required string WalletAddress { get; init; }

    /// <summary>
    /// Full SIWE challenge message returned by the challenge endpoint and signed by the wallet.
    /// </summary>
    [Required]
    public required string Challenge { get; init; }

    /// <summary>
    ///     EIP-191 signature over the SIWE challenge message
    /// </summary>
    [Required]
    public required string Signature { get; init; }

    /// <summary>
    ///     Nonce contained in the SIWE challenge message
    /// </summary>
    [Required]
    public required string Nonce { get; init; }

    /// <summary>
    ///     Blockchain chain ID
    /// </summary>
    [Required]
    public required string ChainId { get; init; }

    /// <summary>
    ///     Optional tenant context
    /// </summary>
    public Guid? TenantId { get; init; }

    /// <summary>
    ///     Device fingerprint for session tracking
    /// </summary>
    public string? DeviceFingerprint { get; init; }

    /// <summary>
    ///     When true, issues a persistent ("remember me") refresh token using the persistent lifetime.
    /// </summary>
    public bool? RememberMe { get; init; }
}
