using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Service to protect against user enumeration attacks by ensuring consistent timing and responses
/// </summary>
public class UserEnumerationProtectionService(ILogger<UserEnumerationProtectionService> logger, IMemoryCache memoryCache) : IUserEnumerationProtectionService
{
    // Consistent error message to prevent user enumeration
    private const string ConsistentErrorMessage = "Invalid credentials. Please check your email and password.";

    /// <summary>
    ///     Enumeration-safe response for anonymous email-verification requests. Known and unknown
    ///     accounts must receive this identical message, including when delivery boundaries fail.
    /// </summary>
    public const string GenericEmailVerificationMessage = "If an account exists with that email, a verification email has been sent";

    private static readonly Random Random = new Random();

    // Timing constants
    private static readonly TimeSpan MinProcessingTime = TimeSpan.FromMilliseconds(200);

    private static readonly TimeSpan MaxProcessingTime = TimeSpan.FromMilliseconds(800);

    private static readonly TimeSpan TargetProcessingTime = TimeSpan.FromMilliseconds(400);

    // Interface implementation methods

    public async Task AddTimingProtectionDelayAsync(bool isValidUser, DateTime startTime)
    {
        var stopwatch = Stopwatch.StartNew();
        var elapsed = SystemClock.UtcNow - startTime;

        try
        {
            // Calculate target delay based on whether user exists
            var targetDelay = TargetProcessingTime;

            // If user doesn't exist, simulate authentication work
            if (!isValidUser)
            {
                await Task.Delay(Random.Next(50, 150)).ConfigureAwait(false);
                await PerformDummyPasswordHashAsync("dummy_password_for_timing").ConfigureAwait(false);
                await Task.Delay(Random.Next(30, 100)).ConfigureAwait(false);
            }

            stopwatch.Stop();
            var totalElapsed = elapsed + stopwatch.Elapsed;

            // Add remaining delay to reach target
            var remainingDelay = targetDelay - totalElapsed;

            if (remainingDelay > TimeSpan.Zero) { await Task.Delay(remainingDelay).ConfigureAwait(false); }
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

    public async Task SimulateAuthenticationDelayAsync(string email, bool userExists)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Calculate a consistent delay based on email hash
            var targetDelay = CalculateConsistentDelay(email);

            // If user doesn't exist, we need to simulate the full authentication process
            if (!userExists)
            {
                // Simulate database lookup time
                await Task.Delay(Random.Next(50, 150)).ConfigureAwait(false);

                // Perform dummy password hashing to simulate verification
                await PerformDummyPasswordHashAsync("dummy_password_for_timing").ConfigureAwait(false);

                // Simulate additional processing
                await Task.Delay(Random.Next(30, 100)).ConfigureAwait(false);
            }

            stopwatch.Stop();

            // Calculate remaining delay needed to reach target
            var elapsed = stopwatch.Elapsed;
            var remainingDelay = targetDelay - elapsed;

            if (remainingDelay > TimeSpan.Zero) { await Task.Delay(remainingDelay).ConfigureAwait(false); }

            var totalTime = stopwatch.Elapsed + (remainingDelay > TimeSpan.Zero ? remainingDelay : TimeSpan.Zero);

            // Log timing analysis for security monitoring
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Authentication timing: EmailHash={EmailHash}, UserExists={UserExists}, ProcessingTime={ProcessingTimeMs}ms, TargetTime={TargetTimeMs}ms",
                    LogRedaction.RedactSecret(HashEmail(email)),
                    userExists,
                    totalTime.TotalMilliseconds,
                    targetDelay.TotalMilliseconds
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in authentication delay simulation");
            throw;
        }
    }

    public string GetConsistentErrorMessage() { return ConsistentErrorMessage; }

    public async Task PerformDummyPasswordHashAsync(string password)
    {
        // Perform actual BCrypt hashing to maintain realistic timing
        // Use a fixed salt to ensure consistent timing
        var dummySalt = "$2a$12$abcdefghijklmnopqrstuu"; // Fixed salt for dummy operations

        await Task.Run(() =>
            {
                try
                {
                    // Simulate BCrypt.HashPassword operation
                    BCrypt.Net.BCrypt.HashPassword(password, dummySalt);
                }
                catch
                {
                    // Ignore errors in dummy operation, just maintain timing
                    Thread.Sleep(Random.Next(100, 300));
                }
            }
        );
    }

    public TimeSpan GetBaseProcessingTime() { return TargetProcessingTime; }

    /// <summary>
    ///     Calculates a consistent delay based on email hash to prevent timing analysis
    /// </summary>
    private TimeSpan CalculateConsistentDelay(string email)
    {
        // Use deterministic hash to ensure same email always gets same delay
        var emailHash = HashEmail(email);
        var hashBytes = Convert.FromHexString(emailHash);

        // Use first 4 bytes of hash to determine delay within acceptable range
        var delayMs = BitConverter.ToUInt32(hashBytes, 0) % (uint) (MaxProcessingTime.TotalMilliseconds - MinProcessingTime.TotalMilliseconds) + (uint) MinProcessingTime.TotalMilliseconds;

        return TimeSpan.FromMilliseconds(delayMs);
    }

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
