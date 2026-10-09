using Fido2NetLib;
using GameGuild.Configuration;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.CQRS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Dependency injection configuration for Authentication Data layer
///     Registers all services, validators, and business logic components WITHOUT MediatR
/// </summary>
public static class DataDependencyInjection
{
    /// <summary>
    ///     Register all Application layer services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAuthenticationData(this IServiceCollection services, IConfiguration configuration)
    {
        // Register core authentication services
        RegisterAuthenticationServices(services, configuration);

        // Config-gated certificate blockchain anchoring (safe default: disabled no-op)
        services.AddBlockchainCertificateAnchoring(configuration);

        // Register security services
        RegisterSecurityServices(services);

        // Register utility services
        RegisterUtilityServices(services);

        // Register validators (FluentValidation)
        RegisterValidators(services);

        // Register CQRS command handlers
        RegisterCommandHandlers(services);

        return services;
    }

    /// <summary>
    ///     Register core authentication services
    /// </summary>
    private static void RegisterAuthenticationServices(IServiceCollection services, IConfiguration configuration)
    {
        var mfaOptions = OptionBuilderUtilities.CreateAndBind(
            configuration,
            MfaOptions.SectionName,
            static () => new MfaOptions());
        var mfaValidation = mfaOptions.Validate();
        if (!mfaValidation.IsValid)
        {
            throw new InvalidOperationException(
                $"Invalid {MfaOptions.SectionName} configuration: {string.Join("; ", mfaValidation.Errors)}");
        }

        var sessionOptions = OptionBuilderUtilities.CreateAndBind(
            configuration,
            SessionOptions.SectionName,
            static () => new SessionOptions());
        var sessionValidation = sessionOptions.Validate();
        if (!sessionValidation.IsValid)
        {
            throw new InvalidOperationException(
                $"Invalid {SessionOptions.SectionName} configuration: {string.Join("; ", sessionValidation.Errors)}");
        }

        services.AddSingleton(mfaOptions);
        services.AddSingleton(sessionOptions);

        // Configure JWT options from configuration
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));

        // Register repositories
        // NOTE: IUserRepository is registered by the Users module - no need to register here
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IRefreshTokenLineageRepository>(provider =>
            provider.GetRequiredService<IRefreshTokenRepository>() as IRefreshTokenLineageRepository
            ?? throw new InvalidOperationException("The refresh-token store must support persisted session and parent lineage."));
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IRefreshTokenCleanupRepository>(provider =>
            provider.GetRequiredService<IRefreshTokenRepository>() as IRefreshTokenCleanupRepository
            ?? throw new InvalidOperationException("The refresh-token store must support bounded retention cleanup."));
        services.AddScoped<IUserSessionCleanupRepository>(provider =>
            provider.GetRequiredService<IUserSessionRepository>() as IUserSessionCleanupRepository
            ?? throw new InvalidOperationException("The session store must support bounded retention cleanup."));
        services.AddScoped<IUserMfaConfigurationRepository, UserMfaConfigurationRepository>();
        services.AddScoped<IAuthenticationAttemptRepository, AuthenticationAttemptRepository>();
        services.AddScoped<IAuthenticationFlowStateRepository, AuthenticationFlowStateRepository>();
        services.AddScoped<IAuthenticationOrchestrationService, AuthenticationOrchestrationService>();
        services.AddScoped<ITrustedDeviceRepository, TrustedDeviceRepository>();
        services.AddScoped<IMfaAttemptRepository, MfaAttemptRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<GameGuild.Identity.Authorization.IAuthorizationRolePermissionProvider, RolePermissionProvider>();
        services.AddScoped<IServiceAccountRepository, ServiceAccountRepository>();
        services.AddScoped<IExternalLoginRepository, ExternalLoginRepository>();

        // Core authentication services - focused sub-services
        services.AddScoped<IAuthAttemptService, AuthAttemptService>();
        services.AddScoped<ILocalAuthService, LocalAuthService>();
        services.AddScoped<IOAuthAuthService, OAuthAuthService>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<IWeb3AuthService, Web3AuthService>();

        // Composite service for backward compatibility
        services.AddScoped<IAuthService, AuthService>();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthenticatedSessionIssuer, AuthenticatedSessionIssuer>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IOAuthService, OAuthService>();
        // Google ID token verifier — cryptographic signature + iss/aud/exp via Google.Apis.Auth.
        // Supersedes OAuthService.ValidateGoogleIdTokenInternalAsync (Todo 3 swaps the only caller).
        services.AddScoped<IGoogleIdTokenVerifier, GoogleIdTokenVerifier>();
        services.AddScoped<IWeb3Service, Web3Service>();
        services.AddScoped<IServiceAccountService, ServiceAccountService>();

        var redisEnabled = configuration.GetValue<bool>("Redis:Enabled");
        var distributedRevocationEnabled = configuration.GetValue<bool?>("Authentication:TokenRevocation:UseDistributedCache") ?? redisEnabled;
        if (distributedRevocationEnabled)
        {
            services.AddSingleton<ITokenRevocationService, DistributedCacheTokenRevocationService>();
        }
        else
        {
            services.AddSingleton<ITokenRevocationService, InMemoryTokenRevocationService>();
        }

        services.AddSingleton<IVersionedUserTokenRevocationService>(provider =>
            provider.GetRequiredService<ITokenRevocationService>() as IVersionedUserTokenRevocationService
            ?? throw new InvalidOperationException("The token revocation store must support persisted user token versions."));

        // MFA services - focused sub-services
        services.AddScoped<ITotpMfaService, TotpMfaService>();
        services.AddScoped<IBackupCodeMfaService, BackupCodeMfaService>();
        services.AddScoped<IMfaAttemptTrackingService, MfaAttemptTrackingService>();

        // Composite MFA service for backward compatibility
        services.AddScoped<IMfaService, MfaService>();
        services.AddScoped<IStepUpChallengeStore, PostgreSqlStepUpChallengeStore>();
        services.AddScoped<IStepUpReceiptService, StepUpReceiptService>();
        services.TryAddSingleton(TimeProvider.System);

        // Session management
        services.AddScoped<ISessionManagementService, SessionManagementService>();

        // WebAuthn/FIDO2 services
        RegisterWebAuthnServices(services, configuration);
    }

    /// <summary>
    ///     Register WebAuthn/FIDO2 services for passwordless authentication
    /// </summary>
    private static void RegisterWebAuthnServices(IServiceCollection services, IConfiguration configuration)
    {
        // Get WebAuthn configuration
        var webAuthnSection = configuration.GetSection("WebAuthn");
        var serverDomain = webAuthnSection["ServerDomain"] ?? "localhost";
        var serverName = webAuthnSection["ServerName"] ?? "GameGuild";
        var origins = webAuthnSection.GetSection("Origins").Get<HashSet<string>>()
            ?? new HashSet<string> { "https://localhost:3000", "https://localhost:5000" };

        // Register Fido2Configuration
        var fido2Config = new Fido2Configuration
        {
            ServerDomain = serverDomain,
            ServerName = serverName,
            Origins = origins,
            TimestampDriftTolerance = 60000 // 60 seconds
        };

        // Register Fido2 as singleton
        services.AddSingleton(fido2Config);
        services.AddSingleton<IFido2>(sp => new Fido2(sp.GetRequiredService<Fido2Configuration>()));

        // Register WebAuthn repository and sub-services
        services.AddScoped<IWebAuthnCredentialRepository, WebAuthnCredentialRepository>();
        services.AddScoped<IWebAuthnRegistrationService, WebAuthnRegistrationService>();
        services.AddScoped<IWebAuthnAuthenticationService, WebAuthnAuthenticationSubService>();
        services.AddScoped<IWebAuthnCredentialManagementService, WebAuthnCredentialManagementService>();

        // Facade preserves original IWebAuthnService contract for backward compatibility
        services.AddScoped<IWebAuthnService, WebAuthnService>();
    }

    /// <summary>
    ///     Register security-focused services
    /// </summary>
    private static void RegisterSecurityServices(IServiceCollection services)
    {
        // Anomaly-detection sub-services
        services.AddScoped<IThreatDetectionService, ThreatDetectionService>();
        services.AddScoped<IBehavioralAnalysisService, BehavioralAnalysisService>();
        services.AddScoped<ILoginAttemptAnalysisService, LoginAttemptAnalysisService>();

        // Facade that preserves the original IAuthenticationAnomalyDetectionService contract
        services.AddScoped<AuthenticationAnomalyDetectionService>();
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();
        services.AddScoped<IAuthenticationAnomalyDetectionService, AuthenticationAnomalyDetectionService>();
        services.AddScoped<IUserEnumerationProtectionService, UserEnumerationProtectionService>();
        services.AddScoped<IEncryptionService, EncryptionService>();
        services.AddScoped<ISiemIntegrationService, SiemIntegrationService>();

        // Refresh token hashing service (singleton - stateless)
        services.AddSingleton<IRefreshTokenHasher, RefreshTokenHasher>();

        // Note: These services have interface mismatches and need interface updates
        // to match GameGuild implementation signatures before registering with interfaces
    }

    /// <summary>
    ///     Register utility services
    /// </summary>
    private static void RegisterUtilityServices(IServiceCollection services)
    {
        // Register HttpClient for OAuth services
        services.AddHttpClient();
    }

    /// <summary>
    ///     Register FluentValidation validators
    /// </summary>
    private static void RegisterValidators(IServiceCollection services)
    {
        // Register validators explicitly using FluentValidation
        services.AddScoped<FluentValidation.IValidator<LocalSignInCommand>, LocalSignInCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<LocalSignUpCommand>, LocalSignUpCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<RefreshTokenCommand>, RefreshTokenCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<RevokeTokenCommand>, RevokeTokenCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<GoogleIdTokenSignInCommand>, GoogleIdTokenSignInCommandValidator>();
    }

    /// <summary>
    ///     Register CQRS command handlers
    /// </summary>
    private static void RegisterCommandHandlers(IServiceCollection services)
    {
        // Register command handlers for local authentication
        services.AddScoped<IRequestHandler<LocalSignUpCommand, SignInResponse>, LocalSignUpHandler>();
        services.AddScoped<IRequestHandler<LocalSignInCommand, SignInResponse>, LocalSignInHandler>();
        services.AddScoped<IRequestHandler<PolymorphicSignInCommand, SignInResponse>, PolymorphicSignInHandler>();
        services.AddScoped<IRequestHandler<RefreshTokenCommand, SignInResponse>, RefreshTokenHandler>();
        services.AddScoped<IRequestHandler<GoogleIdTokenSignInCommand, SignInResponse>, GoogleIdTokenSignInHandler>();
        services.AddScoped<IRequestHandler<SendEmailVerificationCommand, EmailVerificationResponse>, SendEmailVerificationCommandHandler>();
        services.AddScoped<IRequestHandler<VerifyEmailCommand, EmailVerificationResult>, VerifyEmailCommandHandler>();
        services.AddScoped<IRequestHandler<RequestPasswordResetCommand, PasswordResetRequestResult>, RequestPasswordResetCommandHandler>();
        services.AddScoped<IRequestHandler<ResetPasswordCommand, PasswordResetResult>, ResetPasswordCommandHandler>();
        services.AddScoped<IRequestHandler<ChangePasswordCommand, PasswordChangeResult>, ChangePasswordCommandHandler>();
        services.AddScoped<IRequestHandler<RequestMagicLinkCommand, MagicLinkRequestResult>, RequestMagicLinkCommandHandler>();
        services.AddScoped<IRequestHandler<ConsumeMagicLinkCommand, SignInResponse>, ConsumeMagicLinkCommandHandler>();
        
        // Logout handler with immediate token revocation
        services.AddScoped<IRequestHandler<LogoutCommand, LogoutResponse>, LogoutHandler>();
    }
}
