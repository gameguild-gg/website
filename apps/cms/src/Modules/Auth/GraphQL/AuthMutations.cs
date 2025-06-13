using MediatR;
using GameGuild.Modules.Auth.Commands;
using GameGuild.Modules.Auth.Dtos;
using GameGuild.Modules.User.GraphQL;

namespace GameGuild.Modules.Auth.GraphQL;

/// <summary>
/// GraphQL mutations for Auth module using CQRS pattern
/// </summary>
[ExtendObjectType<Mutation>]
public class AuthMutations
{
    /// <summary>
    /// Local sign-up mutation using CQRS
    /// </summary>
    public async Task<SignInResponseDto> LocalSignUp(
        LocalSignUpRequestDto input,
        [Service] IMediator mediator)
    {
        var command = new LocalSignUpCommand
        {
            Email = input.Email, Password = input.Password, Username = input.Username
        };

        return await mediator.Send(command);
    }

    /// <summary>
    /// Local sign-in mutation using CQRS
    /// </summary>
    public async Task<SignInResponseDto> LocalSignIn(
        LocalSignInRequestDto input,
        [Service] IMediator mediator)
    {
        var command = new LocalSignInCommand
        {
            Email = input.Email, Password = input.Password
        };

        return await mediator.Send(command);
    }

    /// <summary>
    /// Generate Web3 challenge using CQRS
    /// </summary>
    public async Task<Web3ChallengeResponseDto> GenerateWeb3Challenge(
        Web3ChallengeRequestDto input,
        [Service] IMediator mediator)
    {
        var command = new GenerateWeb3ChallengeCommand
        {
            WalletAddress = input.WalletAddress, ChainId = input.ChainId
        };

        return await mediator.Send(command);
    }

    /// <summary>
    /// Verify Web3 signature using CQRS
    /// </summary>
    public async Task<SignInResponseDto> VerifyWeb3Signature(
        Web3VerifyRequestDto input,
        [Service] IMediator mediator)
    {
        var command = new VerifyWeb3SignatureCommand
        {
            WalletAddress = input.WalletAddress, Signature = input.Signature, Nonce = input.Nonce, ChainId = input.ChainId
        };

        return await mediator.Send(command);
    }
}
