using System.Diagnostics;
using System.Globalization;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Authenticates a verified wallet identity against a persisted account and session.
/// </summary>
public class Web3AuthService(
    IUserRepository userRepository,
    IExternalLoginRepository externalLoginRepository,
    IJwtTokenService jwtTokenService,
    IRefreshTokenHasher refreshTokenHasher,
    ISessionManagementService sessionManagementService,
    ISender sender,
    IWeb3Service web3Service,
    IConfiguration configuration,
    IAuthAttemptService authAttemptService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<Web3AuthService> logger,
    IOptions<JwtOptions>? jwtOptions = null
) : IWeb3AuthService
{
    private const string WalletProvider = "web3";

    public async Task<Web3ChallengeResponse> GenerateWeb3ChallengeAsync(Web3ChallengeRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var challenge = await web3Service.GenerateChallengeAsync(request.WalletAddress, chainId: request.ChainId).ConfigureAwait(false);
        return new Web3ChallengeResponse { Challenge = challenge.Message, Nonce = challenge.Nonce, ExpiresAt = challenge.ExpiresAt };
    }

    public async Task<SignInResponse> VerifyWeb3SignatureAsync(Web3VerificationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();
        var isValid = await web3Service.VerifySignatureAsync(
            request.WalletAddress, request.Signature, request.Challenge, request.ChainId, request.Nonce).ConfigureAwait(false);
        var httpContext = httpContextAccessor.HttpContext;
        var ipAddress = authAttemptService.GetClientIpAddress(httpContext);
        var userAgent = httpContext?.Request.Headers.UserAgent.ToString();
        if (!isValid)
        {
            await authAttemptService.RecordFailedAttemptAsync(
                "web3@audit.invalid", null, ipAddress, userAgent, "InvalidSignature", stopwatch.Elapsed, "Web3").ConfigureAwait(false);
            throw new AuthenticationRequiredException("Invalid Web3 signature");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var user = await ResolveWalletUserAsync(request.WalletAddress, cancellationToken).ConfigureAwait(false);
        if (user.IsDeleted || !user.ValidateForAuthentication(user.TokenVersion).IsSuccess)
        {
            throw new AuthenticationRequiredException("Wallet account is unavailable");
        }

        await DefaultTenantMembershipProvisioner.EnsureAsync(sender, user.Id, cancellationToken).ConfigureAwait(false);
        var memberships = await sender.Send(new global::GameGuild.Identity.Tenants.GetUserMembershipsQuery(user.Id), cancellationToken).ConfigureAwait(false);
        var tenantContext = TenantAccessContextResolver.Resolve(memberships, request.TenantId);
        if (!tenantContext.TenantId.HasValue || (request.TenantId.HasValue && tenantContext.TenantId != request.TenantId))
        {
            throw new AuthenticationRequiredException("Wallet account has no active access to the requested tenant");
        }

        var deviceInfo = new DeviceInfo
        {
            Fingerprint = string.IsNullOrWhiteSpace(request.DeviceFingerprint) ? Guid.NewGuid().ToString("N") : request.DeviceFingerprint,
            IpAddress = ipAddress, UserAgent = userAgent, DeviceName = "Wallet Device", DeviceType = "Web"
        };
        var refreshDays = jwtOptions?.Value.RefreshTokenExpirationDays
                          ?? int.Parse(configuration["Jwt:RefreshTokenExpirationDays"] ?? configuration["Jwt:RefreshTokenExpiryInDays"] ?? "7", CultureInfo.InvariantCulture);
        var refreshExpires = SystemClock.UtcNow.AddDays(refreshDays);
        var sessionId = Guid.NewGuid();
        var refresh = await jwtTokenService.GenerateRefreshTokenAsync(user.Id, deviceInfo, cancellationToken).ConfigureAwait(false);
        var access = await jwtTokenService.GenerateAccessTokenAsync(
            user.Id, user.Email, tenantContext.Roles.ToArray(), tenantContext.TenantId, user.TokenVersion, sessionId, cancellationToken).ConfigureAwait(false);
        var refreshTokenHash = refreshTokenHasher.HashToken(refresh);
        var session = await sessionManagementService.CreateSessionAsync(
            sessionId, user.Id, ipAddress ?? "unknown", userAgent ?? string.Empty,
            refreshTokenHash, refreshExpires, deviceInfo.Fingerprint, cancellationToken).ConfigureAwait(false);
        refreshExpires = AuthenticatedSessionDeadline.Require(session, user.Id, sessionId, refreshTokenHash, refreshExpires);
        await authAttemptService.RecordSuccessfulAttemptAsync(
            user.Email, user.Id, ipAddress ?? "unknown", userAgent, stopwatch.Elapsed, "Web3").ConfigureAwait(false);
        logger.LogInformation("Verified wallet authentication established session {SessionId} for account {UserId}", sessionId, user.Id);
        var accessMinutes = jwtOptions?.Value.AccessTokenExpirationMinutes
                            ?? int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60", CultureInfo.InvariantCulture);
        return new SignInResponse
        {
            Success = true, Message = "Web3 authentication successful", AccessToken = access, RefreshToken = refresh,
            ExpiresAt = refreshExpires, ExpiresIn = accessMinutes * 60, AccessTokenExpiresAt = SystemClock.UtcNow.AddMinutes(accessMinutes),
            RefreshTokenExpiresAt = refreshExpires, UserId = user.Id, Email = user.Email, SessionId = sessionId,
            TenantId = tenantContext.TenantId, AvailableTenants = tenantContext.AvailableTenants
        };
    }

    private async Task<User> ResolveWalletUserAsync(string verifiedWalletAddress, CancellationToken cancellationToken)
    {
        var key = verifiedWalletAddress.ToLowerInvariant();
        var link = await externalLoginRepository.GetByProviderKeyAsync(WalletProvider, key, cancellationToken).ConfigureAwait(false);
        if (link is not null)
        {
            return await userRepository.GetByIdAsync(link.UserId, cancellationToken).ConfigureAwait(false)
                   ?? throw new AuthenticationRequiredException("Linked wallet account was not found");
        }

        // This is an unverified internal identifier. Wallet possession never verifies an email,
        // and a matching email must not silently link or take over an existing account.
        var identifier = key + "@web3.local";
        if (await userRepository.GetByEmailAsync(identifier, cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new AuthenticationRequiredException("Wallet identity requires an explicit account link");
        }

        var user = User.CreateOAuthUser(identifier, "Wallet " + key, emailVerified: false);
        await userRepository.AddAsync(user, cancellationToken).ConfigureAwait(false);
        await userRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        // Insert only. A concurrent winner can never have its account reassigned by sign-in.
        // The enclosing command transaction rolls back this attempt on a uniqueness collision.
        await externalLoginRepository.AddAsync(new ExternalLogin { UserId = user.Id, Provider = WalletProvider, ProviderKey = key }, cancellationToken).ConfigureAwait(false);
        return user;
    }
}
