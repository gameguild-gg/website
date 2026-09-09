using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

public sealed record TerminateSessionCommand(Guid SessionId, SessionTerminationReason Reason) : ICommand<bool>;
public sealed record TerminateUserSessionsCommand(
    Guid UserId,
    SessionTerminationReason Reason,
    Guid? ExceptSessionId = null) : ICommand<int>;
public sealed record RefreshUserSessionCommand(Guid SessionId) : ICommand<bool>;
public sealed record TrustDeviceCommand(Guid UserId, string DeviceFingerprint, string DeviceName) : ICommand<bool>;
public sealed record RevokeTrustedDeviceCommand(Guid UserId, Guid DeviceId) : ICommand<bool>;

public sealed class SessionMutationCommandHandler(ISessionManagementService sessionService) :
    ICommandHandler<TerminateSessionCommand, bool>,
    ICommandHandler<TerminateUserSessionsCommand, int>,
    ICommandHandler<RefreshUserSessionCommand, bool>,
    ICommandHandler<TrustDeviceCommand, bool>,
    ICommandHandler<RevokeTrustedDeviceCommand, bool>
{
    public Task<bool> Handle(TerminateSessionCommand command, CancellationToken cancellationToken) =>
        sessionService.TerminateSessionAsync(command.SessionId, command.Reason, cancellationToken);

    public Task<int> Handle(TerminateUserSessionsCommand command, CancellationToken cancellationToken) =>
        sessionService.TerminateAllUserSessionsAsync(
            command.UserId,
            command.Reason,
            command.ExceptSessionId,
            cancellationToken);

    public Task<bool> Handle(RefreshUserSessionCommand command, CancellationToken cancellationToken) =>
        sessionService.RefreshSessionAsync(command.SessionId, cancellationToken);

    public Task<bool> Handle(TrustDeviceCommand command, CancellationToken cancellationToken) =>
        sessionService.TrustDeviceAsync(
            command.UserId,
            command.DeviceFingerprint,
            command.DeviceName,
            cancellationToken);

    public Task<bool> Handle(RevokeTrustedDeviceCommand command, CancellationToken cancellationToken) =>
        sessionService.RevokeTrustedDeviceAsync(command.UserId, command.DeviceId, cancellationToken);
}
