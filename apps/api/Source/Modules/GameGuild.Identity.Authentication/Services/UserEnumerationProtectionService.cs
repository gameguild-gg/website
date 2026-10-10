using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Service to protect against user enumeration attacks by ensuring consistent timing and responses
/// </summary>
public class UserEnumerationProtectionService(
    ILogger<UserEnumerationProtectionService> logger,
    IMemoryCache memoryCache,
    IPasswordHasher passwordHasher) : IUserEnumerationProtectionService
{
    // Consistent error message to prevent user enumeration
    private const string ConsistentErrorMessage = "Invalid credentials. Please check your email and password.";

    /// <summary>
    ///     Enumeration-safe response for anonymous email-verification requests. Known and unknown
    ///     accounts must receive this identical message, including when delivery boundaries fail.
    /// </summary>
    public const string GenericEmailVerificationMessage = "If an account exists with that email, a verification email has been sent";

    // Every compensated window is topped up to the same target, measured from the
    // server-owned monotonic origin captured before account resolution.
    private static readonly TimeSpan TargetProcessingTime = TimeSpan.FromMilliseconds(400);

    // Interface implementation methods

    /// <summary>
    ///     Captures the server-owned monotonic origin for an authentication attempt. Callers must
    ///     invoke this before any account resolution so lookups and credential verification fall
    ///     inside the compensated window.
    /// </summary>
    public AuthenticationTimingScope BeginAuthenticationTiming()
    {
        return new AuthenticationTimingScope();
    }

    /// <summary>
    ///     Compensates an authentication window so all outcomes take structurally identical time.
    ///     Windows that completed no usable credential verification (missing account, passwordless
    ///     account, unusable credential) first receive equivalent dummy verification at the
    ///     configured BCrypt work factor; every window is then topped up to the target processing
    ///     time measured from the monotonic origin. There is no existence-dependent jitter.
    /// </summary>
    public async Task AddTimingProtectionDelayAsync(AuthenticationTimingScope timingScope, CredentialWorkClassification credentialWork)
    {
        ArgumentNullException.ThrowIfNull(timingScope);

        try
        {
            // Only windows without completed credential work need the dummy verification; the
            // real verification path already spent that work inside the measured window.
            if (credentialWork == CredentialWorkClassification.None)
            {
                await passwordHasher.PerformDummyVerificationAsync().ConfigureAwait(false);
            }

            // Total elapsed covers account resolution, credential work and the dummy work above,
            // because the scope origin precedes all of them.
            var remainingDelay = TargetProcessingTime - timingScope.ElapsedSinceOrigin;

            if (remainingDelay > TimeSpan.Zero) { await Task.Delay(remainingDelay).ConfigureAwait(false); }

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Authentication timing compensation: CredentialWork={CredentialWork}, WindowElapsed={WindowElapsedMs}ms, TargetTime={TargetTimeMs}ms",
                    credentialWork,
                    timingScope.ElapsedSinceOrigin.TotalMilliseconds,
                    TargetProcessingTime.TotalMilliseconds
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in timing protection delay");
            throw;
        }
    }

    public string GetGenericErrorMessage(string context)
    {
        // Return consistent error message regardless of context to prevent enumeration
        return context switch
        {
            "login" => "Invalid credentials. Please check your email and password.",
            "password_reset" => "If an account exists with that email, a password reset link has been sent.",
            "email_verification" => GenericEmailVerificationMessage,
            "registration" => "Unable to complete registration. Please try again.",
            "mfa" => "Invalid authentication code. Please try again.",
            _ => "Authentication failed. Please try again."
        };
    }

    private const int MaxAttemptsPerWindow = 10;
    private const int TimeWindowMinutes = 15;
    private const string AttemptKeyPrefix = "enum:attempts:";

    public async Task<ThrottleDecision> ShouldThrottleAsync(string identifier)
    {
        var cacheKey = AttemptKeyPrefix + identifier;

        var attemptCount = memoryCache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(TimeWindowMinutes);
            return 0;
        });

        var shouldThrottle = attemptCount >= MaxAttemptsPerWindow;
        var delayMs = shouldThrottle ? Math.Min(attemptCount * 500, 5000) : 0;

        if (shouldThrottle)
        {
            logger.LogWarning("Throttling enumeration attempts for identifier {Identifier}: {AttemptCount} attempts in {TimeWindow} min window",
                identifier, attemptCount, TimeWindowMinutes);
        }

        await Task.CompletedTask.ConfigureAwait(false);

        return new ThrottleDecision { ShouldThrottle = shouldThrottle, DelayMs = delayMs, AttemptCount = attemptCount, TimeWindowMinutes = TimeWindowMinutes };
    }

    public async Task RecordEnumerationAttemptAsync(string identifier, string attemptType)
    {
        var cacheKey = AttemptKeyPrefix + identifier;

        var currentCount = memoryCache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(TimeWindowMinutes);
            return 0;
        });

        memoryCache.Set(cacheKey, currentCount + 1, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(TimeWindowMinutes)
        });

        logger.LogWarning("Potential enumeration attempt detected — Identifier: {Identifier}, Type: {AttemptType}, Count: {Count}",
            identifier, attemptType, currentCount + 1);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public string GetConsistentErrorMessage() { return ConsistentErrorMessage; }

    public TimeSpan GetBaseProcessingTime() { return TargetProcessingTime; }
}
