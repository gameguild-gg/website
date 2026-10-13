using System.Globalization;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Service to protect against user enumeration attacks by ensuring consistent timing and responses
/// </summary>
public class UserEnumerationProtectionService(ILogger<UserEnumerationProtectionService> logger, IMemoryCache memoryCache,
    IConfiguration? configuration = null, IPasswordHasher? passwordHasher = null) : IUserEnumerationProtectionService, IAuthenticationTimingProtection
{
    public UserEnumerationProtectionService(ILogger<UserEnumerationProtectionService> logger, IMemoryCache memoryCache,
        IPasswordHasher passwordHasher) : this(logger, memoryCache, null, passwordHasher) { }

    // Consistent error message to prevent user enumeration
    private const string ConsistentErrorMessage = "Invalid credentials. Please check your email and password.";

    /// <summary>
    ///     Enumeration-safe response for anonymous email-verification requests. Known and unknown
    ///     accounts must receive this identical message, including when delivery boundaries fail.
    /// </summary>
    public const string GenericEmailVerificationMessage = "If an account exists with that email, a verification email has been sent";

    private static readonly TimeSpan TargetProcessingTime = TimeSpan.FromMilliseconds(400);

    // Interface implementation methods

    public Task AddTimingProtectionDelayAsync(bool isValidUser, DateTime startTime) =>
        CompleteAuthenticationTimingAsync(AuthenticationTimingOrigin.FromLegacyWallClock(startTime), isValidUser);

    public AuthenticationTimingScope BeginAuthenticationTiming() => new();

    public Task AddTimingProtectionDelayAsync(AuthenticationTimingScope timingScope, CredentialWorkClassification credentialWork)
    {
        ArgumentNullException.ThrowIfNull(timingScope);
        return CompleteAuthenticationTimingAsync(timingScope.Origin, credentialWork == CredentialWorkClassification.Completed);
    }

    public Task CompleteAuthenticationTimingAsync(AuthenticationTimingOrigin origin, bool credentialWorkCompleted) =>
        CompleteAuthenticationTimingAsync(origin, credentialWorkCompleted, CancellationToken.None);

    public async Task CompleteAuthenticationTimingAsync(AuthenticationTimingOrigin origin, bool credentialWorkCompleted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(origin);
        cancellationToken.ThrowIfCancellationRequested();

        if (!credentialWorkCompleted)
        {
            if (passwordHasher is null)
            {
                await PerformDummyPasswordHashAsync("dummy_password_for_timing", cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await passwordHasher.PerformDummyVerificationAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = TargetProcessingTime - origin.Elapsed;
            if (remaining <= TimeSpan.Zero) { break; }
            // Timer APIs use whole milliseconds. Round upward and recheck the same
            // monotonic origin after waking; a fractional or early timer cannot miss the floor.
            var delay = TimeSpan.FromMilliseconds(Math.Ceiling(remaining.TotalMilliseconds));
            await Task.Delay(delay, origin.Clock, cancellationToken).ConfigureAwait(false);
        }
        cancellationToken.ThrowIfCancellationRequested();
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

    public Task SimulateAuthenticationDelayAsync(string email, bool userExists) =>
        SimulateAuthenticationDelayAsync(email, userExists, CancellationToken.None);

    public async Task SimulateAuthenticationDelayAsync(string email, bool userExists, CancellationToken cancellationToken)
    {
        var origin = AuthenticationTimingOrigin.Start();

        try
        {
            // This entry point receives no completed verification. Simulate the same credential
            // work for either account class and account for it in the shared monotonic floor.
            await CompleteAuthenticationTimingAsync(origin, false, cancellationToken).ConfigureAwait(false);

            // Log timing analysis for security monitoring
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Authentication timing: EmailHash={EmailHash}, UserExists={UserExists}, ProcessingTime={ProcessingTimeMs}ms, TargetTime={TargetTimeMs}ms",
                    LogRedaction.RedactSecret(HashEmail(email)),
                    userExists,
                    origin.Elapsed.TotalMilliseconds,
                    TargetProcessingTime.TotalMilliseconds
                );
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in authentication delay simulation");
            throw;
        }
    }

    public string GetConsistentErrorMessage() { return ConsistentErrorMessage; }

    public Task PerformDummyPasswordHashAsync(string password) => PerformDummyPasswordHashAsync(password, CancellationToken.None);

    public async Task PerformDummyPasswordHashAsync(string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var workFactor = PasswordHasher.ResolveBCryptWorkFactor(configuration);
        var dummySalt = "$2a$" + workFactor.ToString("00", CultureInfo.InvariantCulture) + "$abcdefghijklmnopqrstuu";
        // Check cancellation before and after the synchronous BCrypt operation.
        // A failed hash must propagate as a failure of timing protection.
        await Task.Run(() => BCrypt.Net.BCrypt.HashPassword(password, dummySalt), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogDebug("Dummy credential work completed (BCrypt work factor: {WorkFactor})", workFactor);
    }

    public TimeSpan GetBaseProcessingTime() { return TargetProcessingTime; }

    /// <summary>
    ///     Creates a hash of the email for logging without exposing the actual email
    /// </summary>
    private string HashEmail(string email)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(email.ToLowerInvariant()));

        return Convert.ToHexString(hash)[..16]; // First 16 chars for brevity
    }
}
