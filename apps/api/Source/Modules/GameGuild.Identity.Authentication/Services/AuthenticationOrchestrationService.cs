using System.Text.Json;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Full authentication orchestration over the module's existing authentication services.
///     Validates the primary credential, evaluates risk with the existing anomaly analysis,
///     persists flow state with a bounded lifetime, delegates every challenge verification to
///     the existing MFA/WebAuthn services, and issues tokens through <see cref="IJwtTokenService"/>
///     only when every required step is satisfied on an unexpired flow.
/// </summary>
public sealed class AuthenticationOrchestrationService(
    IAuthenticationFlowStateRepository flowStateRepository,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IAuthAttemptService authAttemptService,
    IUserEnumerationProtectionService enumerationProtectionService,
    IAuthenticationAnomalyDetectionService anomalyDetectionService,
    ITrustedDeviceRepository trustedDeviceRepository,
    ITotpMfaService totpMfaService,
    IBackupCodeMfaService backupCodeMfaService,
    IWebAuthnAuthenticationService webAuthnAuthenticationService,
    IMfaAttemptTrackingService mfaAttemptTrackingService,
    IUserMfaConfigurationRepository userMfaConfigurationRepository,
    IRoleRepository roleRepository,
    IJwtTokenService jwtTokenService,
    IOptions<JwtOptions> jwtOptions,
    ILogger<AuthenticationOrchestrationService> logger) : IAuthenticationOrchestrationService
{
    /// <summary>Flow lifetime: flows expire 15 minutes after initiation.</summary>
    public static readonly TimeSpan FlowLifetime = TimeSpan.FromMinutes(15);

    /// <summary>Risk score at or above which the strongest challenge set is required.</summary>
    public const double HighRiskChallengeThreshold = 0.7;

    /// <summary>Risk score at or above which MFA verification is required.</summary>
    public const double MfaChallengeThreshold = 0.5;

    private const string LocalAuthMethod = "Local";
    private const string PasswordContextKey = "password";
    private const string TotpCodeKey = "totpCode";
    private const string BackupCodeKey = "backupCode";
    private const string WebAuthnAssertionKey = "webAuthnAssertion";

    private static readonly JsonSerializerOptions SerializationOptions = new(JsonSerializerDefaults.General);

    public async Task<AuthenticationFlowState> InitiateAuthenticationAsync(InitiateAuthenticationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var identifier = request.Identifier.Trim();
        if (identifier.Length == 0)
        {
            throw new ArgumentException("An identifier is required to initiate an authentication flow.", nameof(request));
        }

        var user = await ValidatePrimaryCredentialAsync(request, identifier).ConfigureAwait(false);

        var attemptContext = BuildAttemptContext(request, user.Id);
        var riskScore = await AssessRiskScoreAsync(user.Id, attemptContext).ConfigureAwait(false);

        var requiredSteps = new List<AuthenticationStep> { AuthenticationStep.PrimaryCredential };
        requiredSteps.AddRange(await DetermineRequiredChallengesAsync(user.Id, riskScore, attemptContext).ConfigureAwait(false));

        var completedSteps = new List<AuthenticationStep> { AuthenticationStep.PrimaryCredential };
        var now = SystemClock.UtcNow;

        var record = new AuthenticationFlowStateRecord
        {
            FlowId = Guid.NewGuid(),
            UserId = user.Id,
            CurrentStep = requiredSteps.Except(completedSteps).DefaultIfEmpty(AuthenticationStep.PrimaryCredential).First(),
            RequiredStepsJson = SerializeSteps(requiredSteps),
            CompletedStepsJson = SerializeSteps(completedSteps),
            IsComplete = false,
            RiskScore = riskScore,
            InitiatedAt = now,
            ExpiresAt = now.Add(FlowLifetime),
            IpAddress = request.IpAddress,
            DeviceFingerprint = request.DeviceFingerprint
        };

        await flowStateRepository.CreateAsync(record, CancellationToken.None).ConfigureAwait(false);

        logger.LogInformation(
            "Authentication flow {FlowId} initiated for user {UserId} with risk score {RiskScore} and steps {RequiredSteps}",
            record.FlowId, user.Id, riskScore, string.Join(",", requiredSteps));

        return ToState(record);
    }

    public async Task<List<AuthenticationStep>> DetermineRequiredChallengesAsync(Guid userId, double riskScore, AuthenticationAttemptContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var challenges = new List<AuthenticationStep>();
        if (riskScore >= HighRiskChallengeThreshold)
        {
            challenges.Add(AuthenticationStep.MfaVerification);
            challenges.Add(AuthenticationStep.RiskChallenge);
        }
        else if (riskScore >= MfaChallengeThreshold)
        {
            challenges.Add(AuthenticationStep.MfaVerification);
        }

        // An unrecognized device (missing or untrusted fingerprint) requires device trust.
        var fingerprint = context.DeviceFingerprint;
        var deviceRecognized = !string.IsNullOrWhiteSpace(fingerprint)
            && await trustedDeviceRepository.IsDeviceTrustedAsync(userId, fingerprint, CancellationToken.None).ConfigureAwait(false);
        if (!deviceRecognized)
        {
            challenges.Add(AuthenticationStep.DeviceTrust);
        }

        return [.. challenges.Distinct().OrderBy(step => (int)step)];
    }

    public async Task<AuthenticationFlowState> ProcessAuthenticationStepAsync(Guid flowId, AuthenticationStep step, object stepData)
    {
        var record = await LoadActiveFlowAsync(flowId).ConfigureAwait(false);
        var state = ToState(record);

        if (step == AuthenticationStep.PrimaryCredential)
        {
            // The primary credential is validated during initiation and can never be re-processed.
            throw new InvalidOperationException("The primary credential step is satisfied by flow initiation and cannot be processed.");
        }

        if (record.UserId is not { } userId)
        {
            throw new UnauthorizedAccessException($"Authentication flow '{flowId}' has no identified user.");
        }

        EnforceStepOrder(state, step);

        if (await mfaAttemptTrackingService.IsUserLockedOutAsync(userId, CancellationToken.None).ConfigureAwait(false))
        {
            throw new UnauthorizedAccessException("MFA verification is temporarily locked out for this account.");
        }

        var method = await VerifyChallengeAsync(record, step, stepData).ConfigureAwait(false);

        var completedSteps = DeserializeSteps(record.CompletedStepsJson);
        completedSteps.Add(step);
        var requiredSteps = DeserializeSteps(record.RequiredStepsJson);
        var isComplete = requiredSteps.All(completedSteps.Contains);

        var stepDataMap = DeserializeStepData(record.StepDataJson) ?? [];
        stepDataMap[step.ToString()] = method.ToString();

        record.CompletedStepsJson = SerializeSteps(completedSteps);
        record.CurrentStep = requiredSteps.Except(completedSteps).DefaultIfEmpty(step).First();
        record.IsComplete = isComplete;
        record.StepDataJson = JsonSerializer.Serialize(stepDataMap, SerializationOptions);

        await flowStateRepository.UpdateAsync(record, CancellationToken.None).ConfigureAwait(false);

        logger.LogInformation(
            "Authentication flow {FlowId} step {Step} satisfied via {Method}; completed {CompletedCount}/{RequiredCount}",
            flowId, step, method, completedSteps.Count, requiredSteps.Count);

        return ToState(record);
    }

    public async Task<AuthenticationResult> CompleteAuthenticationAsync(Guid flowId)
    {
        var record = await LoadActiveFlowAsync(flowId).ConfigureAwait(false);
        var state = ToState(record);

        var pending = state.RequiredSteps.Where(required => !state.CompletedSteps.Contains(required)).ToList();
        if (pending.Count > 0)
        {
            throw new InvalidOperationException(
                $"Authentication flow '{flowId}' cannot complete while steps [{string.Join(", ", pending)}] are pending.");
        }

        if (record.UserId is not { } userId)
        {
            throw new UnauthorizedAccessException($"Authentication flow '{flowId}' has no identified user.");
        }

        var user = await userRepository.GetByIdAsync(userId, CancellationToken.None).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException($"Authentication flow '{flowId}' belongs to a user that no longer exists.");
        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException($"Authentication flow '{flowId}' belongs to an inactive user.");
        }

        var roles = await roleRepository.GetUserRolesAsync(userId, includeExpired: false, cancellationToken: CancellationToken.None).ConfigureAwait(false);
        var roleNames = roles.Where(role => role.IsActive).Select(role => role.Name).ToArray();

        var sessionId = Guid.NewGuid();
        var accessToken = await jwtTokenService.GenerateAccessTokenAsync(
            userId,
            user.Email,
            roleNames,
            tenantId: null,
            user.TokenVersion,
            sessionId,
            CancellationToken.None).ConfigureAwait(false);
        var refreshToken = await jwtTokenService.GenerateRefreshTokenAsync(
            userId,
            new DeviceInfo
            {
                Fingerprint = string.IsNullOrWhiteSpace(record.DeviceFingerprint) ? Guid.NewGuid().ToString() : record.DeviceFingerprint,
                IpAddress = string.IsNullOrWhiteSpace(record.IpAddress) ? "unknown" : record.IpAddress,
                UserAgent = "AuthenticationOrchestration"
            },
            CancellationToken.None).ConfigureAwait(false);

        record.IsComplete = true;
        record.CompletedAt = SystemClock.UtcNow;
        record.CurrentStep = state.RequiredSteps.DefaultIfEmpty(AuthenticationStep.PrimaryCredential).Last();
        await flowStateRepository.UpdateAsync(record, CancellationToken.None).ConfigureAwait(false);

        logger.LogInformation("Authentication flow {FlowId} completed; tokens issued for user {UserId}", flowId, userId);

        return new OrchestrationAuthenticationResult
        {
            IsSuccess = true,
            UserId = userId,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenExpiresAt = SystemClock.UtcNow.AddMinutes(jwtOptions.Value.AccessTokenExpirationMinutes),
            SessionId = sessionId,
            MfaEnabled = state.RequiredSteps.Contains(AuthenticationStep.MfaVerification),
            DeviceTrusted = !state.RequiredSteps.Contains(AuthenticationStep.DeviceTrust),
            RiskScore = state.RiskScore,
            Metadata = new Dictionary<string, object>
            {
                ["flowId"] = flowId,
                ["requiredSteps"] = string.Join(",", state.RequiredSteps)
            }
        };
    }

    public async Task AbandonAuthenticationFlowAsync(Guid flowId)
    {
        await flowStateRepository.AbandonAsync(flowId, SystemClock.UtcNow, CancellationToken.None).ConfigureAwait(false);
        logger.LogInformation("Authentication flow {FlowId} abandoned", flowId);
    }

    public async Task<AuthenticationFlowState?> GetFlowStateAsync(Guid flowId)
    {
        var record = await flowStateRepository.GetByFlowIdAsync(flowId, CancellationToken.None).ConfigureAwait(false);
        if (record is null)
        {
            return null;
        }

        // Fail closed: abandoned and expired flows are not observable.
        if (record.AbandonedAt is not null)
        {
            return null;
        }

        if (SystemClock.UtcNow > record.ExpiresAt)
        {
            await flowStateRepository.AbandonAsync(flowId, SystemClock.UtcNow, CancellationToken.None).ConfigureAwait(false);
            return null;
        }

        return ToState(record);
    }

    private async Task<GameGuild.Identity.Users.User> ValidatePrimaryCredentialAsync(InitiateAuthenticationRequest request, string identifier)
    {
        if (!string.Equals(request.AuthMethod, LocalAuthMethod, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Authentication method '{request.AuthMethod}' is not orchestrated; only '{LocalAuthMethod}' primary credentials are supported.");
        }

        if (!TryReadPayload(request.Context, PasswordContextKey, out var password))
        {
            throw new UnauthorizedAccessException("A primary credential is required to initiate an authentication flow.");
        }

        var user = await userRepository.GetByEmailAsync(identifier.ToLowerInvariant(), CancellationToken.None).ConfigureAwait(false);
        var userExists = user is not null;
        var credentialValid = user is { IsActive: true, HasPassword: true }
            && passwordHasher.VerifyPassword(user.PasswordHash!, password);

        if (!credentialValid)
        {
            await enumerationProtectionService.AddTimingProtectionDelayAsync(userExists, SystemClock.UtcNow).ConfigureAwait(false);
            await authAttemptService.RecordFailedAttemptAsync(
                identifier,
                user?.Id,
                request.IpAddress,
                request.UserAgent,
                "InvalidCredentials",
                TimeSpan.Zero,
                request.AuthMethod).ConfigureAwait(false);
            logger.LogWarning("Primary credential validation failed for identifier {Identifier} from {IpAddress}", identifier, request.IpAddress);
            throw new UnauthorizedAccessException(enumerationProtectionService.GetGenericErrorMessage("login"));
        }

        await authAttemptService.RecordSuccessfulAttemptAsync(
            user!.Email,
            user.Id,
            request.IpAddress,
            request.UserAgent,
            TimeSpan.Zero,
            request.AuthMethod).ConfigureAwait(false);

        return user;
    }

    private async Task<double> AssessRiskScoreAsync(Guid userId, AuthenticationAttemptContext context)
    {
        try
        {
            var analysis = await anomalyDetectionService.AnalyzeLoginAttemptAsync(context).ConfigureAwait(false);
            return Math.Clamp(analysis.RiskScore / 100.0, 0.0, 1.0);
        }
        catch (Exception exception)
        {
            // Fail closed: when risk analysis is unavailable the flow must assume maximum risk.
            logger.LogError(exception, "Risk analysis failed for user {UserId}; assuming maximum risk score", userId);
            return 1.0;
        }
    }

    private async Task<AuthenticationFlowStateRecord> LoadActiveFlowAsync(Guid flowId)
    {
        var record = await flowStateRepository.GetByFlowIdAsync(flowId, CancellationToken.None).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException($"Authentication flow '{flowId}' was not found.");

        if (record.AbandonedAt is not null)
        {
            throw new UnauthorizedAccessException($"Authentication flow '{flowId}' was abandoned.");
        }

        if (SystemClock.UtcNow > record.ExpiresAt)
        {
            await flowStateRepository.AbandonAsync(flowId, SystemClock.UtcNow, CancellationToken.None).ConfigureAwait(false);
            throw new UnauthorizedAccessException($"Authentication flow '{flowId}' has expired.");
        }

        return record;
    }

    private async Task<MfaMethod> VerifyChallengeAsync(AuthenticationFlowStateRecord record, AuthenticationStep step, object stepData)
    {
        if (stepData is not IReadOnlyDictionary<string, object> payload)
        {
            logger.LogWarning("Step {Step} for flow {FlowId} received no verification payload", step, record.FlowId);
            throw new UnauthorizedAccessException($"Step '{step}' requires a verification payload.");
        }

        var userId = record.UserId!.Value;
        var deviceId = record.DeviceFingerprint;

        if (TryReadPayload(payload, TotpCodeKey, out var totpCode))
        {
            var verified = await totpMfaService.VerifyTotpAsync(userId, totpCode, deviceId, CancellationToken.None).ConfigureAwait(false);
            if (!verified)
            {
                throw await FailChallengeAsync(record, MfaMethod.Totp, "invalid TOTP code").ConfigureAwait(false);
            }

            await RecordSuccessAttemptAsync(userId, MfaMethod.Totp, deviceId).ConfigureAwait(false);
            return MfaMethod.Totp;
        }

        if (TryReadPayload(payload, BackupCodeKey, out var backupCode))
        {
            var verified = await backupCodeMfaService.VerifyBackupCodeAsync(userId, backupCode, deviceId, CancellationToken.None).ConfigureAwait(false);
            if (!verified)
            {
                throw await FailChallengeAsync(record, MfaMethod.BackupCode, "invalid backup code").ConfigureAwait(false);
            }

            await RecordSuccessAttemptAsync(userId, MfaMethod.BackupCode, deviceId).ConfigureAwait(false);
            return MfaMethod.BackupCode;
        }

        if (TryReadPayload(payload, WebAuthnAssertionKey, out var assertion))
        {
            var result = await webAuthnAuthenticationService
                .CompleteAuthenticationAsync(assertion, record.IpAddress, userAgent: null, CancellationToken.None)
                .ConfigureAwait(false);
            if (result is not { Success: true } || result.UserId != userId)
            {
                throw await FailChallengeAsync(record, MfaMethod.WebAuthn, "invalid WebAuthn assertion").ConfigureAwait(false);
            }

            await RecordSuccessAttemptAsync(userId, MfaMethod.WebAuthn, deviceId).ConfigureAwait(false);
            return MfaMethod.WebAuthn;
        }

        logger.LogWarning("Step {Step} for flow {FlowId} received no supported verification payload", step, record.FlowId);
        throw new UnauthorizedAccessException($"Step '{step}' received no supported verification payload.");
    }

    /// <summary>
    ///     Records a failed challenge attempt through the existing MFA attempt tracking (which applies
    ///     the existing lockout policy) and returns the exception to throw.
    /// </summary>
    private async Task<UnauthorizedAccessException> FailChallengeAsync(
        AuthenticationFlowStateRecord record,
        MfaMethod method,
        string failureReason)
    {
        var userId = record.UserId!.Value;
        logger.LogWarning(
            "Challenge {Step} failed for flow {FlowId} via {Method}: {Reason}",
            record.CurrentStep, record.FlowId, method, failureReason);

        try
        {
            var configuration = await userMfaConfigurationRepository.GetByUserIdAsync(userId, CancellationToken.None).ConfigureAwait(false);
            if (configuration is not null)
            {
                await mfaAttemptTrackingService
                    .RecordFailedMfaAttemptAsync(configuration, method, failureReason, record.DeviceFingerprint, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            else
            {
                await mfaAttemptTrackingService
                    .RecordMfaAttemptAsync(userId, method, success: false, failureReason, record.DeviceFingerprint, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not record failed challenge attempt for flow {FlowId}", record.FlowId);
        }

        return new UnauthorizedAccessException(failureReason);
    }

    private async Task RecordSuccessAttemptAsync(Guid userId, MfaMethod method, string? deviceId)
    {
        try
        {
            await mfaAttemptTrackingService
                .RecordMfaAttemptAsync(userId, method, success: true, failureReason: null, deviceId, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not record successful challenge attempt for user {UserId}", userId);
        }
    }

    private static void EnforceStepOrder(AuthenticationFlowState state, AuthenticationStep step)
    {
        if (!state.RequiredSteps.Contains(step))
        {
            throw new InvalidOperationException($"Step '{step}' is not required for this authentication flow.");
        }

        if (state.CompletedSteps.Contains(step))
        {
            throw new InvalidOperationException($"Step '{step}' has already been completed.");
        }

        if (state.NextStep is { } next && next != step)
        {
            throw new InvalidOperationException($"Step '{step}' cannot be processed before '{next}'.");
        }
    }

    private static AuthenticationAttemptContext BuildAttemptContext(InitiateAuthenticationRequest request, Guid userId) => new()
    {
        UserId = userId,
        Identifier = request.Identifier.ToLowerInvariant(),
        AuthenticationMethod = request.AuthMethod,
        IpAddress = string.IsNullOrWhiteSpace(request.IpAddress) ? "unknown" : request.IpAddress,
        UserAgent = string.IsNullOrWhiteSpace(request.UserAgent) ? "Unknown" : request.UserAgent,
        DeviceFingerprint = request.DeviceFingerprint,
        TenantId = request.TenantId,
        AttemptedAt = SystemClock.UtcNow
    };

    private static bool TryReadPayload(IReadOnlyDictionary<string, object>? payload, string key, out string value)
    {
        value = string.Empty;
        if (payload is null || !payload.TryGetValue(key, out var raw) || raw is null)
        {
            return false;
        }

        var text = raw.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        value = text.Trim();
        return true;
    }

    private static AuthenticationFlowState ToState(AuthenticationFlowStateRecord record) => new OrchestrationAuthenticationFlowState
    {
        FlowId = record.FlowId,
        UserId = record.UserId,
        CurrentStep = record.CurrentStep,
        RequiredSteps = DeserializeSteps(record.RequiredStepsJson),
        CompletedSteps = DeserializeSteps(record.CompletedStepsJson),
        IsComplete = record.IsComplete,
        RiskScore = record.RiskScore,
        InitiatedAt = record.InitiatedAt,
        ExpiresAt = record.ExpiresAt,
        IpAddress = record.IpAddress,
        DeviceFingerprint = record.DeviceFingerprint,
        StepData = DeserializeStepData(record.StepDataJson)
    };

    private static string SerializeSteps(IEnumerable<AuthenticationStep> steps) =>
        JsonSerializer.Serialize(steps.Distinct().OrderBy(step => (int)step).ToList(), SerializationOptions);

    private static List<AuthenticationStep> DeserializeSteps(string json) =>
        string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<AuthenticationStep>>(json, SerializationOptions) ?? [];

    private static Dictionary<string, object>? DeserializeStepData(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object>>(json, SerializationOptions);
}
