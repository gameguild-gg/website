using Fido2NetLib;
using GameGuild.Configuration;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.CQRS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

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
        RegisterSecurityServices(services, configuration);

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

        var apiKeyLifecycleOptions = OptionBuilderUtilities.CreateAndBind(
            configuration,
            ApiKeyLifecycleOptions.SectionName,
            static () => new ApiKeyLifecycleOptions());
        var apiKeyLifecycleValidation = apiKeyLifecycleOptions.Validate();
        if (!apiKeyLifecycleValidation.IsValid)
        {
            throw new InvalidOperationException(
                $"Invalid {ApiKeyLifecycleOptions.SectionName} configuration: {string.Join("; ", apiKeyLifecycleValidation.Errors)}");
        }

        services.AddSingleton(apiKeyLifecycleOptions);

        // Configure JWT options from configuration
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));

        // Sign-in compliance gate (issue #267): safe default is disabled; hosts register a
        // real ISignInCompliancePolicy adapter to compose product compliance signals. The
        // default policy is stateless, so the shared instance is served per scope.
        services.Configure<SignInComplianceGateOptions>(configuration.GetSection(SignInComplianceGateOptions.SectionName));
        services.TryAddScoped<ISignInCompliancePolicy>(static _ => AllowAllSignInCompliancePolicy.Instance);

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
        services.AddScoped<IAdaptiveBehaviorBaselineRepository, AdaptiveBehaviorBaselineRepository>();
        services.AddScoped<IAuthenticationFlowStateRepository, AuthenticationFlowStateRepository>();
        services.AddScoped<IAuthenticationOrchestrationService, AuthenticationOrchestrationService>();
        services.AddScoped<ITrustedDeviceRepository, TrustedDeviceRepository>();
        services.AddScoped<IMfaAttemptRepository, MfaAttemptRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<GameGuild.Identity.Authorization.IAuthorizationRolePermissionProvider, RolePermissionProvider>();
        services.AddScoped<IServiceAccountRepository, ServiceAccountRepository>();
        services.AddScoped<IExternalLoginRepository, ExternalLoginRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();

        // Core authentication services - focused sub-services
        services.AddScoped<IAuthAttemptService, AuthAttemptService>();
        services.AddScoped<ISuspiciousLoginAlertPublisher, SuspiciousLoginAlertPublisher>();
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
    private static void RegisterSecurityServices(IServiceCollection services, IConfiguration configuration)
    {
        // Anomaly-detection sub-services
        services.AddScoped<IThreatDetectionService, ThreatDetectionService>();
        services.AddScoped<IBehavioralAnalysisService, BehavioralAnalysisService>();
        services.AddScoped<ILoginAttemptAnalysisService, LoginAttemptAnalysisService>();

        // Adaptive (online statistical learning) anomaly detection: EWMA baselines per subject
        // scored alongside the fixed-weight heuristics above. Fails open and abstains on cold start.
        RegisterAdaptiveAnomalyDetection(services, configuration);

        // Credential-stuffing threat intelligence (safe default: local operator-supplied feed)
        RegisterThreatIntelligence(services, configuration);

        // Facade that preserves the original IAuthenticationAnomalyDetectionService contract
        services.AddScoped<AuthenticationAnomalyDetectionService>();
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();
        services.AddScoped<IEmailCodeService, EmailCodeService>();
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
    ///     Registers the credential-stuffing threat-intelligence provider selected by
    ///     <c>ThreatIntelligence:Provider</c>. The safe default is <c>LocalFile</c> (an
    ///     operator-supplied local feed, zero external calls); <c>None</c> registers the
    ///     disabled no-op. Unknown providers fail startup so misconfiguration is loud.
    /// </summary>
    private static void RegisterThreatIntelligence(IServiceCollection services, IConfiguration configuration)
    {
        var threatIntelligenceOptions = OptionBuilderUtilities.CreateAndBind(
            configuration,
            ThreatIntelligenceOptions.SectionName,
            static () => new ThreatIntelligenceOptions());
        ValidateThreatIntelligenceOptions(threatIntelligenceOptions);
        services.AddSingleton(threatIntelligenceOptions);

        if (string.Equals(threatIntelligenceOptions.Provider, ThreatIntelligenceOptions.NoneProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IThreatIntelligenceProvider>(NullThreatIntelligenceProvider.Instance);
            return;
        }

        services.AddSingleton<IThreatIntelligenceProvider>(static provider =>
            new LocalFileThreatIntelligenceProvider(
                provider.GetRequiredService<ThreatIntelligenceOptions>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<ILogger<LocalFileThreatIntelligenceProvider>>(),
                provider.GetService<IServiceScopeFactory>()));
    }

    /// <summary>
    ///     Registers the adaptive anomaly detection options and service. The learned model is
    ///     enabled by default; disabling it via configuration keeps only the heuristic scorer.
    /// </summary>
    private static void RegisterAdaptiveAnomalyDetection(IServiceCollection services, IConfiguration configuration)
    {
        var adaptiveOptions = OptionBuilderUtilities.CreateAndBind(
            configuration,
            AdaptiveAnomalyDetectionOptions.SectionName,
            static () => new AdaptiveAnomalyDetectionOptions());
        if (adaptiveOptions.EwmaAlpha is <= 0 or > 1)
        {
            throw new InvalidOperationException(
                $"Invalid {AdaptiveAnomalyDetectionOptions.SectionName} configuration: EwmaAlpha must be in (0, 1].");
        }

        if (adaptiveOptions.ZScoreThreshold <= 0)
        {
            throw new InvalidOperationException(
                $"Invalid {AdaptiveAnomalyDetectionOptions.SectionName} configuration: ZScoreThreshold must be positive.");
        }

        if (adaptiveOptions.MinimumObservations < 2)
        {
            throw new InvalidOperationException(
                $"Invalid {AdaptiveAnomalyDetectionOptions.SectionName} configuration: MinimumObservations must be at least 2.");
        }

        services.AddSingleton(adaptiveOptions);
        services.AddScoped<IAdaptiveAnomalyDetectionService, AdaptiveAnomalyDetectionService>();
    }

    private static void ValidateThreatIntelligenceOptions(ThreatIntelligenceOptions options)
    {
        var isKnownProvider = string.Equals(options.Provider, ThreatIntelligenceOptions.LocalFileProvider, StringComparison.OrdinalIgnoreCase)
                              || string.Equals(options.Provider, ThreatIntelligenceOptions.NoneProvider, StringComparison.OrdinalIgnoreCase);
        if (!isKnownProvider)
        {
            throw new InvalidOperationException(
                $"Invalid {ThreatIntelligenceOptions.SectionName} configuration: unknown provider '{options.Provider}'. "
                + $"Supported providers are '{ThreatIntelligenceOptions.LocalFileProvider}' (default) and '{ThreatIntelligenceOptions.NoneProvider}'.");
        }

        if (options.MaliciousIpRiskScore is < 0 or > 100 || options.BreachedPasswordRiskScore is < 0 or > 100)
        {
            throw new InvalidOperationException(
                $"Invalid {ThreatIntelligenceOptions.SectionName} configuration: risk scores must be between 0 and 100.");
        }

        if (string.IsNullOrWhiteSpace(options.LocalFile.FilePath))
        {
            throw new InvalidOperationException(
                $"Invalid {ThreatIntelligenceOptions.SectionName} configuration: LocalFile:FilePath must not be empty.");
        }

        if (options.LocalFile.ReloadInterval < TimeSpan.FromSeconds(1))
        {
            throw new InvalidOperationException(
                $"Invalid {ThreatIntelligenceOptions.SectionName} configuration: LocalFile:ReloadInterval must be at least one second.");
        }
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
        services.AddScoped<IRequestHandler<RequestEmailCodeCommand, EmailCodeRequestResult>, RequestEmailCodeCommandHandler>();
        services.AddScoped<IRequestHandler<ConsumeEmailCodeCommand, SignInResponse>, ConsumeEmailCodeCommandHandler>();
        
        // Logout handler with immediate token revocation
        services.AddScoped<IRequestHandler<LogoutCommand, LogoutResponse>, LogoutHandler>();
    }
}
