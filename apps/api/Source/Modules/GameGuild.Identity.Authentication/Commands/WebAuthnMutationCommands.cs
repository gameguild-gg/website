using Microsoft.Extensions.Configuration;
using GameGuild.CQRS;
using GameGuild.Identity.Users;

namespace GameGuild.Identity.Authentication;

public sealed record BeginWebAuthnRegistrationCommand(
    Guid UserId,
    string Email,
    string DisplayName,
    WebAuthnAuthenticatorType? PreferredAuthenticatorType) : ICommand<WebAuthnRegistrationOptionsResult>;
public sealed record CompleteWebAuthnRegistrationCommand(
    Guid UserId,
    string AttestationResponse,
    string? FriendlyName,
    bool IsPasswordless,
    string? IpAddress,
    string UserAgent) : ICommand<WebAuthnRegistrationResult>;
public sealed record BeginWebAuthnAuthenticationCommand(string? Email) : ICommand<WebAuthnAuthenticationOptionsResult>;
public sealed record CompleteWebAuthnAuthenticationCommand(
    string AssertionResponse,
    string? IpAddress,
    string UserAgent) : ICommand<WebAuthnAuthenticationResult>;
public sealed record VerifyWebAuthnCredentialCommand(Guid UserId, Guid CredentialId) : ICommand<WebAuthnCredentialVerifyResult>;
public sealed record DeleteWebAuthnCredentialCommand(Guid UserId, Guid CredentialId) : ICommand<bool>;
public sealed record UpdateWebAuthnCredentialNameCommand(
    Guid UserId,
    Guid CredentialId,
    string FriendlyName) : ICommand<bool>;

public sealed class WebAuthnMutationCommandHandler(
    IWebAuthnService webAuthnService,
    IJwtTokenService jwtTokenService,
    IUserRepository userRepository,
    IConfiguration configuration) :
    ICommandHandler<BeginWebAuthnRegistrationCommand, WebAuthnRegistrationOptionsResult>,
    ICommandHandler<CompleteWebAuthnRegistrationCommand, WebAuthnRegistrationResult>,
    ICommandHandler<BeginWebAuthnAuthenticationCommand, WebAuthnAuthenticationOptionsResult>,
    ICommandHandler<CompleteWebAuthnAuthenticationCommand, WebAuthnAuthenticationResult>,
    ICommandHandler<VerifyWebAuthnCredentialCommand, WebAuthnCredentialVerifyResult>,
    ICommandHandler<DeleteWebAuthnCredentialCommand, bool>,
    ICommandHandler<UpdateWebAuthnCredentialNameCommand, bool>
{
    public Task<WebAuthnRegistrationOptionsResult> Handle(
        BeginWebAuthnRegistrationCommand command,
        CancellationToken cancellationToken) =>
        webAuthnService.BeginRegistrationAsync(
            command.UserId,
            command.Email,
            command.DisplayName,
            command.PreferredAuthenticatorType,
            cancellationToken);

    public Task<WebAuthnRegistrationResult> Handle(
        CompleteWebAuthnRegistrationCommand command,
        CancellationToken cancellationToken) =>
        webAuthnService.CompleteRegistrationAsync(
            command.UserId,
            command.AttestationResponse,
            command.FriendlyName,
            command.IsPasswordless,
            command.IpAddress,
            command.UserAgent,
            cancellationToken);

    public Task<WebAuthnAuthenticationOptionsResult> Handle(
        BeginWebAuthnAuthenticationCommand command,
        CancellationToken cancellationToken) =>
        webAuthnService.BeginAuthenticationAsync(command.Email, null, cancellationToken);

    public async Task<WebAuthnAuthenticationResult> Handle(
        CompleteWebAuthnAuthenticationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await webAuthnService.CompleteAuthenticationAsync(
            command.AssertionResponse,
            command.IpAddress,
            command.UserAgent,
            cancellationToken).ConfigureAwait(false);
        if (!result.Success || result.UserId is not { } userId)
            return result;

        var user = await userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return result;

        result.Email = user.Email;
        result.AccessToken = await jwtTokenService.GenerateAccessTokenAsync(
            user.Id,
            user.Email,
            [],
            null,
            user.TokenVersion,
            cancellationToken).ConfigureAwait(false);
        result.RefreshToken = await jwtTokenService.GenerateRefreshTokenAsync(
            user.Id,
            new DeviceInfo
            {
                Fingerprint = $"webauthn:{result.CredentialId?.ToString("N") ?? "unknown"}",
                IpAddress = command.IpAddress,
                UserAgent = command.UserAgent
            },
            cancellationToken).ConfigureAwait(false);

        var accessTokenMinutes = ParsePositiveInt(configuration["Jwt:AccessTokenExpirationMinutes"], 60);
        var refreshTokenDays = ParsePositiveInt(
            configuration["Jwt:RefreshTokenExpirationDays"] ?? configuration["Jwt:RefreshTokenExpiryInDays"],
            30);
        result.AccessTokenExpiresAt = SystemClock.UtcNow.AddMinutes(accessTokenMinutes);
        result.RefreshTokenExpiresAt = SystemClock.UtcNow.AddDays(refreshTokenDays);
        result.ExpiresIn = accessTokenMinutes * 60;
        return result;
    }

    public Task<WebAuthnCredentialVerifyResult> Handle(
        VerifyWebAuthnCredentialCommand command,
        CancellationToken cancellationToken) =>
        webAuthnService.VerifyCredentialAsync(command.UserId, command.CredentialId, cancellationToken);

    public Task<bool> Handle(DeleteWebAuthnCredentialCommand command, CancellationToken cancellationToken) =>
        webAuthnService.DeleteCredentialAsync(command.UserId, command.CredentialId, cancellationToken);

    public Task<bool> Handle(UpdateWebAuthnCredentialNameCommand command, CancellationToken cancellationToken) =>
        webAuthnService.UpdateCredentialNameAsync(
            command.UserId,
            command.CredentialId,
            command.FriendlyName,
            cancellationToken);

    private static int ParsePositiveInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}
