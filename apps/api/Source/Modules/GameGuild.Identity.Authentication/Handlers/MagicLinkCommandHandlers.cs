using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

public sealed class RequestMagicLinkCommandHandler(
    IUserRepository userRepository,
    IEmailVerificationService emailVerificationService,
    IPublisher publisher,
    IConfiguration configuration,
    ILogger<RequestMagicLinkCommandHandler> logger) : ICommandHandler<RequestMagicLinkCommand, MagicLinkRequestResult>
{
    public async Task<MagicLinkRequestResult> Handle(RequestMagicLinkCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken).ConfigureAwait(false);
        string? token = null;

        if (user is not null)
        {
            token = await emailVerificationService.GenerateMagicLinkTokenAsync(user.Id, user.Email).ConfigureAwait(false);

            await publisher.Publish(
                new MagicLinkRequestedNotification
                {
                    Email = user.Email,
                    Token = token,
                    UserName = user.Username ?? user.Name,
                    TenantId = request.TenantId,
                    IpAddress = request.IpAddress,
                    UserAgent = request.UserAgent
                },
                cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Magic-link token generated for user {UserId} from {IpAddress}",
                user.Id,
                request.IpAddress ?? "unknown");
        }
        else
        {
            logger.LogInformation("Magic-link requested for unknown email from {IpAddress}", request.IpAddress ?? "unknown");
        }

        return new MagicLinkRequestResult
        {
            DevelopmentPreviewToken = ShouldExposeDevelopmentToken(configuration) ? token : null
        };
    }

    private static bool ShouldExposeDevelopmentToken(IConfiguration configuration)
        => configuration.GetValue<bool>("Authentication:MagicLink:ExposeDevelopmentToken");
}

public sealed class ConsumeMagicLinkCommandHandler(
    IUserRepository userRepository,
    IEmailVerificationService emailVerificationService,
    IJwtTokenService jwtTokenService,
    IConfiguration configuration,
    ILogger<ConsumeMagicLinkCommandHandler> logger) : ICommandHandler<ConsumeMagicLinkCommand, SignInResponse>
{
    public async Task<SignInResponse> Handle(ConsumeMagicLinkCommand request, CancellationToken cancellationToken)
    {
        var validation = await emailVerificationService.VerifyMagicLinkTokenAsync(request.Token).ConfigureAwait(false);
        if (!validation.Success || validation.UserId is not { } userId)
        {
            throw new UnauthorizedAccessException(validation.FailureReason ?? "Invalid or expired magic-link token");
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            throw new UnauthorizedAccessException("Invalid or expired magic-link token");
        }

        var accessTokenMinutes = ParsePositiveInt(configuration["Jwt:AccessTokenExpirationMinutes"], 60);
        var refreshTokenDays = ParsePositiveInt(
            configuration["Jwt:RefreshTokenExpirationDays"] ?? configuration["Jwt:RefreshTokenExpiryInDays"],
            30);
        var now = SystemClock.UtcNow;

        var accessToken = await jwtTokenService.GenerateAccessTokenAsync(
            user.Id,
            user.Email,
            [],
            request.TenantId,
            user.TokenVersion,
            cancellationToken).ConfigureAwait(false);

        var refreshToken = await jwtTokenService.GenerateRefreshTokenAsync(
            user.Id,
            new DeviceInfo
            {
                Fingerprint = request.DeviceFingerprint ?? $"magic-link:{Guid.NewGuid():N}",
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent,
                DeviceName = "Magic Link",
                DeviceType = "Web"
            },
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation("User {UserId} signed in with magic-link authentication", user.Id);

        return new SignInResponse
        {
            Success = true,
            Message = "Magic-link sign-in successful",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = now.AddDays(refreshTokenDays),
            ExpiresIn = accessTokenMinutes * 60,
            AccessTokenExpiresAt = now.AddMinutes(accessTokenMinutes),
            RefreshTokenExpiresAt = now.AddDays(refreshTokenDays),
            UserId = user.Id,
            Email = user.Email,
            TenantId = request.TenantId,
            SessionId = Guid.NewGuid()
        };
    }

    private static int ParsePositiveInt(string? value, int fallback)
        => int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}
