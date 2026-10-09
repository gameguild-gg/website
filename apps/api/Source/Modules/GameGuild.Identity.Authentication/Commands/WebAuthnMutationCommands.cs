using Microsoft.Extensions.Configuration;
using GameGuild.CQRS;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Options;

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
public sealed record DeactivateWebAuthnCredentialCommand(Guid UserId, Guid CredentialId)
    : ICommand<WebAuthnCredentialTransitionResult>;
public sealed record ActivateWebAuthnCredentialCommand(Guid UserId, Guid CredentialId)
    : ICommand<WebAuthnCredentialTransitionResult>;
public sealed record UpdateWebAuthnCredentialNameCommand(
    Guid UserId,
    Guid CredentialId,
    string FriendlyName) : ICommand<bool>;

public sealed class WebAuthnMutationCommandHandler :
    ICommandHandler<BeginWebAuthnRegistrationCommand, WebAuthnRegistrationOptionsResult>,
    ICommandHandler<CompleteWebAuthnRegistrationCommand, WebAuthnRegistrationResult>,
    ICommandHandler<BeginWebAuthnAuthenticationCommand, WebAuthnAuthenticationOptionsResult>,
    ICommandHandler<CompleteWebAuthnAuthenticationCommand, WebAuthnAuthenticationResult>,
    ICommandHandler<VerifyWebAuthnCredentialCommand, WebAuthnCredentialVerifyResult>,
    ICommandHandler<DeleteWebAuthnCredentialCommand, bool>,
    ICommandHandler<DeactivateWebAuthnCredentialCommand, WebAuthnCredentialTransitionResult>,
    ICommandHandler<ActivateWebAuthnCredentialCommand, WebAuthnCredentialTransitionResult>,
    ICommandHandler<UpdateWebAuthnCredentialNameCommand, bool>
{
    private readonly IWebAuthnService webAuthnService;
    private readonly IUserRepository userRepository;
    private readonly IAuthenticatedSessionIssuer? sessionIssuer;

    public WebAuthnMutationCommandHandler(IWebAuthnService webAuthnService, IJwtTokenService jwtTokenService,
        IUserRepository userRepository, IConfiguration configuration)
        : this(webAuthnService, jwtTokenService, userRepository, configuration, null, null) { }

    public WebAuthnMutationCommandHandler(IWebAuthnService webAuthnService, IJwtTokenService jwtTokenService,
        IUserRepository userRepository, IConfiguration configuration, IOptions<JwtOptions>? jwtOptions)
        : this(webAuthnService, jwtTokenService, userRepository, configuration, jwtOptions, null) { }

    public WebAuthnMutationCommandHandler(
        IWebAuthnService webAuthnService,
        IJwtTokenService jwtTokenService,
        IUserRepository userRepository,
        IConfiguration configuration,
        IOptions<JwtOptions>? jwtOptions,
        IAuthenticatedSessionIssuer? sessionIssuer)
    {
        this.webAuthnService = webAuthnService;
        this.userRepository = userRepository;
        this.sessionIssuer = sessionIssuer;
    }

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
        cancellationToken.ThrowIfCancellationRequested();
        var result = await webAuthnService.CompleteAuthenticationAsync(
            command.AssertionResponse,
            command.IpAddress,
            command.UserAgent,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.Success)
        {
            return result;
        }
        if (result.UserId is not { } userId)
        {
            throw new AuthenticationRequiredException("Invalid WebAuthn authentication");
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (user is null || user.IsDeleted || !user.ValidateForAuthentication(user.TokenVersion).IsSuccess)
        {
            throw new AuthenticationRequiredException("Invalid WebAuthn authentication");
        }

        var issuer = sessionIssuer ?? throw new InvalidOperationException("Authenticated session issuer is not configured.");
        var issued = await issuer.IssueAsync(
            user,
            null,
            new DeviceInfo
            {
                Fingerprint = $"webauthn:{result.CredentialId?.ToString("N") ?? "unknown"}",
                IpAddress = command.IpAddress,
                UserAgent = command.UserAgent
            },
            cancellationToken).ConfigureAwait(false);
        result.Email = issued.Email;
        result.AccessToken = issued.AccessToken;
        result.RefreshToken = issued.RefreshToken;
        result.AccessTokenExpiresAt = issued.AccessTokenExpiresAt;
        result.RefreshTokenExpiresAt = issued.RefreshTokenExpiresAt;
        result.ExpiresIn = issued.ExpiresIn;
        return result;
    }

    public Task<WebAuthnCredentialVerifyResult> Handle(
        VerifyWebAuthnCredentialCommand command,
        CancellationToken cancellationToken) =>
        webAuthnService.VerifyCredentialAsync(command.UserId, command.CredentialId, cancellationToken);

    public Task<bool> Handle(DeleteWebAuthnCredentialCommand command, CancellationToken cancellationToken) =>
        webAuthnService.DeleteCredentialAsync(command.UserId, command.CredentialId, cancellationToken);

    public Task<WebAuthnCredentialTransitionResult> Handle(
        DeactivateWebAuthnCredentialCommand command,
        CancellationToken cancellationToken) =>
        webAuthnService.DeactivateCredentialAsync(command.UserId, command.CredentialId, cancellationToken);

    public Task<WebAuthnCredentialTransitionResult> Handle(
        ActivateWebAuthnCredentialCommand command,
        CancellationToken cancellationToken) =>
        webAuthnService.ActivateCredentialAsync(command.UserId, command.CredentialId, cancellationToken);

    public Task<bool> Handle(UpdateWebAuthnCredentialNameCommand command, CancellationToken cancellationToken) =>
        webAuthnService.UpdateCredentialNameAsync(
            command.UserId,
            command.CredentialId,
            command.FriendlyName,
            cancellationToken);

    private static int ParsePositiveInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}
