using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

public sealed record CreateStepUpChallengeCommand(StepUpOperationBinding Binding) : ICommand<StepUpChallengeResponse>;
public sealed record BeginStepUpWebAuthnCommand(Guid ChallengeId) : ICommand<WebAuthnAuthenticationOptionsResult>;
public sealed record VerifyStepUpChallengeCommand(Guid ChallengeId, StepUpVerification Verification) : ICommand<StepUpReceiptResponse>;

public sealed class StepUpCommandHandler(IStepUpReceiptService service) :
    ICommandHandler<CreateStepUpChallengeCommand, StepUpChallengeResponse>,
    ICommandHandler<BeginStepUpWebAuthnCommand, WebAuthnAuthenticationOptionsResult>,
    ICommandHandler<VerifyStepUpChallengeCommand, StepUpReceiptResponse>
{
    public Task<StepUpChallengeResponse> Handle(CreateStepUpChallengeCommand command, CancellationToken ct)
        => service.CreateChallengeAsync(command.Binding, ct);

    public Task<WebAuthnAuthenticationOptionsResult> Handle(BeginStepUpWebAuthnCommand command, CancellationToken ct)
        => service.BeginWebAuthnAsync(command.ChallengeId, ct);

    public Task<StepUpReceiptResponse> Handle(VerifyStepUpChallengeCommand command, CancellationToken ct)
        => service.VerifyAsync(command.ChallengeId, command.Verification, ct);
}
