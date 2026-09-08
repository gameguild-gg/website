using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Command to verify Web3 signature and authenticate a user
/// </summary>
public class VerifyWeb3SignatureCommand : ICommand<SignInResponse>
{
    public string WalletAddress { get; set; } = string.Empty;

    public string Signature { get; set; } = string.Empty;

    public string Nonce { get; set; } = string.Empty;

    public string ChainId { get; set; } = string.Empty;

    public Guid? TenantId { get; set; }

    public string? DeviceFingerprint { get; set; }
}
