using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for verifying Web3 wallet signatures
/// </summary>
public sealed class VerifyWeb3SignatureHandler(IAuthService authService) : ICommandHandler<VerifyWeb3SignatureCommand, SignInResponse>
{
    public async Task<SignInResponse> Handle(VerifyWeb3SignatureCommand command, CancellationToken cancellationToken)
    {
        var verifyRequest = new Web3VerificationRequest { WalletAddress = command.WalletAddress, Signature = command.Signature, Challenge = command.Nonce };

        return await authService.VerifyWeb3SignatureAsync(verifyRequest, cancellationToken).ConfigureAwait(false);
    }
}
