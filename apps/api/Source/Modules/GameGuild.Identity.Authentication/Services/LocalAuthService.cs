using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.CQRS;
using GameGuild.Email;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authentication;

/// <summary>
/// Local authentication: sign-in, sign-up, refresh token rotation, token revocation
/// </summary>
public class LocalAuthService(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IRefreshTokenLineageRepository tokenLineageRepository,
    IJwtTokenService jwtTokenService,
    IRefreshTokenHasher refreshTokenHasher,
    IConfiguration configuration,
    IAuthAttemptService authAttemptService,
    IPasswordHasher passwordHasher,
#pragma warning disable CS9113 // Parameter is unread - reserved for future use
    IAuthenticationAnomalyDetectionService anomalyDetectionService,
#pragma warning restore CS9113
    IUserEnumerationProtectionService enumerationProtection,
    IHttpContextAccessor httpContextAccessor,
    ILogger<LocalAuthService> logger,
    ISender sender,
    ISessionManagementService sessionManagementService,
    IOptions<JwtOptions>? jwtOptions = null,
    IAuthenticationAuditEventSink? auditEventSink = null,
    IRefreshTokenLifecycleRecorder? lifecycleRecorder = null,
    ISuspiciousLoginAlertPublisher? suspiciousLoginAlerts = null
) : ILocalAuthService
{
    public async Task<SignInResponse> LocalSignInAsync(LocalSignInRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var httpContext = httpContextAccessor.HttpContext;
        var ipAddress = authAttemptService.GetClientIpAddress(httpContext);
        var userAgent = httpContext?.Request.Headers.UserAgent.ToString() ?? string.Empty;
        var deviceFingerprint = httpContext?.Request.Headers["X-Device-Fingerprint"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(deviceFingerprint))
        {
            deviceFingerprint = request.DeviceFingerprint;
        }

        // Digest of the candidate password for breached-password (credential-stuffing)
        // threat-intelligence matching. Only the digest travels on the attempt context;
        // it is never persisted or logged.
        var candidatePasswordSha256Hex = ComputePasswordSha256Hex(request.Password);

#pragma warning disable IDE0059 // Unnecessary assignment - Initial null IS used in failure path at RecordFailedAttempt
        Guid? userId = null;
#pragma warning restore IDE0059
        var userExists = false;
        var authenticationSucceeded = false;
        string? failureReason = null;

        try
        {
            // Lookup user from database
            var normalizedEmail = request.Email.ToLowerInvariant();
            var user = request.CredentialResolutionFailed ? null
                : request.ResolvedUserId.HasValue
                    ? await userRepository.GetByIdAsync(request.ResolvedUserId.Value, cancellationToken).ConfigureAwait(false)
                    : await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken).ConfigureAwait(false);
            userExists = user != null;

            // Verify password if user exists
            if (user != null)
            {
                var passwordValid = user.HasPassword && passwordHasher.VerifyPassword(user.PasswordHash!, request.Password);

                if (passwordValid)
                {
                    authenticationSucceeded = true;
                    userId = user.Id;
                    logger.LogInformation("User {Email} authenticated successfully with ID {UserId}", LogRedaction.MaskEmail(user.Email), userId);
                }
                else
                {
                    failureReason = "InvalidCredentials";
                    logger.LogWarning("Invalid password for user {Email}", LogRedaction.MaskEmail(request.Email));
                }
            }
            else
            {
                failureReason = "InvalidCredentials";
                logger.LogWarning("User not found: {Email}", LogRedaction.MaskEmail(request.Email));
            }

            // Apply user enumeration protection timing
            await enumerationProtection.AddTimingProtectionDelayAsync(userExists, SystemClock.UtcNow).ConfigureAwait(false);

            if (!authenticationSucceeded)
            {
                try
                {
                    await authAttemptService.RecordFailedAttemptAsync(request.Email, userId, ipAddress, userAgent, failureReason!, stopwatch.Elapsed).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    // Risk analysis and central audit must still run when the local attempt service is unavailable.
                    logger.LogError(exception, "Could not record failed authentication attempt for user {UserId}", userId);
                }

                var failedAttemptContext = CreateAttemptContext(
                    request.Email, userId, ipAddress, userAgent, request.TenantId, deviceFingerprint, candidatePasswordSha256Hex);
                var failedAttemptAnalysis = await AnalyzeAttemptForAuditAsync(failedAttemptContext).ConfigureAwait(false);
                if (failedAttemptAnalysis is { IsAnomalous: true })
                {
                    await RecordRiskAuditEventAsync(
                        "Authentication.ThreatDetected",
                        userId,
                        request.TenantId,
                        ipAddress,
                        userAgent,
                        failureReason,
                        failedAttemptAnalysis).ConfigureAwait(false);
                }

                // Brute force against a known account must reach the owner even when the attempt
                // fails. The publisher gates on the configured severity (High by default).
                if (failedAttemptAnalysis is not null
                    && userId.HasValue
                    && failedAttemptAnalysis.DetectedAnomalies.Contains(SecurityAlertKinds.BruteForceDetected, StringComparer.Ordinal))
                {
                    await RecordSuspiciousLoginAlertAsync(
                        userId, request.TenantId, failedAttemptAnalysis, SecurityAlertKinds.BruteForceDetected, cancellationToken).ConfigureAwait(false);
                }

                throw new UnauthorizedAccessException(enumerationProtection.GetGenericErrorMessage("login"));
            }

            var authenticatedUserId = userId ?? throw new InvalidOperationException("A successful authentication must have a user ID.");

            // Analyze login attempt for anomalies
            var attemptContext = CreateAttemptContext(
                request.Email, userId, ipAddress, userAgent, request.TenantId, deviceFingerprint, candidatePasswordSha256Hex);

            var anomalyResult = await anomalyDetectionService.AnalyzeLoginAttemptAsync(attemptContext).ConfigureAwait(false);
            var behavioralAnalysis = await AnalyzeBehavioralPatternsForAuditAsync(authenticatedUserId, attemptContext).ConfigureAwait(false);

            var requiresStepUp = anomalyResult.RiskLevel >= RiskLevel.High;
            if (requiresStepUp)
            {
                await RecordRiskAuditEventAsync(
                    "Authentication.StepUpRequired",
                    userId,
                    request.TenantId,
                    ipAddress,
                    userAgent,
                    "StepUpRequired",
                    anomalyResult,
                    authenticationSucceeded: false,
                    behavioralAnalysis: behavioralAnalysis).ConfigureAwait(false);

                // The step-up challenge is a confirmed high-risk signal: alert the account owner.
                await RecordSuspiciousLoginAlertAsync(
                    userId, request.TenantId, anomalyResult, SecurityAlertKinds.LoginStepUpRequired, cancellationToken).ConfigureAwait(false);
            }

            // Require step-up authentication for high-risk logins
            if (anomalyResult.RiskLevel >= RiskLevel.High)
            {
                logger.LogWarning("High-risk login attempt detected: UserId={UserId}, RiskLevel={RiskLevel}, Anomalies={Anomalies}",
                    authenticatedUserId, anomalyResult.RiskLevel, string.Join(", ", anomalyResult.DetectedAnomalies));

                var stepUpToken = Guid.NewGuid().ToString("N");
                var stepUpExpiresAt = SystemClock.UtcNow.AddMinutes(5);

                return new SignInResponse
                {
                    Success = false,
                    Message = "Additional verification required",
                    RequiresStepUp = true,
                    StepUpToken = stepUpToken,
                    StepUpExpiresAt = stepUpExpiresAt,
                    RiskLevel = anomalyResult.RiskLevel,
                    RiskFactors = anomalyResult.DetectedAnomalies.ToList(),
                    AvailableMethods = ["TOTP", "Email"],
                    UserId = authenticatedUserId,
                    Email = request.Email,
                    TenantId = request.TenantId
                };
            }

            // Create device info for refresh token
            var deviceInfo = new DeviceInfo { Fingerprint = string.IsNullOrWhiteSpace(deviceFingerprint) ? Guid.NewGuid().ToString() : deviceFingerprint, IpAddress = ipAddress, UserAgent = userAgent, DeviceName = "Test Device", DeviceType = "Web" };

            // Fetch user again to get token version
            var authenticatedUser = await userRepository.GetByIdAsync(authenticatedUserId, cancellationToken).ConfigureAwait(false);
            var tokenVersion = authenticatedUser?.TokenVersion ?? 1;
            await DefaultTenantMembershipProvisioner.EnsureAsync(sender, authenticatedUserId, cancellationToken).ConfigureAwait(false);
            var tenantAccessContext = await ResolveTenantAccessContextAsync(authenticatedUserId, request.TenantId, cancellationToken).ConfigureAwait(false);
            RequireActiveTenantAccess(tenantAccessContext);

            var refreshTokenExpiryDays = jwtOptions?.Value.RefreshTokenExpirationDays
                                         ?? int.Parse(configuration["Jwt:RefreshTokenExpirationDays"] ?? configuration["Jwt:RefreshTokenExpiryInDays"] ?? "7", CultureInfo.InvariantCulture);
            var refreshTokenExpiresAt = SystemClock.UtcNow.AddDays(refreshTokenExpiryDays);
            var sessionId = Guid.NewGuid();
            var refreshToken = await jwtTokenService.GenerateRefreshTokenAsync(authenticatedUserId, deviceInfo, cancellationToken).ConfigureAwait(false);
            var accessToken = await jwtTokenService.GenerateAccessTokenAsync(
                authenticatedUserId,
                authenticatedUser?.Email ?? request.Email,
                tenantAccessContext.Roles.ToArray(),
                tenantAccessContext.TenantId,
                tokenVersion,
                sessionId,
                cancellationToken).ConfigureAwait(false);
            var refreshTokenHash = refreshTokenHasher.HashToken(refreshToken);
            var session = await sessionManagementService.CreateSessionAsync(
                sessionId,
                authenticatedUserId,
                ipAddress ?? "unknown",
                userAgent ?? string.Empty,
                refreshTokenHash,
                refreshTokenExpiresAt,
                deviceInfo.Fingerprint,
                cancellationToken).ConfigureAwait(false);
            refreshTokenExpiresAt = AuthenticatedSessionDeadline.Require(
                session, authenticatedUserId, sessionId, refreshTokenHash, refreshTokenExpiresAt);

            if (anomalyResult.IsAnomalous || behavioralAnalysis is { MatchesTypicalBehavior: false })
            {
                await RecordRiskAuditEventAsync(
                    "Authentication.ThreatDetected",
                    authenticatedUserId,
                    request.TenantId,
                    ipAddress ?? "unknown",
                    userAgent,
                    null,
                    anomalyResult,
                    authenticationSucceeded: true,
                    behavioralAnalysis: behavioralAnalysis).ConfigureAwait(false);
            }

            // Confirmed signals on a successful sign-in: impossible travel, and brute force that
            // eventually succeeded (classic account-takeover pattern). Both must reach the owner.
            if (anomalyResult.DetectedAnomalies.Contains(SecurityAlertKinds.ImpossibleTravel, StringComparer.Ordinal))
            {
                await RecordSuspiciousLoginAlertAsync(
                    authenticatedUserId, request.TenantId, anomalyResult, SecurityAlertKinds.ImpossibleTravel, cancellationToken).ConfigureAwait(false);
            }

            if (anomalyResult.DetectedAnomalies.Contains(SecurityAlertKinds.BruteForceDetected, StringComparer.Ordinal))
            {
                await RecordSuspiciousLoginAlertAsync(
                    authenticatedUserId, request.TenantId, anomalyResult, SecurityAlertKinds.BruteForceDetected, cancellationToken).ConfigureAwait(false);
            }

            // Record successful login attempt
            await authAttemptService.RecordSuccessfulAttemptAsync(request.Email, authenticatedUserId, ipAddress ?? "unknown", userAgent, stopwatch.Elapsed).ConfigureAwait(false);

            var accessTokenExpirationMinutes = jwtOptions?.Value.AccessTokenExpirationMinutes
                                               ?? int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60", CultureInfo.InvariantCulture);

            return new SignInResponse
            {
                Success = true,
                Message = "Sign-in successful",
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = refreshTokenExpiresAt,
                ExpiresIn = accessTokenExpirationMinutes * 60,
                AccessTokenExpiresAt = SystemClock.UtcNow.AddMinutes(accessTokenExpirationMinutes),
                RefreshTokenExpiresAt = refreshTokenExpiresAt,
                UserId = authenticatedUserId,
                Email = authenticatedUser?.Email ?? request.Email,
                SessionId = sessionId,
                TenantId = tenantAccessContext.TenantId,
                AvailableTenants = tenantAccessContext.AvailableTenants
            };
        }
        catch (SecurityException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during authentication for {Email}", LogRedaction.MaskEmail(request.Email));

            await authAttemptService.RecordFailedAttemptAsync(request.Email, userId, ipAddress, userAgent, "SystemError", stopwatch.Elapsed).ConfigureAwait(false);

            throw new UnauthorizedAccessException(enumerationProtection.GetGenericErrorMessage("login"));
        }
    }

    private AuthenticationAttemptContext CreateAttemptContext(
        string identifier,
        Guid? userId,
        string ipAddress,
        string? userAgent,
        Guid? tenantId,
        string? deviceFingerprint,
        string? passwordSha256Hex = null) => new()
    {
        UserId = userId,
        Identifier = identifier.ToLowerInvariant(),
        AuthenticationMethod = "Password",
        IpAddress = ipAddress,
        UserAgent = userAgent ?? "Unknown",
        DeviceFingerprint = deviceFingerprint,
        PasswordSha256Hex = passwordSha256Hex,
        TenantId = tenantId,
        AttemptedAt = SystemClock.UtcNow
    };

    private static string? ComputePasswordSha256Hex(string? password)
        => string.IsNullOrEmpty(password)
            ? null
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));

    private async Task<AuthenticationAnomalyResult?> AnalyzeAttemptForAuditAsync(AuthenticationAttemptContext attemptContext)
    {
        try
        {
            return await anomalyDetectionService.AnalyzeLoginAttemptAsync(attemptContext).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not analyze failed authentication attempt for user {UserId}", attemptContext.UserId);
            return null;
        }
    }

    private async Task<BehavioralAnalysisResult?> AnalyzeBehavioralPatternsForAuditAsync(
        Guid userId,
        AuthenticationAttemptContext attemptContext)
    {
        try
        {
            return await anomalyDetectionService.AnalyzeBehavioralPatternsAsync(userId, attemptContext).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not analyze authentication behavior for user {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    ///     Forwards a confirmed suspicious-login signal to the (optional) alert publisher, which
    ///     records a redacted durable event consumed host-side to queue the owner notification.
    ///     Absent publisher (hosts without the durable transport) degrades to a no-op.
    /// </summary>
    private async Task RecordSuspiciousLoginAlertAsync(
        Guid? userId,
        Guid? tenantId,
        AuthenticationAnomalyResult analysis,
        string alertKind,
        CancellationToken cancellationToken)
    {
        if (suspiciousLoginAlerts is null || userId is not { } alertUserId)
        {
            return;
        }

        await suspiciousLoginAlerts.RecordAsync(
            alertUserId, tenantId, alertKind, analysis.RiskLevel, analysis.RiskScore, cancellationToken).ConfigureAwait(false);
    }

    private async Task RecordRiskAuditEventAsync(
        string actionType,
        Guid? userId,
        Guid? tenantId,
        string ipAddress,
        string? userAgent,
        string? errorMessage,
        AuthenticationAnomalyResult? analysis,
        bool authenticationSucceeded = false,
        BehavioralAnalysisResult? behavioralAnalysis = null)
    {
        if (analysis is null && behavioralAnalysis is null)
        {
            return;
        }

        var riskScore = Math.Max(analysis?.RiskScore ?? 0, behavioralAnalysis?.RiskScore ?? 0);
        var riskLevel = (RiskLevel)Math.Max((int)(analysis?.RiskLevel ?? RiskLevel.Low), (int)(behavioralAnalysis?.RiskLevel ?? RiskLevel.Low));
        var riskFactors = (analysis?.DetectedAnomalies ?? [])
            .Concat(behavioralAnalysis?.DetectedAnomalies ?? [])
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        try
        {
            await anomalyDetectionService.RecordSuspiciousActivityAsync(new SuspiciousActivity
            {
                UserId = userId,
                ActivityType = actionType,
                Description = "Authentication risk analysis detected an unusual sign-in pattern.",
                IpAddress = ipAddress,
                UserAgent = userAgent,
                RiskScore = riskScore,
                RiskLevel = riskLevel,
                DetectedAt = SystemClock.UtcNow,
                ActionsTaken = actionType == "Authentication.StepUpRequired" ? ["StepUpRequired"] : [],
                Metadata = new Dictionary<string, string>
                {
                    ["authenticationMethod"] = "Password",
                    ["correlationId"] = httpContextAccessor.HttpContext?.TraceIdentifier ?? string.Empty,
                    ["riskFactors"] = string.Join(",", riskFactors)
                }
            }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not forward authentication threat event {ActionType} to SIEM", actionType);
        }

        if (auditEventSink is null)
        {
            return;
        }

        try
        {
            await auditEventSink.RecordAsync(new AuthenticationAuditEvent(
                actionType,
                userId,
                authenticationSucceeded,
                "Password",
                ipAddress,
                userAgent,
                TenantId: tenantId,
                ErrorMessage: errorMessage,
                AssessedRiskLevel: riskLevel,
                Metadata: new
                {
                    RiskScore = riskScore,
                    RiskLevel = riskLevel.ToString(),
                    RiskFactors = riskFactors,
                    BehavioralAnalysis = behavioralAnalysis is null ? null : new
                    {
                        behavioralAnalysis.RiskScore,
                        RiskLevel = behavioralAnalysis.RiskLevel.ToString(),
                        behavioralAnalysis.Confidence,
                        behavioralAnalysis.MatchesTypicalBehavior,
                        Deviations = behavioralAnalysis.DetectedAnomalies
                    },
                    CorrelationId = httpContextAccessor.HttpContext?.TraceIdentifier
                }),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not record authentication risk event {ActionType}", actionType);
        }
    }

    public async Task<SignInResponse> LocalSignUpAsync(LocalSignUpRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var httpContext = httpContextAccessor.HttpContext;
        var ipAddress = authAttemptService.GetClientIpAddress(httpContext);
        var userAgent = httpContext?.Request.Headers.UserAgent.ToString();

        try
        {
            var username = UsernameSlug.Normalize(request.Username);
            if (request.Username is not { Length: >= 3 and <= 50 } || username is not { Length: >= 3 and <= 50 })
            {
                throw new RequestValidationException(
                    [new ValidationError("Username", "Username must produce a handle of 3 to 50 characters.")]);
            }

            var passwordValidation = passwordHasher.ValidatePasswordStrength(request.Password);
            if (!passwordValidation.IsValid)
            {
                throw new RequestValidationException(
                    passwordValidation.ValidationFailures.Select(failure => new ValidationError("Password", failure)));
            }

            // Check for existing user
            var emailExists = await userRepository.ExistsByEmailAsync(request.Email.ToLowerInvariant(), cancellationToken).ConfigureAwait(false);

            if (emailExists)
            {
                await enumerationProtection.AddTimingProtectionDelayAsync(true, SystemClock.UtcNow).ConfigureAwait(false);
                logger.LogWarning("Sign-up attempt with existing email: {Email}", LogRedaction.MaskEmail(request.Email));

                throw new InvalidOperationException("User already exists");
            }

            var passwordHash = passwordHasher.HashPassword(request.Password);

            // Create new user using the unified User entity
            var newUser = User.CreateWithPassword(
                request.Email.ToLowerInvariant(),
                request.Username ?? request.Email.Split('@')[0],
                passwordHash,
                username);
            newUser.AddIntegrationEvent(new UserCreatedEvent(newUser.Id)
            {
                TenantId = request.TenantId ?? DurableIntegrationEventTenants.Platform,
                ActorId = newUser.Id,
                AggregateType = nameof(User),
                AggregateId = newUser.Id.ToString(),
                CorrelationId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow
            });

            // Save to database
            await userRepository.AddAsync(newUser, cancellationToken).ConfigureAwait(false);
            await userRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            var userId = newUser.Id;

            logger.LogInformation("Created new user with ID: {UserId} and Email: {Email}", userId, LogRedaction.MaskEmail(newUser.Email));

            await DefaultTenantMembershipProvisioner.EnsureAsync(sender, userId, cancellationToken).ConfigureAwait(false);

            // Create device info for refresh token
            var deviceInfo = new DeviceInfo { Fingerprint = Guid.NewGuid().ToString(), IpAddress = ipAddress, UserAgent = userAgent, DeviceName = "Test Device", DeviceType = "Web" };

            var tenantAccessContext = await ResolveTenantAccessContextAsync(userId, request.TenantId, cancellationToken).ConfigureAwait(false);

            var refreshTokenExpiryDays = jwtOptions?.Value.RefreshTokenExpirationDays
                                         ?? int.Parse(configuration["Jwt:RefreshTokenExpirationDays"] ?? configuration["Jwt:RefreshTokenExpiryInDays"] ?? "7", CultureInfo.InvariantCulture);
            var refreshTokenExpiresAt = SystemClock.UtcNow.AddDays(refreshTokenExpiryDays);
            var sessionId = Guid.NewGuid();
            var refreshToken = await jwtTokenService.GenerateRefreshTokenAsync(userId, deviceInfo, cancellationToken).ConfigureAwait(false);
            var accessToken = await jwtTokenService.GenerateAccessTokenAsync(
                userId,
                newUser.Email,
                tenantAccessContext.Roles.ToArray(),
                tenantAccessContext.TenantId,
                newUser.TokenVersion,
                sessionId,
                cancellationToken).ConfigureAwait(false);
            var refreshTokenHash = refreshTokenHasher.HashToken(refreshToken);
            var session = await sessionManagementService.CreateSessionAsync(
                sessionId,
                userId,
                ipAddress ?? "unknown",
                userAgent ?? string.Empty,
                refreshTokenHash,
                refreshTokenExpiresAt,
                deviceInfo.Fingerprint,
                cancellationToken).ConfigureAwait(false);
            refreshTokenExpiresAt = AuthenticatedSessionDeadline.Require(
                session, userId, sessionId, refreshTokenHash, refreshTokenExpiresAt);

            // Record successful registration
            await authAttemptService.RecordSuccessfulAttemptAsync(request.Email, userId, ipAddress ?? "unknown", userAgent, stopwatch.Elapsed, "Registration").ConfigureAwait(false);
            logger.LogInformation("User {Email} successfully signed up", LogRedaction.MaskEmail(request.Email));

            var accessTokenExpirationMinutes = jwtOptions?.Value.AccessTokenExpirationMinutes
                                               ?? int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60", CultureInfo.InvariantCulture);

            return new SignInResponse
            {
                Success = true,
                Message = "Sign-up successful",
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = refreshTokenExpiresAt,
                ExpiresIn = accessTokenExpirationMinutes * 60,
                AccessTokenExpiresAt = SystemClock.UtcNow.AddMinutes(accessTokenExpirationMinutes),
                RefreshTokenExpiresAt = refreshTokenExpiresAt,
                UserId = userId,
                Email = newUser.Email,
                SessionId = sessionId,
                TenantId = tenantAccessContext.TenantId,
                AvailableTenants = tenantAccessContext.AvailableTenants
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during user registration for {Email}", LogRedaction.MaskEmail(request.Email));

            throw;
        }
    }

    public async Task<SignInResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RefreshTokenLifecycleMetrics.RecordAttempt(RefreshTokenLifecycleOperation.Rotated);
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            logger.LogWarning("Refresh token request rejected because the token was missing.");
            await RecordRefreshRejectionAsync(null, RefreshTokenLifecycleReason.Missing, cancellationToken).ConfigureAwait(false);

            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        var httpContext = httpContextAccessor.HttpContext;
        var ipAddress = authAttemptService.GetClientIpAddress(httpContext);
        var userAgent = httpContext?.Request.Headers.UserAgent.ToString();

        // Hash the incoming token to match against stored hash
        var hashedToken = refreshTokenHasher.HashToken(request.RefreshToken);
        var storedToken = await refreshTokenRepository.GetByTokenAsync(hashedToken, cancellationToken).ConfigureAwait(false);
        var now = SystemClock.UtcNow;

        if (storedToken == null)
        {
            await RecordRefreshRejectionAsync(null, RefreshTokenLifecycleReason.Unknown, cancellationToken).ConfigureAwait(false);
            logger.LogWarning(
                "Invalid refresh token attempt from {IpAddress}. TokenFound: {TokenFound}, IsActive: {IsActive}, ExpiresAt: {ExpiresAt}",
                ipAddress,
                storedToken != null,
                storedToken?.IsActive,
                storedToken?.ExpiresAt
            );

            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        if (storedToken.IsRevoked || storedToken.ReplacedByToken != null)
        {
            logger.LogWarning(
                "Rejected refresh token replay from {IpAddress}. RevokedAt: {RevokedAt}, RevokedByIp: {RevokedByIp}",
                ipAddress,
                storedToken.RevokedAt,
                storedToken.RevokedByIp
            );

            await InvalidateSessionsAfterRefreshReplayAsync(storedToken, ipAddress, cancellationToken)
                .ConfigureAwait(false);
            await RecordRefreshMutationAsync(new RefreshTokenLifecycleEvent(RefreshTokenLifecycleOperation.ReplayContained,
                storedToken.UserId, storedToken.Id, storedToken.SessionId, Reason: RefreshTokenLifecycleReason.Revoked), cancellationToken).ConfigureAwait(false);

            // A thrown denial would roll back the containment in the command transaction.
            return new RefreshTokenContainmentDenial();
        }

        if (storedToken.ExpiresAt <= now)
        {
            await RecordRefreshRejectionAsync(storedToken, RefreshTokenLifecycleReason.Expired, cancellationToken).ConfigureAwait(false);
            logger.LogWarning(
                "Invalid expired refresh token attempt from {IpAddress} for user {UserId}",
                ipAddress,
                storedToken.UserId);

            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        var userId = storedToken.UserId;
        var user = await userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            await RecordRefreshRejectionAsync(storedToken, RefreshTokenLifecycleReason.UserUnavailable, cancellationToken).ConfigureAwait(false);
            logger.LogWarning("Rejected refresh token for an unavailable user {UserId}", userId);
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        var tokenVersion = user.TokenVersion;
        await DefaultTenantMembershipProvisioner.EnsureAsync(sender, userId, cancellationToken).ConfigureAwait(false);
        var tenantAccessContext = await ResolveTenantAccessContextAsync(userId, request.TenantId, cancellationToken).ConfigureAwait(false);
        var userEmail = user.Email;
        try { RequireActiveTenantAccess(tenantAccessContext); }
        catch (AccessDeniedException)
        {
            await RecordRefreshRejectionAsync(storedToken, RefreshTokenLifecycleReason.TenantDenied, cancellationToken).ConfigureAwait(false);
            throw;
        }

        // Create device info for refresh token
        var deviceInfo = new DeviceInfo { Fingerprint = Guid.NewGuid().ToString(), IpAddress = ipAddress, UserAgent = userAgent, DeviceName = "Test Device", DeviceType = "Web" };
        var authenticatedAt = new DateTimeOffset(
            DateTime.SpecifyKind(
                storedToken.CreatedAt > DateTime.UnixEpoch ? storedToken.CreatedAt : now,
                DateTimeKind.Utc));

        var existingSession = await sessionManagementService.GetSessionByRefreshTokenAsync(hashedToken, cancellationToken).ConfigureAwait(false);
        var sessionId = existingSession?.Id ?? Guid.NewGuid();
        var refreshTokenExpiryDays = jwtOptions?.Value.RefreshTokenExpirationDays
                                     ?? int.Parse(configuration["Jwt:RefreshTokenExpirationDays"] ?? configuration["Jwt:RefreshTokenExpiryInDays"] ?? "7", CultureInfo.InvariantCulture);
        var newRefreshToken = await jwtTokenService.GenerateRefreshTokenAsync(userId, deviceInfo, authenticatedAt, cancellationToken).ConfigureAwait(false);
        var refreshTokenExpiresAt = now.AddDays(refreshTokenExpiryDays);
        var slidingExpiration = jwtOptions?.Value.RefreshTokenSlidingExpiration
                                ?? bool.Parse(configuration["Jwt:RefreshTokenSlidingExpiration"] ?? bool.TrueString);
        if (!slidingExpiration && refreshTokenExpiresAt > storedToken.ExpiresAt)
        {
            refreshTokenExpiresAt = storedToken.ExpiresAt;
        }
        var replacementTokenHash = refreshTokenHasher.HashToken(newRefreshToken);
        var accessToken = await jwtTokenService.GenerateAccessTokenAsync(
            userId,
            userEmail,
            tenantAccessContext.Roles.ToArray(),
            tenantAccessContext.TenantId,
            tokenVersion,
            authenticatedAt,
            sessionId,
            cancellationToken).ConfigureAwait(false);

        if (existingSession == null)
        {
            await sessionManagementService.CreateSessionAsync(
                sessionId,
                userId,
                ipAddress ?? "unknown",
                userAgent ?? string.Empty,
                replacementTokenHash,
                refreshTokenExpiresAt,
                deviceInfo.Fingerprint,
                cancellationToken).ConfigureAwait(false);
        }
        else if (!await sessionManagementService.RefreshSessionAsync(
                     sessionId,
                     replacementTokenHash,
                     refreshTokenExpiresAt,
                     cancellationToken).ConfigureAwait(false))
        {
            logger.LogWarning("Refresh token session {SessionId} was no longer active for user {UserId}; invalidating sessions", sessionId, userId);
            await InvalidateSessionsAfterRefreshReplayAsync(storedToken, ipAddress, cancellationToken).ConfigureAwait(false);
            await RecordRefreshMutationAsync(new RefreshTokenLifecycleEvent(RefreshTokenLifecycleOperation.ReplayContained,
                userId, storedToken.Id, sessionId, tenantAccessContext.TenantId, Reason: RefreshTokenLifecycleReason.SessionInactive), cancellationToken).ConfigureAwait(false);
            return new RefreshTokenContainmentDenial();
        }

        var rotationClaimed = await refreshTokenRepository.TryRevokeForRotationAsync(
            storedToken.Id,
            hashedToken,
            replacementTokenHash,
            now,
            ipAddress,
            cancellationToken).ConfigureAwait(false);

        if (!rotationClaimed)
        {
            logger.LogWarning("Refresh token rotation lost a concurrent claim for user {UserId}; invalidating sessions", userId);
            await InvalidateSessionsAfterRefreshReplayAsync(storedToken, ipAddress, cancellationToken).ConfigureAwait(false);
            await RecordRefreshMutationAsync(new RefreshTokenLifecycleEvent(RefreshTokenLifecycleOperation.ReplayContained,
                userId, storedToken.Id, sessionId, tenantAccessContext.TenantId, Reason: RefreshTokenLifecycleReason.ConcurrentRotation), cancellationToken).ConfigureAwait(false);
            return new RefreshTokenContainmentDenial();
        }

        await tokenLineageRepository.RecordRotationAsync(userId, storedToken.Id, replacementTokenHash, sessionId, cancellationToken)
            .ConfigureAwait(false);
        var refreshedSession = await sessionManagementService.GetSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        refreshTokenExpiresAt = AuthenticatedSessionDeadline.Require(
            refreshedSession, userId, sessionId, replacementTokenHash, refreshTokenExpiresAt);
        await RecordRefreshMutationAsync(new RefreshTokenLifecycleEvent(RefreshTokenLifecycleOperation.Rotated,
            userId, storedToken.Id, sessionId, tenantAccessContext.TenantId, storedToken.ParentTokenId), cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Refresh token rotated for user {UserId}", userId);

        var accessTokenExpirationMinutes = jwtOptions?.Value.AccessTokenExpirationMinutes
                                          ?? int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60", CultureInfo.InvariantCulture);

        return new SignInResponse
        {
            Success = true,
            Message = "Token refreshed successfully",
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = refreshTokenExpiresAt,
            ExpiresIn = accessTokenExpirationMinutes * 60,
            AccessTokenExpiresAt = SystemClock.UtcNow.AddMinutes(accessTokenExpirationMinutes),
            RefreshTokenExpiresAt = refreshTokenExpiresAt,
            UserId = userId,
            Email = userEmail,
            SessionId = sessionId,
            TenantId = tenantAccessContext.TenantId,
            AvailableTenants = tenantAccessContext.AvailableTenants
        };
    }

    private async Task InvalidateSessionsAfterRefreshReplayAsync(
        RefreshToken token,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var userId = token.UserId;
        var scope = jwtOptions?.Value.RefreshTokenReplayContainmentScope ?? JwtOptionsResolver.ResolveReplayScope(configuration);
        if (scope == RefreshTokenReplayScope.Family)
        {
            var familySessionId = await tokenLineageRepository.RevokeFamilyAsync(userId, token.Id, ipAddress, cancellationToken)
                .ConfigureAwait(false);
            if (familySessionId.HasValue)
            {
                await sessionManagementService.TerminateSessionAsync(familySessionId.Value, SessionTerminationReason.SecurityViolation, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
        }
        else if (scope != RefreshTokenReplayScope.Account)
        {
            throw new InvalidOperationException("JWT RefreshTokenReplayContainmentScope must be Family or Account");
        }
        await refreshTokenRepository.RevokeAllForUserAsync(userId, ipAddress, cancellationToken)
            .ConfigureAwait(false);
        await sessionManagementService.TerminateAllUserSessionsAsync(
            userId,
            SessionTerminationReason.SecurityViolation,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var compromisedUser = await userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (compromisedUser is null)
        {
            return;
        }

        compromisedUser.IncrementTokenVersion();
        await userRepository.UpdateAsync(compromisedUser, cancellationToken).ConfigureAwait(false);
        await userRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static TenantAccessContext RequireActiveTenantAccess(TenantAccessContext tenantAccessContext)
    {
        if (tenantAccessContext.TenantId.HasValue)
        {
            return tenantAccessContext;
        }

        throw new AccessDeniedException("Authenticated user has no active tenant membership.");
    }

    private async Task<TenantAccessContext> ResolveTenantAccessContextAsync(Guid userId, Guid? requestedTenantId, CancellationToken cancellationToken)
    {
        var memberships = await sender.Send(new global::GameGuild.Identity.Tenants.GetUserMembershipsQuery(userId), cancellationToken).ConfigureAwait(false);

        return TenantAccessContextResolver.Resolve(memberships, requestedTenantId);
    }

    public async Task RevokeRefreshTokenAsync(string token, string ipAddress, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RefreshTokenLifecycleMetrics.RecordAttempt(RefreshTokenLifecycleOperation.Revoked);
        // Hash the incoming token to match against stored hash
        var hashedToken = refreshTokenHasher.HashToken(token);
        var refreshToken = await refreshTokenRepository.GetByTokenAsync(hashedToken, cancellationToken).ConfigureAwait(false);

        if (refreshToken == null || !refreshToken.IsActive)
        {
            await RecordRefreshRejectionAsync(refreshToken, refreshToken is null ? RefreshTokenLifecycleReason.Unknown :
                refreshToken.IsRevoked ? RefreshTokenLifecycleReason.Revoked : RefreshTokenLifecycleReason.Expired, cancellationToken).ConfigureAwait(false);
            throw new ArgumentException("Invalid token");
        }

        refreshToken.IsRevoked = true;
        refreshToken.RevokedAt = SystemClock.UtcNow;
        refreshToken.RevokedByIp = ipAddress;
        refreshToken.UpdatedAt = SystemClock.UtcNow;

        await refreshTokenRepository.UpdateAsync(refreshToken, cancellationToken).ConfigureAwait(false);

        var session = await sessionManagementService.GetSessionByRefreshTokenAsync(hashedToken, cancellationToken).ConfigureAwait(false);
        if (session != null)
        {
            await sessionManagementService.TerminateSessionAsync(session.Id, SessionTerminationReason.UserLogout, cancellationToken).ConfigureAwait(false);
        }
        await RecordRefreshMutationAsync(new RefreshTokenLifecycleEvent(RefreshTokenLifecycleOperation.Revoked,
            refreshToken.UserId, refreshToken.Id, session?.Id ?? refreshToken.SessionId), cancellationToken).ConfigureAwait(false);
    }

    private Task RecordRefreshMutationAsync(RefreshTokenLifecycleEvent lifecycleEvent, CancellationToken cancellationToken) =>
        lifecycleRecorder?.RecordMutationAsync(lifecycleEvent, cancellationToken) ?? Task.CompletedTask;

    private Task RecordRefreshRejectionAsync(RefreshToken? token, RefreshTokenLifecycleReason reason, CancellationToken cancellationToken) =>
        lifecycleRecorder?.RecordRejectionAsync(new RefreshTokenLifecycleEvent(RefreshTokenLifecycleOperation.Rejected,
            token?.UserId, token?.Id, token?.SessionId, Reason: reason), cancellationToken) ?? Task.CompletedTask;
}
