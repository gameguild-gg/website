using GameGuild.CQRS;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

            try
            {
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
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to dispatch magic-link notification for user {UserId}", user.Id);
            }

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

public sealed class ConsumeMagicLinkCommandHandler : ICommandHandler<ConsumeMagicLinkCommand, SignInResponse>
{
    private readonly IUserRepository userRepository;
    private readonly IEmailVerificationService emailVerificationService;
    private readonly ILogger<ConsumeMagicLinkCommandHandler> logger;
    private readonly IAuthenticatedSessionIssuer? sessionIssuer;

    public ConsumeMagicLinkCommandHandler(IUserRepository userRepository, IEmailVerificationService emailVerificationService,
        IJwtTokenService jwtTokenService, IConfiguration configuration, ILogger<ConsumeMagicLinkCommandHandler> logger)
        : this(userRepository, emailVerificationService, jwtTokenService, configuration, logger, null, null) { }

    public ConsumeMagicLinkCommandHandler(IUserRepository userRepository, IEmailVerificationService emailVerificationService,
        IJwtTokenService jwtTokenService, IConfiguration configuration, ILogger<ConsumeMagicLinkCommandHandler> logger,
        IOptions<JwtOptions>? jwtOptions)
        : this(userRepository, emailVerificationService, jwtTokenService, configuration, logger, jwtOptions, null) { }

    public ConsumeMagicLinkCommandHandler(
        IUserRepository userRepository,
        IEmailVerificationService emailVerificationService,
        IJwtTokenService jwtTokenService,
        IConfiguration configuration,
        ILogger<ConsumeMagicLinkCommandHandler> logger,
        IOptions<JwtOptions>? jwtOptions,
        IAuthenticatedSessionIssuer? sessionIssuer)
    {
        this.userRepository = userRepository;
        this.emailVerificationService = emailVerificationService;
        this.logger = logger;
        this.sessionIssuer = sessionIssuer;
    }

    public async Task<SignInResponse> Handle(ConsumeMagicLinkCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validation = await emailVerificationService.VerifyMagicLinkTokenAsync(request.Token).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!validation.Success || validation.UserId is not { } userId)
        {
            throw new AuthenticationRequiredException("Invalid or expired magic-link token");
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (user is null || user.IsDeleted || !user.ValidateForAuthentication(user.TokenVersion).IsSuccess)
        {
            throw new AuthenticationRequiredException("Invalid or expired magic-link token");
        }

        var issuer = sessionIssuer ?? throw new InvalidOperationException("Authenticated session issuer is not configured.");
        var response = await issuer.IssueAsync(
            user,
            request.TenantId,
            new DeviceInfo
            {
                Fingerprint = request.DeviceFingerprint ?? $"magic-link:{Guid.NewGuid():N}",
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent,
                DeviceName = "Magic Link",
                DeviceType = "Web"
            },
            cancellationToken).ConfigureAwait(false);
        response.Message = "Magic-link sign-in successful";
        logger.LogInformation("User {UserId} signed in with magic-link authentication", user.Id);
        return response;
    }

    private static int ParsePositiveInt(string? value, int fallback)
        => int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}
