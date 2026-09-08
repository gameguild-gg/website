using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

public sealed record RotateSigningKeyCommand(string Reason, int ValidityDays) : ICommand<JwtSigningKey>;
public sealed record CleanupExpiredSigningKeysCommand(int RetentionDays) : ICommand<int>;

public sealed class KeyRotationCommandHandler(IKeyRotationService keyRotationService) :
    ICommandHandler<RotateSigningKeyCommand, JwtSigningKey>,
    ICommandHandler<CleanupExpiredSigningKeysCommand, int>
{
    public Task<JwtSigningKey> Handle(RotateSigningKeyCommand command, CancellationToken cancellationToken) =>
        keyRotationService.RotateKeyAsync(command.Reason, command.ValidityDays, cancellationToken);

    public Task<int> Handle(CleanupExpiredSigningKeysCommand command, CancellationToken cancellationToken) =>
        keyRotationService.CleanupExpiredKeysAsync(command.RetentionDays, cancellationToken);
}
