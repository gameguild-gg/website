using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Records login attempts, runs inline risk analysis, persists results, and
///     composes higher-level anomaly results from attempt context.
/// </summary>
public class LoginAttemptAnalysisService(
    IAuthenticationAttemptRepository authAttemptRepository,
    IThreatDetectionService threatDetectionService,
    ILogger<LoginAttemptAnalysisService> logger,
    IConfiguration configuration,
    ISiemIntegrationService siemService,
    IThreatIntelligenceProvider? threatIntelligenceProvider = null,
    IAuthenticationAuditEventSink? auditEventSink = null,
    IAdaptiveAnomalyDetectionService? adaptiveAnomalyDetectionService = null) : ILoginAttemptAnalysisService
{
    private const int DefaultSuspiciousThreshold = 3;
    private const string MaliciousIpAnomaly = "ThreatIntel:MaliciousIp";
    private const string BreachedPasswordAnomaly = "ThreatIntel:BreachedPassword";
    private const string ThreatIntelMatchActionType = "Authentication.ThreatIntelligenceMatch";

    public async Task RecordSuspiciousActivityAsync(SuspiciousActivity activity)
    {
        logger.LogWarning(
            "Suspicious activity recorded - Type: {ActivityType}, UserId: {UserId}, Identifier: {Identifier}, RiskLevel: {RiskLevel}",
            activity.ActivityType,
            activity.UserId,
            activity.Identifier,
            activity.RiskLevel);

        await siemService
            .SendSuspiciousActivityEventAsync(activity, CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<AuthenticationAttemptAnalysis> RecordLoginAttemptAsync(
        CreateAuthenticationAttemptRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var loginAttempt = new AuthenticationAttempt
            {
                Id = Guid.NewGuid(),
                Email = request.Email.ToLowerInvariant(),
                UserId = request.UserId,
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent,
                IsSuccessful = request.IsSuccessful,
                FailureReason = request.FailureReason,
                AttemptedAt = SystemClock.UtcNow,
                ProcessingTime = request.ProcessingTime,
                Location = request.Location,
                DeviceFingerprint = request.DeviceFingerprint,
                SessionId = request.SessionId,
                TenantId = request.TenantId,
                Metadata = request.Metadata,
                CorrelationId = request.CorrelationId
            };

            var analysis = await AnalyzeLoginAttemptInternalAsync(loginAttempt, cancellationToken)
                .ConfigureAwait(false);
            loginAttempt.IsSuspicious = analysis.IsSuspicious;
            loginAttempt.RiskScore = analysis.RiskScore;

            await authAttemptRepository.CreateAsync(loginAttempt, cancellationToken).ConfigureAwait(false);

            if (analysis.IsSuspicious)
            {
                await LogSuspiciousActivityAsync(loginAttempt, analysis).ConfigureAwait(false);
            }

            logger.LogInformation(
                "Login attempt recorded: Email={Email}, IP={IpAddress}, Success={IsSuccessful}, Risk={RiskScore}, Suspicious={IsSuspicious}",
                request.Email,
                request.IpAddress,
                request.IsSuccessful,
                analysis.RiskScore,
                analysis.IsSuspicious);

            return analysis;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error recording login attempt for {Email} from {IpAddress}",
                request.Email, request.IpAddress);
            throw;
        }
        finally
        {
            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                logger.LogWarning("Slow login attempt recording: {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
        }
    }

    public async Task<AuthenticationAnomalyResult> AnalyzeLoginAttemptAsync(AuthenticationAttemptContext context)
    {
        var result = new AuthenticationAnomalyResult
        {
            IsAnomalous = false,
            RiskLevel = RiskLevel.Low,
            RiskScore = 0,
            DetectedAnomalies = new List<string>()
        };

        try
        {
            if (context.UserId.HasValue)
            {
                var userId = context.UserId.Value;
                var since = SystemClock.UtcNow.AddHours(-24);
                var recentAttempts = await authAttemptRepository
                    .GetRecentAttemptsAsync(userId, since, cancellationToken: default)
                    .ConfigureAwait(false);

                if (!recentAttempts.Any())
                {
                    result.RiskScore += 10;
                    result.DetectedAnomalies.Add("FirstAttemptOrLongAbsence");
                }

                var lastSuccessful = await authAttemptRepository
                    .GetLastSuccessfulAttemptAsync(userId, cancellationToken: default)
                    .ConfigureAwait(false);

                if (lastSuccessful != null && lastSuccessful.IpAddress != context.IpAddress)
                {
                    result.RiskScore += 20;
                    result.DetectedAnomalies.Add("IpAddressChange");
                }

                if (lastSuccessful != null && !string.IsNullOrEmpty(lastSuccessful.UserAgent) &&
                    lastSuccessful.UserAgent != context.UserAgent)
                {
                    result.RiskScore += 15;
                    result.DetectedAnomalies.Add("UserAgentChange");
                }

                if (context.Location != null && lastSuccessful != null)
                {
                    var timeBetween = context.AttemptedAt - lastSuccessful.AttemptedAt;
                    var previousLocation = ParseLocation(lastSuccessful.Location);

                    if (previousLocation != null)
                    {
                        var isImpossibleTravel = await threatDetectionService
                            .DetectImpossibleTravelAsync(userId, context.Location, previousLocation, timeBetween)
                            .ConfigureAwait(false);

                        if (isImpossibleTravel)
                        {
                            result.RiskScore += 50;
                            result.DetectedAnomalies.Add(SecurityAlertKinds.ImpossibleTravel);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(context.DeviceFingerprint) && lastSuccessful != null &&
                    !string.IsNullOrEmpty(lastSuccessful.DeviceFingerprint) &&
                    lastSuccessful.DeviceFingerprint != context.DeviceFingerprint)
                {
                    result.RiskScore += 15;
                    result.DetectedAnomalies.Add("DeviceFingerprintChange");
                }
            }

            if (!string.IsNullOrEmpty(context.Identifier))
            {
                var isBruteForce = await threatDetectionService
                    .DetectBruteForceAsync(context.Identifier)
                    .ConfigureAwait(false);

                if (isBruteForce)
                {
                    result.RiskScore += 40;
                    result.DetectedAnomalies.Add(SecurityAlertKinds.BruteForceDetected);
                }
            }

            if (context.IsWeekend || context.TimeOfDay.Hours < 6 || context.TimeOfDay.Hours > 22)
            {
                result.RiskScore += 5;
                result.DetectedAnomalies.Add("UnusualTimeOfDay");
            }

            await ApplyThreatIntelligenceSignalsAsync(context, result).ConfigureAwait(false);

            await ApplyAdaptiveAnomalySignalsAsync(context, result).ConfigureAwait(false);

            result.RiskLevel = result.RiskScore switch
            {
                >= 80 => RiskLevel.Critical,
                >= 60 => RiskLevel.High,
                >= 30 => RiskLevel.Medium,
                _ => RiskLevel.Low
            };

            result.IsAnomalous = result.RiskScore >= 30;

            if (result.IsAnomalous)
            {
                logger.LogWarning(
                    "Anomalous login attempt detected - UserId: {UserId}, IP: {IpAddress}, RiskScore: {RiskScore}, Anomalies: {Anomalies}",
                    context.UserId, context.IpAddress, result.RiskScore,
                    string.Join(", ", result.DetectedAnomalies));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing login attempt context");
            throw;
        }

        return result;
    }

    // ── Private helpers ──────────────────────────────────────────────────

    /// <summary>
    ///     Applies credential-stuffing threat-intelligence signals (malicious IP blocklist,
    ///     breached-password corpus) to the risk analysis. Fails open on every provider
    ///     error so a defense-in-depth signal can never break sign-in. In observation mode
    ///     (the default) matches are logged and audited without raising the risk score.
    /// </summary>
    private async Task ApplyThreatIntelligenceSignalsAsync(
        AuthenticationAttemptContext context,
        AuthenticationAnomalyResult result)
    {
        if (threatIntelligenceProvider is null)
        {
            return;
        }

        try
        {
            var enforcementMode = configuration.GetValue(
                "ThreatIntelligence:EnforcementMode",
                ThreatIntelligenceEnforcementMode.Observation);
            var enforce = enforcementMode == ThreatIntelligenceEnforcementMode.Enforce;

            var ipResult = await threatIntelligenceProvider
                .CheckIpAddressAsync(context.IpAddress)
                .ConfigureAwait(false);

            if (ipResult.IsMatch)
            {
                await ApplyThreatIntelligenceMatchAsync(
                    result,
                    MaliciousIpAnomaly,
                    ipResult.MatchedCidr,
                    enforce,
                    configuration.GetValue("ThreatIntelligence:MaliciousIpRiskScore", 60),
                    context).ConfigureAwait(false);
            }

            var passwordResult = await threatIntelligenceProvider
                .CheckPasswordHashAsync(context.PasswordSha256Hex)
                .ConfigureAwait(false);

            if (passwordResult.IsMatch)
            {
                // The matched digest itself is never included in labels, logs, or audit metadata.
                await ApplyThreatIntelligenceMatchAsync(
                    result,
                    BreachedPasswordAnomaly,
                    matchedDetail: null,
                    enforce,
                    configuration.GetValue("ThreatIntelligence:BreachedPasswordRiskScore", 60),
                    context).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            // Fail open: threat intelligence is defense-in-depth and must not affect the
            // outcome (or the availability) of the primary risk analysis.
            logger.LogWarning(exception, "Threat intelligence evaluation skipped after a provider error");
        }
    }

    /// <summary>
    ///     Applies the adaptive (online statistical learning) anomaly assessment to the risk
    ///     analysis: learned behavioral deviations raise the risk score and are recorded as
    ///     detected anomaly labels. The adaptive scorer fails open and abstains during cold
    ///     start, so the fixed-weight heuristics above remain the baseline signal at all times.
    /// </summary>
    private async Task ApplyAdaptiveAnomalySignalsAsync(
        AuthenticationAttemptContext context,
        AuthenticationAnomalyResult result)
    {
        if (adaptiveAnomalyDetectionService is null)
        {
            return;
        }

        var assessment = await adaptiveAnomalyDetectionService
            .AssessAsync(context)
            .ConfigureAwait(false);

        if (!assessment.IsLearnedDeviation)
        {
            return;
        }

        result.RiskScore += assessment.LearnedRiskScoreContribution;
        result.DetectedAnomalies.AddRange(assessment.DeviationLabels);

        logger.LogWarning(
            "Learned behavioral deviation detected - UserId: {UserId}, ZScore: {ZScore}, BaselineSamples: {BaselineSamples}, Features: {Features}",
            context.UserId,
            assessment.CombinedZScore,
            assessment.BaselineSampleCount,
            string.Join(", ", assessment.DeviationLabels));
    }

    private async Task ApplyThreatIntelligenceMatchAsync(
        AuthenticationAnomalyResult result,
        string anomalyLabel,
        string? matchedDetail,
        bool enforce,
        int riskScoreBump,
        AuthenticationAttemptContext context)
    {
        result.DetectedAnomalies.Add(anomalyLabel);

        if (enforce)
        {
            result.RiskScore += riskScoreBump;
        }

        logger.LogWarning(
            "Threat intelligence match ({Mode}) - Signal: {Signal}, IP: {IpAddress}, UserId: {UserId}, RiskScoreBump: {RiskScoreBump}",
            enforce ? "Enforce" : "Observation",
            anomalyLabel,
            context.IpAddress,
            context.UserId,
            enforce ? riskScoreBump : 0);

        if (auditEventSink is null)
        {
            return;
        }

        try
        {
            // Forwarded to the central security event pipeline (SecurityEventLogger) by the
            // audit module's IAuthenticationAuditEventSink implementation.
            await auditEventSink.RecordAsync(
                new AuthenticationAuditEvent(
                    ThreatIntelMatchActionType,
                    context.UserId,
                    Success: false,
                    Method: "Password",
                    IpAddress: context.IpAddress,
                    UserAgent: string.IsNullOrEmpty(context.UserAgent) ? null : context.UserAgent,
                    TenantId: context.TenantId,
                    ErrorMessage: anomalyLabel,
                    AssessedRiskLevel: enforce ? RiskLevel.High : RiskLevel.Medium,
                    Metadata: new
                    {
                        Signal = anomalyLabel,
                        EnforcementMode = enforce ? "Enforce" : "Observation",
                        MatchedDetail = matchedDetail,
                        Provider = threatIntelligenceProvider?.ProviderName
                    }),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not record the threat intelligence match security event");
        }
    }

    private async Task<AuthenticationAttemptAnalysis> AnalyzeLoginAttemptInternalAsync(
        AuthenticationAttempt attempt,
        CancellationToken cancellationToken = default)
    {
        var analysis = new AuthenticationAttemptAnalysis
        {
            IsSuspicious = false,
            RiskScore = 0,
            RiskFactors = new List<string>()
        };

        var oneHourAgo = SystemClock.UtcNow.AddHours(-1);

        var recentIpAttempts = await authAttemptRepository
            .GetFailedAttemptsAsync(attempt.Email, oneHourAgo, cancellationToken)
            .ConfigureAwait(false);

        var ipAttemptCount = recentIpAttempts.Count(a => a.IpAddress == attempt.IpAddress);

        if (ipAttemptCount >= 3)
        {
            analysis.RiskScore += 20;
            analysis.RiskFactors.Add($"Multiple failed attempts from IP: {ipAttemptCount}");
        }

        if (attempt.ProcessingTime < TimeSpan.FromMilliseconds(50))
        {
            analysis.RiskScore += 15;
            analysis.RiskFactors.Add("Abnormally fast authentication attempt");
        }

        if (string.IsNullOrEmpty(attempt.UserAgent) || attempt.UserAgent.Length < 10)
        {
            analysis.RiskScore += 10;
            analysis.RiskFactors.Add("Missing or suspicious user agent");
        }

        var suspiciousThreshold = configuration.GetValue(
            "Authentication:Anomaly:SuspiciousThreshold", DefaultSuspiciousThreshold);

        if (analysis.RiskScore >= suspiciousThreshold * 10)
        {
            analysis.IsSuspicious = true;
        }

        return analysis;
    }

    private async Task LogSuspiciousActivityAsync(
        AuthenticationAttempt attempt,
        AuthenticationAttemptAnalysis analysis)
    {
        logger.LogWarning(
            "Suspicious login attempt detected - Email: {Email}, IP: {IpAddress}, RiskScore: {RiskScore}",
            attempt.Email, attempt.IpAddress, analysis.RiskScore);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static LocationInfo? ParseLocation(string? locationString)
    {
        if (string.IsNullOrEmpty(locationString))
        {
            return null;
        }

        var parts = locationString.Split('-');
        if (parts.Length >= 2)
        {
            return new LocationInfo { Country = parts[0], City = parts[1] };
        }

        return new LocationInfo { Country = locationString };
    }
}
