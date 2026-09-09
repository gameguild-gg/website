using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

public sealed record CreateServiceAccountCommand(
    string Name,
    string? Description,
    Guid? TenantId,
    string Scopes,
    string CreatedBy,
    string? AllowedIpAddresses,
    DateTime? ExpiresAt) : ICommand<(ServiceAccount Account, string ClientSecret)>;
public sealed record PatchServiceAccountCommand(
    Guid ServiceAccountId,
    string? Name,
    string? Description,
    string? Scopes,
    DateTime? ExpiresAt) : ICommand<bool>;
public sealed record RotateServiceAccountSecretCommand(Guid ServiceAccountId) : ICommand<string>;
public sealed record UnlockServiceAccountCommand(Guid ServiceAccountId) : ICommand;
public sealed record LockServiceAccountCommand(Guid ServiceAccountId, string Reason) : ICommand;
public sealed record DeactivateServiceAccountCommand(Guid ServiceAccountId) : ICommand;
public sealed record ReactivateServiceAccountCommand(Guid ServiceAccountId) : ICommand;
public sealed record UpdateServiceAccountScopesCommand(Guid ServiceAccountId, string Scopes) : ICommand;
public sealed record IssueServiceAccountTokenCommand(
    string ClientId,
    string ClientSecret,
    string? IpAddress) : ICommand<ServiceAccountTokenCommandResult>;
public sealed record ServiceAccountTokenCommandResult(
    ServiceAccount? Account,
    string? AccessToken,
    DateTime? ExpiresAt);

public sealed class ServiceAccountMutationCommandHandler(
    IServiceAccountService serviceAccountService,
    IJwtTokenService jwtTokenService) :
    ICommandHandler<CreateServiceAccountCommand, (ServiceAccount Account, string ClientSecret)>,
    ICommandHandler<PatchServiceAccountCommand, bool>,
    ICommandHandler<RotateServiceAccountSecretCommand, string>,
    ICommandHandler<UnlockServiceAccountCommand>,
    ICommandHandler<LockServiceAccountCommand>,
    ICommandHandler<DeactivateServiceAccountCommand>,
    ICommandHandler<ReactivateServiceAccountCommand>,
    ICommandHandler<UpdateServiceAccountScopesCommand>,
    ICommandHandler<IssueServiceAccountTokenCommand, ServiceAccountTokenCommandResult>
{
    public Task<(ServiceAccount Account, string ClientSecret)> Handle(
        CreateServiceAccountCommand command,
        CancellationToken cancellationToken) =>
        serviceAccountService.CreateServiceAccountAsync(
            command.Name,
            command.Description,
            command.TenantId,
            command.Scopes,
            command.CreatedBy,
            command.AllowedIpAddresses,
            command.ExpiresAt,
            cancellationToken);

    public async Task<bool> Handle(PatchServiceAccountCommand command, CancellationToken cancellationToken)
    {
        var account = await serviceAccountService.GetByIdAsync(command.ServiceAccountId, cancellationToken)
            .ConfigureAwait(false);
        if (account is null)
            return false;

        if (!string.IsNullOrEmpty(command.Name))
            account.Name = command.Name;
        if (command.Description is not null)
            account.Description = command.Description;
        if (!string.IsNullOrEmpty(command.Scopes))
            await serviceAccountService.UpdateScopesAsync(
                command.ServiceAccountId,
                command.Scopes,
                cancellationToken).ConfigureAwait(false);
        if (command.ExpiresAt.HasValue)
            account.ExpiresAt = command.ExpiresAt.Value;
        return true;
    }

    public Task<string> Handle(RotateServiceAccountSecretCommand command, CancellationToken cancellationToken) =>
        serviceAccountService.RotateSecretAsync(command.ServiceAccountId, cancellationToken);

    public async Task<Unit> Handle(UnlockServiceAccountCommand command, CancellationToken cancellationToken)
    {
        await serviceAccountService.UnlockAsync(command.ServiceAccountId, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }

    public async Task<Unit> Handle(LockServiceAccountCommand command, CancellationToken cancellationToken)
    {
        await serviceAccountService.LockAsync(command.ServiceAccountId, command.Reason, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeactivateServiceAccountCommand command, CancellationToken cancellationToken)
    {
        await serviceAccountService.DeactivateAsync(command.ServiceAccountId, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }

    public async Task<Unit> Handle(ReactivateServiceAccountCommand command, CancellationToken cancellationToken)
    {
        await serviceAccountService.ReactivateAsync(command.ServiceAccountId, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }

    public async Task<Unit> Handle(UpdateServiceAccountScopesCommand command, CancellationToken cancellationToken)
    {
        await serviceAccountService.UpdateScopesAsync(
            command.ServiceAccountId,
            command.Scopes,
            cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }

    public async Task<ServiceAccountTokenCommandResult> Handle(
        IssueServiceAccountTokenCommand command,
        CancellationToken cancellationToken)
    {
        var account = await serviceAccountService.AuthenticateAsync(
            command.ClientId,
            command.ClientSecret,
            command.IpAddress,
            cancellationToken).ConfigureAwait(false);
        if (account is null)
            return new(null, null, null);

        var (token, expiresAt) = await jwtTokenService.GenerateServiceAccountTokenAsync(
            account.Id.ToString(),
            account.ClientId,
            account.Name,
            account.GetScopesSet(),
            account.TenantId,
            cancellationToken).ConfigureAwait(false);
        return new(account, token, expiresAt);
    }
}
