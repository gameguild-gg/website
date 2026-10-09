using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Detects brute-force attacks, impossible-travel scenarios, and applies throttling logic.
/// </summary>
public class ThreatDetectionService(
    IAuthenticationAttemptRepository authAttemptRepository,
    ILogger<ThreatDetectionService> logger,
    IConfiguration configuration,
    ISiemIntegrationService siemService) : IThreatDetectionService
{
    private const int DefaultMaxFailedAttemptsPerHour = 5;
    private const int DefaultThrottleMinutes = 15;
    private const int DefaultMaxAttemptsPerIpPerHour = 50;

    public async Task<bool> DetectBruteForceAsync(string identifier, int timeWindowMinutes = 15)
    {
        var sinceTime = SystemClock.UtcNow.AddMinutes(-timeWindowMinutes);
        var failedAttempts = await authAttemptRepository
            .GetFailedAttemptsAsync(identifier, sinceTime, CancellationToken.None)
            .ConfigureAwait(false);

        var failedCount = failedAttempts.Count();
        const int bruteForceThreshold = 5;

        if (failedCount >= bruteForceThreshold)
        {
            logger.LogWarning(
                "Brute force attack detected - Identifier: {Identifier}, Failed attempts: {FailedCount} in {TimeWindowMinutes} minutes",
                LogRedaction.MaskIdentifier(identifier), failedCount, timeWindowMinutes);

            await siemService
                .SendBruteForceEventAsync(identifier, failedCount, TimeSpan.FromMinutes(timeWindowMinutes), CancellationToken.None)
                .ConfigureAwait(false);

            return true;
        }

        return false;
    }

    public async Task<bool> DetectImpossibleTravelAsync(
        Guid userId,
        LocationInfo currentLocation,
        LocationInfo previousLocation,
        TimeSpan timeBetween)
    {
        if (string.IsNullOrEmpty(currentLocation.Country) || string.IsNullOrEmpty(previousLocation.Country))
        {
            return false;
        }

        if (currentLocation.Country == previousLocation.Country && currentLocation.City == previousLocation.City)
        {
            return false;
        }

        var isSuspicious = currentLocation.Country != previousLocation.Country && timeBetween.TotalHours < 1;

        if (isSuspicious)
        {
            logger.LogWarning(
                "Impossible travel detected - UserId: {UserId}, From: {PreviousCountry} to {CurrentCountry} in {Hours:F2} hours",
                userId,
                previousLocation.Country,
                currentLocation.Country,
                timeBetween.TotalHours);

            await siemService
                .SendImpossibleTravelEventAsync(userId, previousLocation, currentLocation, timeBetween, CancellationToken.None)
                .ConfigureAwait(false);
        }

        return isSuspicious;
    }

    public async Task<ThrottleDecision> ShouldThrottleAsync(
        string ipAddress,
        string email,
        CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;
        var oneHourAgo = now.AddHours(-1);

        var recentAttempts = await authAttemptRepository
            .GetFailedAttemptsAsync(email.ToLowerInvariant(), oneHourAgo, cancellationToken)
            .ConfigureAwait(false);

        var ipAttempts = recentAttempts.Count(a => a.IpAddress == ipAddress);
        var emailAttempts = recentAttempts.Count;

        var maxIpAttempts = configuration.GetValue(
            "Authentication:Anomaly:MaxAttemptsPerIpPerHour", DefaultMaxAttemptsPerIpPerHour);
        var maxFailedPerHour = configuration.GetValue(
            "Authentication:Anomaly:MaxFailedAttemptsPerHour", DefaultMaxFailedAttemptsPerHour);

        if (ipAttempts >= maxIpAttempts)
        {
            return new ThrottleDecision
            {
                ShouldThrottle = true,
                Reason = "IP address exceeded maximum attempts per hour",
                ThrottleUntil = now.AddMinutes(
                    configuration.GetValue("Authentication:Anomaly:ThrottleMinutes", DefaultThrottleMinutes)),
                RemainingAttempts = 0
            };
        }

        if (emailAttempts >= maxFailedPerHour)
        {
            return new ThrottleDecision
            {
                ShouldThrottle = true,
                Reason = "Email exceeded maximum failed attempts per hour",
                ThrottleUntil = now.AddMinutes(
                    configuration.GetValue("Authentication:Anomaly:ThrottleMinutes", DefaultThrottleMinutes)),
                RemainingAttempts = 0
            };
        }

        return new ThrottleDecision
        {
            ShouldThrottle = false,
            RemainingAttempts = Math.Max(0, maxFailedPerHour - emailAttempts)
        };
    }

    public string GenerateDeviceFingerprint(
        string? userAgent,
        string? acceptLanguage = null,
        string? acceptEncoding = null)
    {
        var fingerprint = $"{userAgent}|{acceptLanguage}|{acceptEncoding}";
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(fingerprint));

        return Convert.ToHexString(hash);
    }
}
