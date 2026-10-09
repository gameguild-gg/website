using System.Diagnostics;
using System.Globalization;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authentication;

/// <summary>
/// OAuth authentication: GitHub OAuth, Google OAuth, Google ID token, Discord OAuth
/// </summary>
public class OAuthAuthService(
    IUserRepository userRepository,
    IJwtTokenService jwtTokenService,
    IRefreshTokenHasher refreshTokenHasher,
    IOAuthService oauthService,
    IGoogleIdTokenVerifier googleIdTokenVerifier,
    IExternalLoginRepository externalLoginRepository,
    IConfiguration configuration,
    IAuthAttemptService authAttemptService,
    IHttpContextAccessor httpContextAccessor,
    ISender sender,
    ISessionManagementService sessionManagementService,
    ILogger<OAuthAuthService> logger,
    IOptions<JwtOptions>? jwtOptions = null
) : IOAuthAuthService
{
    public async Task<SignInResponse> GitHubSignInAsync(OAuthSignInRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Processing GitHub OAuth sign-in");

        var githubUser = await RunProviderAuthenticationAsync(
            "GitHub",
            stopwatch,
            () => oauthService.GetUserProfileAsync("github", request.AccessToken)).ConfigureAwait(false);

        var email = githubUser.Email ?? throw new UnauthorizedAccessException("Email not available from GitHub profile");
        var grantedScopes = oauthService.ResolveAuthorizationScopes("github");
        var user = await ResolveExternalUserAsync("github", email, githubUser.ProviderId, githubUser.Name, githubUser.EmailVerified, grantedScopes, cancellationToken).ConfigureAwait(false);
        await DefaultTenantMembershipProvisioner.EnsureAsync(sender, user.Id, cancellationToken).ConfigureAwait(false);
        var tenantAccessContext = await ResolveTenantAccessContextAsync(user.Id, request.TenantId, cancellationToken).ConfigureAwait(false);

        var httpContext = httpContextAccessor.HttpContext;
        var ipAddress = authAttemptService.GetClientIpAddress(httpContext);
        var userAgent = httpContext?.Request.Headers.UserAgent.ToString();

        var deviceInfo = new DeviceInfo { Fingerprint = Guid.NewGuid().ToString(), IpAddress = ipAddress, UserAgent = userAgent, DeviceName = "OAuth Device", DeviceType = "Web" };

        logger.LogInformation("GitHub OAuth sign-in successful for {Email}", LogRedaction.MaskEmail(email));

        return await CompleteSignInAsync(user, tenantAccessContext, deviceInfo, ipAddress, userAgent, "GitHub sign-in successful", "GitHub", stopwatch, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SignInResponse> GoogleSignInAsync(OAuthSignInRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Processing Google OAuth sign-in");

        var googleUser = await RunProviderAuthenticationAsync(
            "Google",
            stopwatch,
            () => oauthService.GetUserProfileAsync("google", request.AccessToken)).ConfigureAwait(false);

        var email = googleUser.Email ?? throw new UnauthorizedAccessException("Email not available from Google profile");
        var grantedScopes = oauthService.ResolveAuthorizationScopes("google");
        var user = await ResolveExternalUserAsync("google", email, googleUser.ProviderId, googleUser.Name, googleUser.EmailVerified, grantedScopes, cancellationToken).ConfigureAwait(false);
        await DefaultTenantMembershipProvisioner.EnsureAsync(sender, user.Id, cancellationToken).ConfigureAwait(false);
        var tenantAccessContext = await ResolveTenantAccessContextAsync(user.Id, request.TenantId, cancellationToken).ConfigureAwait(false);

        var httpContext = httpContextAccessor.HttpContext;
        var ipAddress = authAttemptService.GetClientIpAddress(httpContext);
        var userAgent = httpContext?.Request.Headers.UserAgent.ToString();

        var deviceInfo = new DeviceInfo { Fingerprint = Guid.NewGuid().ToString(), IpAddress = ipAddress, UserAgent = userAgent, DeviceName = "OAuth Device", DeviceType = "Web" };

        logger.LogInformation("Google OAuth sign-in successful for {Email}", LogRedaction.MaskEmail(email));

        return await CompleteSignInAsync(user, tenantAccessContext, deviceInfo, ipAddress, userAgent, "Google sign-in successful", "Google", stopwatch, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SignInResponse> MicrosoftSignInAsync(OAuthSignInRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Processing Microsoft OAuth sign-in");

        var microsoftUser = await RunProviderAuthenticationAsync(
            "Microsoft",
            stopwatch,
            () => oauthService.GetUserProfileAsync("microsoft", request.AccessToken)).ConfigureAwait(false);
        var email = microsoftUser.Email ?? throw new UnauthorizedAccessException("Email not available from Microsoft profile");
        var grantedScopes = oauthService.ResolveAuthorizationScopes("microsoft");
        var user = await ResolveExternalUserAsync(
            "microsoft", email, microsoftUser.ProviderId, microsoftUser.Name, microsoftUser.EmailVerified, grantedScopes, cancellationToken)
            .ConfigureAwait(false);
        await DefaultTenantMembershipProvisioner.EnsureAsync(sender, user.Id, cancellationToken).ConfigureAwait(false);
        var tenantAccessContext = await ResolveTenantAccessContextAsync(user.Id, request.TenantId, cancellationToken).ConfigureAwait(false);

        var httpContext = httpContextAccessor.HttpContext;
        var ipAddress = authAttemptService.GetClientIpAddress(httpContext);
        var userAgent = httpContext?.Request.Headers.UserAgent.ToString();
        var deviceInfo = new DeviceInfo
        {
            Fingerprint = Guid.NewGuid().ToString(),
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceName = "OAuth Device",
            DeviceType = "Web"
        };

        logger.LogInformation("Microsoft OAuth sign-in successful for {Email}", LogRedaction.MaskEmail(email));

        return await CompleteSignInAsync(
            user, tenantAccessContext, deviceInfo, ipAddress, userAgent, "Microsoft sign-in successful", "Microsoft", stopwatch, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<SignInResponse> GoogleIdTokenSignInAsync(GoogleIdTokenRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        if (string.IsNullOrEmpty(request.IdToken))
        {
            var exception = new UnauthorizedAccessException("ID token is required");
            await RecordFailedOAuthAttemptAsync("GoogleIdToken", stopwatch, exception).ConfigureAwait(false);
            throw exception;
        }

        // Cryptographically verify the Google ID token (signature, iss, aud, exp).
        // Verifier throws UnauthorizedAccessException on any failure → caller surfaces 401.
        var googleUser = await RunProviderAuthenticationAsync(
            "GoogleIdToken",
            stopwatch,
            () => googleIdTokenVerifier.VerifyAsync(request.IdToken, cancellationToken)).ConfigureAwait(false);

        var email = googleUser.Email;
        var providerKey = googleUser.Sub;
        var grantedScopes = oauthService.ResolveAuthorizationScopes("google");

        var user = await ResolveExternalUserAsync("google", email, providerKey, googleUser.Name, googleUser.EmailVerified, grantedScopes, cancellationToken).ConfigureAwait(false);
        var userId = user.Id;

        await DefaultTenantMembershipProvisioner.EnsureAsync(sender, userId, cancellationToken).ConfigureAwait(false);

        var tenantAccessContext = await ResolveTenantAccessContextAsync(userId, request.TenantId, cancellationToken).ConfigureAwait(false);

        var httpContext = httpContextAccessor.HttpContext;
        var ipAddress = authAttemptService.GetClientIpAddress(httpContext);
        var userAgent = httpContext?.Request.Headers.UserAgent.ToString();
        var deviceInfo = new DeviceInfo { Fingerprint = Guid.NewGuid().ToString(), IpAddress = ipAddress, UserAgent = userAgent, DeviceName = "OAuth Device", DeviceType = "Web" };

        logger.LogInformation("Google ID token sign-in successful for {Email}", LogRedaction.MaskEmail(email));

        return await CompleteSignInAsync(user, tenantAccessContext, deviceInfo, ipAddress, userAgent, "Google ID token sign-in successful", "GoogleIdToken", stopwatch, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SignInResponse> DiscordSignInAsync(DiscordSignInRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Processing Discord OAuth sign-in");

        // HandleCallbackAsync dispatches to ExchangeDiscordCodeAsync (code → access token)
        // and then GetUserProfileAsync("discord", token) → OAuthUserProfile.
        var discordUser = await RunProviderAuthenticationAsync(
            "Discord",
            stopwatch,
            () => oauthService.HandleCallbackAsync("discord", request.Code, request.State, request.RedirectUri))
            .ConfigureAwait(false);

        var email = discordUser.Email ?? throw new UnauthorizedAccessException("Discord account has no email");

        var grantedScopes = oauthService.ResolveAuthorizationScopes("discord");
        var user = await ResolveExternalUserAsync("discord", email, discordUser.ProviderId, discordUser.Name, discordUser.EmailVerified, grantedScopes, cancellationToken).ConfigureAwait(false);
        var userId = user.Id;

        await DefaultTenantMembershipProvisioner.EnsureAsync(sender, userId, cancellationToken).ConfigureAwait(false);

        var tenantAccessContext = await ResolveTenantAccessContextAsync(userId, request.TenantId, cancellationToken).ConfigureAwait(false);

        var httpContext = httpContextAccessor.HttpContext;
        var ipAddress = authAttemptService.GetClientIpAddress(httpContext);
        var userAgent = httpContext?.Request.Headers.UserAgent.ToString();
        var deviceInfo = new DeviceInfo { Fingerprint = Guid.NewGuid().ToString(), IpAddress = ipAddress, UserAgent = userAgent, DeviceName = "OAuth Device", DeviceType = "Web" };

        logger.LogInformation("Discord OAuth sign-in successful for {Email}", LogRedaction.MaskEmail(email));

        return await CompleteSignInAsync(user, tenantAccessContext, deviceInfo, ipAddress, userAgent, "Discord sign-in successful", "Discord", stopwatch, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SignInResponse> CompleteSignInAsync(
        User user,
        TenantAccessContext tenantAccessContext,
        DeviceInfo deviceInfo,
        string? ipAddress,
        string? userAgent,
        string successMessage,
        string authenticationMethod,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var refreshTokenExpiryDays = jwtOptions?.Value.RefreshTokenExpirationDays
                                     ?? int.Parse(
                                         configuration["Jwt:RefreshTokenExpirationDays"] ?? configuration["Jwt:RefreshTokenExpiryInDays"] ?? "7",
                                         CultureInfo.InvariantCulture);
        var refreshTokenExpiresAt = SystemClock.UtcNow.AddDays(refreshTokenExpiryDays);
        var sessionId = Guid.NewGuid();
        var refreshToken = await jwtTokenService.GenerateRefreshTokenAsync(user.Id, deviceInfo, cancellationToken).ConfigureAwait(false);
        var accessToken = await jwtTokenService.GenerateAccessTokenAsync(
            user.Id,
            user.Email,
            tenantAccessContext.Roles.ToArray(),
            tenantAccessContext.TenantId,
            user.TokenVersion,
            sessionId,
            cancellationToken).ConfigureAwait(false);
        var refreshTokenHash = refreshTokenHasher.HashToken(refreshToken);
        var session = await sessionManagementService.CreateSessionAsync(
            sessionId,
            user.Id,
            ipAddress ?? "unknown",
            userAgent ?? string.Empty,
            refreshTokenHash,
            refreshTokenExpiresAt,
            deviceInfo.Fingerprint,
            cancellationToken).ConfigureAwait(false);
        refreshTokenExpiresAt = AuthenticatedSessionDeadline.Require(
            session, user.Id, sessionId, refreshTokenHash, refreshTokenExpiresAt);

        await authAttemptService.RecordSuccessfulAttemptAsync(
            user.Email,
            user.Id,
            ipAddress ?? "unknown",
            userAgent,
            stopwatch.Elapsed,
            authenticationMethod).ConfigureAwait(false);

        var accessTokenExpirationMinutes = jwtOptions?.Value.AccessTokenExpirationMinutes
                                           ?? int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60", CultureInfo.InvariantCulture);

        return new SignInResponse
        {
            Success = true,
            Message = successMessage,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = refreshTokenExpiresAt,
            ExpiresIn = accessTokenExpirationMinutes * 60,
            AccessTokenExpiresAt = SystemClock.UtcNow.AddMinutes(accessTokenExpirationMinutes),
            RefreshTokenExpiresAt = refreshTokenExpiresAt,
            UserId = user.Id,
            Email = user.Email,
            SessionId = sessionId,
            TenantId = tenantAccessContext.TenantId,
            AvailableTenants = tenantAccessContext.AvailableTenants
        };
    }

    private async Task<T> RunProviderAuthenticationAsync<T>(
        string authenticationMethod,
        Stopwatch stopwatch,
        Func<Task<T>> authenticate)
    {
        try
        {
            return await authenticate().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await RecordFailedOAuthAttemptAsync(authenticationMethod, stopwatch, exception).ConfigureAwait(false);
            throw;
        }
    }

    private async Task RecordFailedOAuthAttemptAsync(string authenticationMethod, Stopwatch stopwatch, Exception exception)
    {
        try
        {
            var httpContext = httpContextAccessor.HttpContext;
            await authAttemptService.RecordFailedAttemptAsync(
                $"oauth-{authenticationMethod.ToLowerInvariant()}@audit.invalid",
                null,
                authAttemptService.GetClientIpAddress(httpContext),
                httpContext?.Request.Headers.UserAgent.ToString(),
                exception.GetType().Name,
                stopwatch.Elapsed,
                authenticationMethod).ConfigureAwait(false);
        }
        catch (Exception auditException)
        {
            logger.LogError(auditException, "Could not record failed {AuthenticationMethod} authentication", authenticationMethod);
        }
    }

    /// <summary>
    ///     Resolves the GameGuild <see cref="User" /> for a verified external identity using the
    ///     auto-link policy: existing ExternalLogin wins; else verified-email match links to
    ///     the existing user; else a brand-new OAuth user is created. Concurrent sign-ins for
    ///     the same identity race the unique (Provider, ProviderKey) index — on collision the
    ///     losing insert is caught and the winning rows are refetched (idempotent resume).
    ///     The granted scope list (issue #250) is recorded with a consent stamp when the link
    ///     is created, and re-recorded on later sign-ins only when the scope set changed.
    /// </summary>
    private async Task<User> ResolveExternalUserAsync(string provider, string email, string providerKey, string? name, bool emailVerified, string[] grantedScopes, CancellationToken cancellationToken)
    {
        var existingLink = await externalLoginRepository
            .GetByProviderKeyAsync(provider, providerKey, cancellationToken)
            .ConfigureAwait(false);

        if (existingLink != null)
        {
            var recordedScopes = ExternalLoginGrants.Deserialize(existingLink.GrantedScopes);
            if (existingLink.ConsentedAt is null || !ExternalLoginGrants.SameScopeSet(recordedScopes, grantedScopes))
            {
                // Scope set changed since the recorded consent — the user just re-authorized
                // a different set at the provider, so refresh the consent record.
                await externalLoginRepository.RecordConsentAsync(provider, existingLink.UserId, grantedScopes, cancellationToken).ConfigureAwait(false);
            }

            return await userRepository.GetByIdAsync(existingLink.UserId, cancellationToken).ConfigureAwait(false)
                ?? throw new UnauthorizedAccessException("Linked user not found");
        }

        var existingByEmail = await userRepository.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);

        if (existingByEmail != null && !emailVerified)
        {
            // Refuse to merge an unverified-email collision — would let an unverified
            // external identity hijack a pre-existing account.
            throw new UnauthorizedAccessException($"Email is not verified by {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(provider)}");
        }

        var user = existingByEmail;
        var createdNewUser = false;
        if (user == null)
        {
            user = User.CreateOAuthUser(email, name ?? email.Split('@')[0], emailVerified);
            createdNewUser = true;
        }

        try
        {
            if (createdNewUser)
            {
                await userRepository.AddAsync(user, cancellationToken).ConfigureAwait(false);
                await userRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            await externalLoginRepository.UpsertAsync(
                new ExternalLogin
                {
                    UserId = user.Id,
                    Provider = provider,
                    ProviderKey = providerKey,
                    GrantedScopes = ExternalLoginGrants.Serialize(grantedScopes),
                    ConsentedAt = SystemClock.UtcNow,
                    ConsentVersion = OAuthConsentVersions.Current
                },
                cancellationToken).ConfigureAwait(false);

            return user;
        }
        catch (DbUpdateException)
        {
            // Race lost — unique index on (Provider, ProviderKey) or email rejected our insert.
            // Re-fetch the winning rows and resume; the index is the last-line defense.
            existingLink = await externalLoginRepository
                .GetByProviderKeyAsync(provider, providerKey, cancellationToken)
                .ConfigureAwait(false);

            if (existingLink != null)
            {
                return await userRepository.GetByIdAsync(existingLink.UserId, cancellationToken).ConfigureAwait(false)
                    ?? throw new UnauthorizedAccessException("Linked user not found after race");
            }

            // ExternalLogin row was rolled back but a User collision (email uniqueness) won.
            user = await userRepository.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false)
                ?? throw new UnauthorizedAccessException("User not found after race");

            await externalLoginRepository.UpsertAsync(
                new ExternalLogin
                {
                    UserId = user.Id,
                    Provider = provider,
                    ProviderKey = providerKey,
                    GrantedScopes = ExternalLoginGrants.Serialize(grantedScopes),
                    ConsentedAt = SystemClock.UtcNow,
                    ConsentVersion = OAuthConsentVersions.Current
                },
                cancellationToken).ConfigureAwait(false);

            return user;
        }
    }

    private async Task<TenantAccessContext> ResolveTenantAccessContextAsync(Guid userId, Guid? requestedTenantId, CancellationToken cancellationToken)
    {
        var memberships = await sender.Send(new global::GameGuild.Identity.Tenants.GetUserMembershipsQuery(userId), cancellationToken).ConfigureAwait(false);

        return TenantAccessContextResolver.Resolve(memberships, requestedTenantId);
    }

    public Task<string> GetGitHubAuthUrlAsync(string redirectUri)
    {
        var clientId = configuration["OAuth:GitHub:ClientId"];
        var scopes = "user:email";
        var state = Guid.NewGuid().ToString();

        var url = $"https://github.com/login/oauth/authorize?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope={scopes}&state={state}";

        return Task.FromResult(url);
    }

    public Task<string> GetGoogleAuthUrlAsync(string redirectUri)
    {
        var clientId = configuration["OAuth:Google:ClientId"];
        var scopes = "openid email profile";
        var state = Guid.NewGuid().ToString();

        var url = $"https://accounts.google.com/o/oauth2/v2/auth?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope={Uri.EscapeDataString(scopes)}&response_type=code&state={state}";

        return Task.FromResult(url);
    }
}
