using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

public sealed record CreateStepUpChallengeCommand(StepUpOperationBinding Binding) : ICommand<StepUpChallengeResponse>;
public sealed record BeginStepUpWebAuthnCommand(Guid ChallengeId) : ICommand<WebAuthnAuthenticationOptionsResult>;
public sealed record VerifyStepUpChallengeCommand(
    Guid ChallengeId,
    StepUpVerification Verification) : ICommand<StepUpReceiptResponse>;

public sealed class StepUpCommandHandler(IStepUpReceiptService stepUpService) :
    ICommandHandler<CreateStepUpChallengeCommand, StepUpChallengeResponse>,
    ICommandHandler<BeginStepUpWebAuthnCommand, WebAuthnAuthenticationOptionsResult>,
    ICommandHandler<VerifyStepUpChallengeCommand, StepUpReceiptResponse>
{
    public Task<StepUpChallengeResponse> Handle(
        CreateStepUpChallengeCommand command,
        CancellationToken cancellationToken) =>
        stepUpService.CreateChallengeAsync(command.Binding, cancellationToken);

    public Task<WebAuthnAuthenticationOptionsResult> Handle(
        BeginStepUpWebAuthnCommand command,
        CancellationToken cancellationToken) =>
        stepUpService.BeginWebAuthnAsync(command.ChallengeId, cancellationToken);

    public Task<StepUpReceiptResponse> Handle(
        VerifyStepUpChallengeCommand command,
        CancellationToken cancellationToken) =>
        stepUpService.VerifyAsync(command.ChallengeId, command.Verification, cancellationToken);
}
