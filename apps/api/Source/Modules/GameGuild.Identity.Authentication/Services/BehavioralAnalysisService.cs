using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Analyzes behavioral patterns to produce risk assessments based on historical user activity.
/// </summary>
public class BehavioralAnalysisService(
    IAuthenticationAttemptRepository authAttemptRepository,
    ILogger<BehavioralAnalysisService> logger,
    IConfiguration configuration) : IBehavioralAnalysisService
{
    private const int DefaultBehavioralAnalysisWindowDays = 30;

    public async Task<BehavioralAnalysisResult> AnalyzeBehavioralPatternsAsync(
        Guid userId,
        AuthenticationAttemptContext attemptContext)
    {
        var result = new BehavioralAnalysisResult
        {
            RiskLevel = RiskLevel.Low,
            RiskScore = 0,
            MatchesTypicalBehavior = true,
            DetectedAnomalies = new List<string>(),
            Confidence = 0.5
        };

        try
        {
            var analysisWindow = configuration.GetValue(
                "Authentication:Anomaly:BehavioralAnalysisWindowDays", DefaultBehavioralAnalysisWindowDays);
            var since = SystemClock.UtcNow.AddDays(-analysisWindow);
            var historicalAttempts = await authAttemptRepository
                .GetRecentAttemptsAsync(userId, since, limit: 1000, cancellationToken: default)
                .ConfigureAwait(false);

            if (historicalAttempts.Count < 5)
            {
                result.Confidence = 0.2;
                result.DetectedAnomalies.Add("Insufficient historical data for behavioral analysis");
                return result;
            }

            var successfulAttempts = historicalAttempts.Where(a => a.IsSuccessful).ToList();

            // Analyze typical IP addresses
            var commonIps = successfulAttempts
                .GroupBy(a => a.IpAddress)
                .Select(g => new { IpAddress = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .Select(x => x.IpAddress)
                .ToList();

            if (commonIps.Any() && !commonIps.Contains(attemptContext.IpAddress))
            {
                result.RiskScore += 20;
                result.DetectedAnomalies.Add("Authentication from unfamiliar IP address");
                result.MatchesTypicalBehavior = false;
            }

            // Analyze typical user agents
            var commonUserAgents = successfulAttempts
                .Where(a => !string.IsNullOrEmpty(a.UserAgent))
                .GroupBy(a => a.UserAgent)
                .Select(g => new { UserAgent = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(3)
                .Select(x => x.UserAgent)
                .ToList();

            if (commonUserAgents.Any() && !commonUserAgents.Contains(attemptContext.UserAgent))
            {
                result.RiskScore += 15;
                result.DetectedAnomalies.Add("Authentication from unfamiliar device/browser");
                result.MatchesTypicalBehavior = false;
            }

            // Analyze typical authentication times
            var hourlyPattern = successfulAttempts
                .GroupBy(a => a.AttemptedAt.Hour)
                .Select(g => new { Hour = g.Key, Count = g.Count() })
                .ToList();

            var currentHour = SystemClock.UtcNow.Hour;
            var typicalHours = hourlyPattern
                .Where(h => h.Count >= successfulAttempts.Count * 0.1)
                .Select(h => h.Hour)
                .ToList();

            if (typicalHours.Any() && !typicalHours.Contains(currentHour))
            {
                result.RiskScore += 10;
                result.DetectedAnomalies.Add("Authentication at unusual time of day");
            }

            // Analyze geolocation patterns if available
            if (BehavioralLocationIdentity.Capture(attemptContext.Location) is { } currentLocation)
            {
                var commonLocations = successfulAttempts
                    .Select(currentLocation.HistoricalKey)
                    .Where(key => key.HasValue)
                    .Select(key => key.GetValueOrDefault())
                    .GroupBy(key => key)
                    .Select(group => new { Location = group.Key, Count = group.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(3)
                    .Select(x => x.Location)
                    .ToList();

                if (commonLocations.Any() && !commonLocations.Contains(currentLocation.CurrentKey))
                {
                    result.RiskScore += 25;
                    result.DetectedAnomalies.Add("Authentication from unfamiliar location");
                    result.MatchesTypicalBehavior = false;
                }
            }

            // Calculate confidence based on data volume
            result.Confidence = Math.Min(1.0, successfulAttempts.Count / 50.0);

            // Determine final risk level
            result.RiskLevel = result.RiskScore switch
            {
                >= 70 => RiskLevel.Critical,
                >= 50 => RiskLevel.High,
                >= 25 => RiskLevel.Medium,
                _ => RiskLevel.Low
            };

            if (result.RiskScore >= 25)
            {
                logger.LogInformation(
                    "Behavioral anomalies detected for user {UserId}: RiskScore={RiskScore}, Anomalies={AnomalyCount}",
                    userId, result.RiskScore, result.DetectedAnomalies.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing behavioral patterns for user {UserId}", userId);
            throw;
        }

        return result;
    }
}
