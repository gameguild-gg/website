using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

public sealed record TerminateSessionCommand(Guid SessionId, SessionTerminationReason Reason) : ICommand<bool>;
public sealed record TerminateUserSessionsCommand(
    Guid UserId,
    SessionTerminationReason Reason,
    Guid? ExceptSessionId = null) : ICommand<int>;
public sealed record RefreshUserSessionCommand(Guid SessionId) : ICommand<bool>;
public sealed record TrustDeviceCommand(Guid UserId, Guid SessionId, string DeviceName) : ICommand<bool>;
public sealed record RevokeTrustedDeviceCommand(Guid UserId, Guid DeviceId) : ICommand<bool>;
/// <summary>
///     Names or renames one of the acting user's own active sessions. The acting user is bound
///     by the controller from the authenticated principal, never from the request body.
/// </summary>
public sealed record RenameSessionCommand(Guid UserId, Guid SessionId, string Name) : ICommand<bool>;

public sealed class SessionMutationCommandHandler(ISessionManagementService sessionService) :
    ICommandHandler<TerminateSessionCommand, bool>,
    ICommandHandler<TerminateUserSessionsCommand, int>,
    ICommandHandler<RefreshUserSessionCommand, bool>,
    ICommandHandler<TrustDeviceCommand, bool>,
    ICommandHandler<RevokeTrustedDeviceCommand, bool>,
    ICommandHandler<RenameSessionCommand, bool>
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

    public async Task<bool> Handle(TrustDeviceCommand command, CancellationToken cancellationToken)
    {
        if (command.UserId == Guid.Empty || command.SessionId == Guid.Empty)
        {
            return false;
        }

        // Resolve the signed session inside the command transaction. Transport metadata
        // and client-selected fingerprints cannot identify the device being trusted.
        var session = await sessionService.GetSessionAsync(command.SessionId, cancellationToken).ConfigureAwait(false);
        if (session is null || session.Id != command.SessionId || session.UserId != command.UserId ||
            !session.IsValid || session.TerminatedAt.HasValue || string.IsNullOrWhiteSpace(session.DeviceFingerprint))
        {
            return false;
        }

        return await sessionService.TrustDeviceAsync(
            command.UserId,
            session.DeviceFingerprint,
            command.DeviceName ?? string.Empty,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> Handle(RevokeTrustedDeviceCommand command, CancellationToken cancellationToken) =>
        sessionService.RevokeTrustedDeviceAsync(command.UserId, command.DeviceId, cancellationToken);

    public Task<bool> Handle(RenameSessionCommand command, CancellationToken cancellationToken) =>
        sessionService.RenameSessionAsync(command.UserId, command.SessionId, command.Name, cancellationToken);
}
