namespace GameGuild.Identity.Authentication;

/// <summary>
/// Service interface for login attempt recording and IP extraction
/// </summary>
public interface IAuthAttemptService
{
    /// <summary>Records a successful authentication attempt</summary>
    Task RecordSuccessfulAttemptAsync(string email, Guid userId, string ipAddress, string? userAgent, TimeSpan processingTime);

    /// <summary>Records a successful authentication attempt using the named authentication provider.</summary>
    Task RecordSuccessfulAttemptAsync(string email, Guid userId, string ipAddress, string? userAgent, TimeSpan processingTime, string authenticationMethod);

    /// <summary>Records a failed authentication attempt</summary>
    Task RecordFailedAttemptAsync(string email, Guid? userId, string ipAddress, string? userAgent, string failureReason, TimeSpan processingTime);

    /// <summary>Records a failed authentication attempt using the named authentication provider.</summary>
    Task RecordFailedAttemptAsync(string email, Guid? userId, string ipAddress, string? userAgent, string failureReason, TimeSpan processingTime, string authenticationMethod);

    /// <summary>Extracts the client IP address from the HTTP context</summary>
    string GetClientIpAddress(Microsoft.AspNetCore.Http.HttpContext? httpContext);
}
