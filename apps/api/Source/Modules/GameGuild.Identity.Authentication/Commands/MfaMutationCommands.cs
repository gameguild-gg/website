using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

public sealed record InitiateMfaSetupCommand(Guid UserId, string UserEmail) : ICommand<MfaSetupResult>;
public sealed record CompleteMfaSetupCommand(Guid UserId, string Code) : ICommand<MfaVerificationResult>;
public sealed record VerifyMfaCodeCommand(Guid UserId, string Code, MfaMethod Method) : ICommand<MfaVerificationResult>;
public sealed record RegenerateMfaBackupCodesCommand(Guid UserId) : ICommand<string[]>;
public sealed record DisableMfaCommand(Guid UserId, string ConfirmationCode) : ICommand<bool>;

public sealed class MfaMutationCommandHandler(IMfaService mfaService) :
    ICommandHandler<InitiateMfaSetupCommand, MfaSetupResult>,
    ICommandHandler<CompleteMfaSetupCommand, MfaVerificationResult>,
    ICommandHandler<VerifyMfaCodeCommand, MfaVerificationResult>,
    ICommandHandler<RegenerateMfaBackupCodesCommand, string[]>,
    ICommandHandler<DisableMfaCommand, bool>
{
    public Task<MfaSetupResult> Handle(InitiateMfaSetupCommand command, CancellationToken cancellationToken) =>
        mfaService.InitiateMfaSetupAsync(command.UserId, command.UserEmail, cancellationToken);

    public Task<MfaVerificationResult> Handle(CompleteMfaSetupCommand command, CancellationToken cancellationToken) =>
        mfaService.CompleteMfaSetupAsync(command.UserId, command.Code, cancellationToken);

    public Task<MfaVerificationResult> Handle(VerifyMfaCodeCommand command, CancellationToken cancellationToken) =>
        mfaService.VerifyMfaAsync(command.UserId, command.Code, command.Method, cancellationToken);

    public Task<string[]> Handle(RegenerateMfaBackupCodesCommand command, CancellationToken cancellationToken) =>
        mfaService.GenerateBackupCodesAsync(command.UserId, cancellationToken);

    public Task<bool> Handle(DisableMfaCommand command, CancellationToken cancellationToken) =>
        mfaService.DisableMfaAsync(command.UserId, command.ConfirmationCode, cancellationToken);
}
