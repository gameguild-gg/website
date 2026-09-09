using GameGuild.CQRS;
using Moq;
using Microsoft.Extensions.Configuration;
using GameGuild.Identity.Users;

namespace GameGuild.Identity.Authentication.UnitTests.Infrastructure;

internal sealed class IdentityCommandTestSender : ISender
{
    private readonly KeyRotationCommandHandler? _keyRotationHandler;
    private readonly MfaMutationCommandHandler? _mfaHandler;
    private readonly ServiceAccountMutationCommandHandler? _serviceAccountHandler;
    private readonly SessionMutationCommandHandler? _sessionHandler;
    private readonly StepUpCommandHandler? _stepUpHandler;
    private readonly WebAuthnMutationCommandHandler? _webAuthnHandler;

    private IdentityCommandTestSender(
        KeyRotationCommandHandler? keyRotationHandler = null,
        MfaMutationCommandHandler? mfaHandler = null,
        ServiceAccountMutationCommandHandler? serviceAccountHandler = null,
        SessionMutationCommandHandler? sessionHandler = null,
        StepUpCommandHandler? stepUpHandler = null,
        WebAuthnMutationCommandHandler? webAuthnHandler = null)
    {
        _keyRotationHandler = keyRotationHandler;
        _mfaHandler = mfaHandler;
        _serviceAccountHandler = serviceAccountHandler;
        _sessionHandler = sessionHandler;
        _stepUpHandler = stepUpHandler;
        _webAuthnHandler = webAuthnHandler;
    }

    public static IdentityCommandTestSender ForKeyRotation(IKeyRotationService service) =>
        new(keyRotationHandler: new KeyRotationCommandHandler(service));

    public static IdentityCommandTestSender ForMfa(IMfaService service) =>
        new(mfaHandler: new MfaMutationCommandHandler(service));

    public static IdentityCommandTestSender ForServiceAccounts(
        IServiceAccountService service,
        IJwtTokenService? jwtTokenService = null) =>
        new(serviceAccountHandler: new ServiceAccountMutationCommandHandler(
            service,
            jwtTokenService ?? Mock.Of<IJwtTokenService>()));

    public static IdentityCommandTestSender ForSessions(ISessionManagementService service) =>
        new(sessionHandler: new SessionMutationCommandHandler(service));

    public static IdentityCommandTestSender ForStepUp(IStepUpReceiptService service) =>
        new(stepUpHandler: new StepUpCommandHandler(service));

    public static IdentityCommandTestSender ForWebAuthn(
        IWebAuthnService service,
        IJwtTokenService? jwtTokenService = null,
        IUserRepository? userRepository = null,
        IConfiguration? configuration = null) =>
        new(webAuthnHandler: new WebAuthnMutationCommandHandler(
            service,
            jwtTokenService ?? Mock.Of<IJwtTokenService>(),
            userRepository ?? Mock.Of<IUserRepository>(),
            configuration ?? new ConfigurationBuilder().Build()));

    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        object response = request switch
        {
            RotateSigningKeyCommand command when _keyRotationHandler is not null => await _keyRotationHandler.Handle(command, cancellationToken),
            CleanupExpiredSigningKeysCommand command when _keyRotationHandler is not null => await _keyRotationHandler.Handle(command, cancellationToken),
            InitiateMfaSetupCommand command when _mfaHandler is not null => await _mfaHandler.Handle(command, cancellationToken),
            CompleteMfaSetupCommand command when _mfaHandler is not null => await _mfaHandler.Handle(command, cancellationToken),
            VerifyMfaCodeCommand command when _mfaHandler is not null => await _mfaHandler.Handle(command, cancellationToken),
            RegenerateMfaBackupCodesCommand command when _mfaHandler is not null => await _mfaHandler.Handle(command, cancellationToken),
            DisableMfaCommand command when _mfaHandler is not null => await _mfaHandler.Handle(command, cancellationToken),
            CreateServiceAccountCommand command when _serviceAccountHandler is not null => await _serviceAccountHandler.Handle(command, cancellationToken),
            PatchServiceAccountCommand command when _serviceAccountHandler is not null => await _serviceAccountHandler.Handle(command, cancellationToken),
            RotateServiceAccountSecretCommand command when _serviceAccountHandler is not null => await _serviceAccountHandler.Handle(command, cancellationToken),
            UnlockServiceAccountCommand command when _serviceAccountHandler is not null => await _serviceAccountHandler.Handle(command, cancellationToken),
            LockServiceAccountCommand command when _serviceAccountHandler is not null => await _serviceAccountHandler.Handle(command, cancellationToken),
            DeactivateServiceAccountCommand command when _serviceAccountHandler is not null => await _serviceAccountHandler.Handle(command, cancellationToken),
            ReactivateServiceAccountCommand command when _serviceAccountHandler is not null => await _serviceAccountHandler.Handle(command, cancellationToken),
            UpdateServiceAccountScopesCommand command when _serviceAccountHandler is not null => await _serviceAccountHandler.Handle(command, cancellationToken),
            IssueServiceAccountTokenCommand command when _serviceAccountHandler is not null => await _serviceAccountHandler.Handle(command, cancellationToken),
            TerminateSessionCommand command when _sessionHandler is not null => await _sessionHandler.Handle(command, cancellationToken),
            TerminateUserSessionsCommand command when _sessionHandler is not null => await _sessionHandler.Handle(command, cancellationToken),
            RefreshUserSessionCommand command when _sessionHandler is not null => await _sessionHandler.Handle(command, cancellationToken),
            TrustDeviceCommand command when _sessionHandler is not null => await _sessionHandler.Handle(command, cancellationToken),
            RevokeTrustedDeviceCommand command when _sessionHandler is not null => await _sessionHandler.Handle(command, cancellationToken),
            CreateStepUpChallengeCommand command when _stepUpHandler is not null => await _stepUpHandler.Handle(command, cancellationToken),
            BeginStepUpWebAuthnCommand command when _stepUpHandler is not null => await _stepUpHandler.Handle(command, cancellationToken),
            VerifyStepUpChallengeCommand command when _stepUpHandler is not null => await _stepUpHandler.Handle(command, cancellationToken),
            BeginWebAuthnRegistrationCommand command when _webAuthnHandler is not null => await _webAuthnHandler.Handle(command, cancellationToken),
            CompleteWebAuthnRegistrationCommand command when _webAuthnHandler is not null => await _webAuthnHandler.Handle(command, cancellationToken),
            BeginWebAuthnAuthenticationCommand command when _webAuthnHandler is not null => await _webAuthnHandler.Handle(command, cancellationToken),
            CompleteWebAuthnAuthenticationCommand command when _webAuthnHandler is not null => await _webAuthnHandler.Handle(command, cancellationToken),
            VerifyWebAuthnCredentialCommand command when _webAuthnHandler is not null => await _webAuthnHandler.Handle(command, cancellationToken),
            DeleteWebAuthnCredentialCommand command when _webAuthnHandler is not null => await _webAuthnHandler.Handle(command, cancellationToken),
            UpdateWebAuthnCredentialNameCommand command when _webAuthnHandler is not null => await _webAuthnHandler.Handle(command, cancellationToken),
            _ => throw new InvalidOperationException($"No test handler is configured for {request.GetType().Name}.")
        };

        return (TResponse)response;
    }

    public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        await Send((object)request, cancellationToken);
    }

    public async Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        return request switch
        {
            UnlockServiceAccountCommand command => await Send<Unit>(command, cancellationToken),
            LockServiceAccountCommand command => await Send<Unit>(command, cancellationToken),
            DeactivateServiceAccountCommand command => await Send<Unit>(command, cancellationToken),
            ReactivateServiceAccountCommand command => await Send<Unit>(command, cancellationToken),
            UpdateServiceAccountScopesCommand command => await Send<Unit>(command, cancellationToken),
            _ => await Send((dynamic)request, cancellationToken)
        };
    }
}
