using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
/// Login attempt recording and IP address extraction. When a geolocation provider is
/// registered (issue #242), forensics records are enriched with the coarse country/city of
/// the attempt IP; enrichment failures degrade to Unknown rather than failing the pipeline.
/// </summary>
public class AuthAttemptService(
    IAuthenticationAttemptRepository authenticationAttemptRepository,
    IUserEnumerationProtectionService enumerationProtection,
    ILogger<AuthAttemptService> logger,
    IGeolocationProvider? geolocationProvider = null,
    IAuthenticationAuditEventSink? auditEventSink = null
) : IAuthAttemptService
{
    private const int MaxLoggedFailureReasonLength = 256;

    public Task RecordSuccessfulAttemptAsync(string email, Guid userId, string ipAddress, string? userAgent, TimeSpan processingTime)
    {
        return RecordSuccessfulAttemptAsync(email, userId, ipAddress, userAgent, processingTime, "Password");
    }

    public async Task RecordSuccessfulAttemptAsync(string email, Guid userId, string ipAddress, string? userAgent, TimeSpan processingTime, string authenticationMethod)
    {
        ipAddress = NormalizeIpAddress(ipAddress);

        var attempt = new AuthenticationAttempt
        {
            Email = email,
            UserId = userId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            IsSuccessful = true,
            AttemptedAt = SystemClock.UtcNow,
            ProcessingTime = processingTime
        };
        await EnrichWithGeolocationAsync(attempt).ConfigureAwait(false);

        try
        {
            await authenticationAttemptRepository.CreateAsync(attempt).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Keep trying the central audit sink even when the local attempt store is unavailable.
            logger.LogError(ex, "Error persisting successful authentication attempt");
        }

        LogLoginAuditEvent(attempt);
        await ForwardAuditEventAsync(attempt, authenticationMethod).ConfigureAwait(false);
    }

    public Task RecordFailedAttemptAsync(string email, Guid? userId, string ipAddress, string? userAgent, string failureReason, TimeSpan processingTime)
    {
        return RecordFailedAttemptAsync(email, userId, ipAddress, userAgent, failureReason, processingTime, "Password");
    }

    public async Task RecordFailedAttemptAsync(string email, Guid? userId, string ipAddress, string? userAgent, string failureReason, TimeSpan processingTime, string authenticationMethod)
    {
        ipAddress = NormalizeIpAddress(ipAddress);

        var attempt = new AuthenticationAttempt
        {
            Email = email,
            UserId = userId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            IsSuccessful = false,
            FailureReason = failureReason,
            AttemptedAt = SystemClock.UtcNow,
            ProcessingTime = processingTime
        };
        await EnrichWithGeolocationAsync(attempt).ConfigureAwait(false);

        try
        {
            await authenticationAttemptRepository.CreateAsync(attempt).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Keep the central audit and abuse tracking independent from the local attempt store.
            logger.LogError(ex, "Error persisting failed authentication attempt");
        }

        LogLoginAuditEvent(attempt);
        await ForwardAuditEventAsync(attempt, authenticationMethod).ConfigureAwait(false);

        try
        {
            await enumerationProtection.RecordEnumerationAttemptAsync(ipAddress, "login").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error recording login enumeration attempt");
        }
    }

    public string GetClientIpAddress(HttpContext? httpContext)
    {
        if (httpContext == null)
        {
            return "Unknown";
        }

        // ForwardedHeadersMiddleware updates RemoteIpAddress only for configured trusted proxies.
        // Never consume forwarding headers directly: clients can forge them.
        return NormalizeIpAddress(httpContext.Connection.RemoteIpAddress?.ToString());
    }

    private static string NormalizeIpAddress(string? value)
    {
        if (!IPAddress.TryParse(value, out var address))
        {
            return "Unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.ToString();
    }

    /// <summary>
    ///     Enriches the forensics record with the coarse location of the attempt IP (issue #242).
    ///     Fail-closed: no provider, unresolvable/private addresses and provider failures all
    ///     leave the location Unknown; the attempt itself is always recorded.
    /// </summary>
    private async Task EnrichWithGeolocationAsync(AuthenticationAttempt attempt)
    {
        if (geolocationProvider is null || attempt.IpAddress is not (not null and not "Unknown"))
        {
            return;
        }

        try
        {
            var resolved = await geolocationProvider.ResolveAsync(attempt.IpAddress, CancellationToken.None).ConfigureAwait(false);
            if (resolved is null)
            {
                return;
            }

            attempt.Location = string.Join(", ", new[] { resolved.City, resolved.Country }.Where(part => !string.IsNullOrWhiteSpace(part)));
            attempt.Country = resolved.CountryCode;
            attempt.City = resolved.City;
        }
        catch (Exception ex)
        {
            // Enrichment must never fail the login-forensics pipeline.
            logger.LogWarning(ex, "Geolocation enrichment failed for IP {IpAddress}; recording without location", attempt.IpAddress);
        }
    }

    private void LogLoginAuditEvent(AuthenticationAttempt attempt)
    {
        var level = attempt.IsSuccessful ? LogLevel.Information : LogLevel.Warning;
        logger.Log(
            level,
            "Authentication audit event {AuditEventType}: UserId={UserId}, EmailHash={EmailHash}, TenantId={TenantId}, IpAddress={IpAddress}, Success={Success}, FailureReason={FailureReason}, ProcessingTimeMs={ProcessingTimeMs}, AuditEvent={AuditEvent}, AuditCategory={AuditCategory}",
            attempt.IsSuccessful ? "AuthenticationSucceeded" : "AuthenticationFailed",
            attempt.UserId,
            HashIdentifier(attempt.Email),
            attempt.TenantId,
            attempt.IpAddress,
            attempt.IsSuccessful,
            SanitizeFailureReasonForLog(attempt.FailureReason),
            attempt.ProcessingTime.TotalMilliseconds,
            true,
            "Authentication");
    }

    private static string SanitizeFailureReasonForLog(string? failureReason)
    {
        if (string.IsNullOrEmpty(failureReason))
        {
            return string.Empty;
        }

        var sanitized = new StringBuilder(Math.Min(failureReason.Length, MaxLoggedFailureReasonLength));
        var previousWasWhitespace = false;

        foreach (var character in failureReason)
        {
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace && sanitized.Length > 0)
                {
                    sanitized.Append(' ');
                }

                previousWasWhitespace = true;
            }
            else
            {
                sanitized.Append(character);
                previousWasWhitespace = false;
            }

            if (sanitized.Length >= MaxLoggedFailureReasonLength)
            {
                break;
            }
        }

        return sanitized.ToString().TrimEnd();
    }

    private async Task ForwardAuditEventAsync(AuthenticationAttempt attempt, string method)
    {
        if (auditEventSink is null)
        {
            return;
        }

        try
        {
            await auditEventSink.RecordAsync(new AuthenticationAuditEvent(
                attempt.IsSuccessful ? "Authentication.Succeeded" : "Authentication.Failed",
                attempt.UserId,
                attempt.IsSuccessful,
                method,
                attempt.IpAddress,
                attempt.UserAgent,
                attempt.SessionId,
                attempt.TenantId,
                attempt.FailureReason,
                new { attempt.ProcessingTime, attempt.IsSuspicious, attempt.RiskScore, attempt.CorrelationId, attempt.Country, attempt.City }),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Central audit availability must never block authentication.
            logger.LogError(exception, "Error forwarding authentication event {UserId} to the central audit log", attempt.UserId);
        }
    }

    private static string HashIdentifier(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes);
    }
}
