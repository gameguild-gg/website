namespace GameGuild.Configuration.PresentationLayer.RateLimiting;

/// <summary>
///     Named rate limiting policies for the application.
///     Use these constants with [EnableRateLimiting] and [DisableRateLimiting] attributes.
/// </summary>
public static class RateLimitPolicies
{
    // ============ Fixed Window Policies (simple, predictable) ============

    /// <summary>
    ///     Strict rate limiting for authentication endpoints (sign-in, sign-up, password reset).
    ///     Algorithm: Fixed Window, Partitioned by: IP + User ID.
    ///     Default: 10 requests per minute.
    ///     Purpose: Prevent brute-force attacks and credential stuffing.
    /// </summary>
    public const string Authentication = "authentication";

    /// <summary>
    ///     Rate limiting for authorization/permission check endpoints.
    ///     Algorithm: Fixed Window, Partitioned by: User ID + Tenant ID.
    ///     Default: 100 requests per minute.
    ///     Purpose: Prevent DoS attacks on permission evaluation.
    /// </summary>
    public const string Authorization = "authorization";

    /// <summary>
    ///     Relaxed rate limiting for internal/admin endpoints.
    ///     Algorithm: Fixed Window, Partitioned by: User ID.
    ///     Default: 200 requests per minute.
    ///     Purpose: Allow higher throughput for admin operations.
    /// </summary>
    public const string Internal = "internal";

    // ============ Sliding Window Policies (smoother, prevents burst at window boundary) ============

    /// <summary>
    ///     General API rate limiting for all other endpoints.
    ///     Algorithm: Sliding Window, Partitioned by: User ID or IP.
    ///     Default: 60 requests per minute.
    ///     Purpose: Fair usage and protection against abuse.
    /// </summary>
    public const string Api = "api";

    /// <summary>
    ///     Per-tenant rate limiting to ensure tenant isolation.
    ///     Algorithm: Sliding Window, Partitioned by: Tenant ID.
    ///     Default: 1000 requests per minute.
    ///     Purpose: Prevent one tenant from impacting others.
    /// </summary>
    public const string PerTenant = "per-tenant";

    /// <summary>
    ///     Per-user rate limiting for authenticated users.
    ///     Algorithm: Sliding Window, Partitioned by: User ID.
    ///     Default: 300 requests per minute.
    ///     Purpose: Fair usage per individual user.
    /// </summary>
    public const string PerUser = "per-user";

    // ============ Token Bucket Policies (bursty traffic allowed) ============

    /// <summary>
    ///     Token bucket policy for bursty traffic patterns.
    ///     Algorithm: Token Bucket, Partitioned by: User ID or IP.
    ///     Default: 100 tokens max, 20 tokens replenished per 10 seconds.
    ///     Purpose: Allow bursts while maintaining average rate.
    /// </summary>
    public const string Bursty = "bursty";

    /// <summary>
    ///     API key rate limiting for external integrations.
    ///     Algorithm: Token Bucket, Partitioned by: API Key.
    ///     Default: Standard tier = 100/min, Premium tier = 1000/min.
    ///     Purpose: Tiered rate limiting for API consumers.
    /// </summary>
    public const string ApiKey = "api-key";

    // ============ Concurrency Limiting Policies (resource protection) ============

    /// <summary>
    ///     Concurrency limiting for expensive operations.
    ///     Algorithm: Concurrency Limiter, Partitioned by: User ID.
    ///     Default: 10 concurrent requests.
    ///     Purpose: Protect server resources from expensive operations (reports, exports).
    /// </summary>
    public const string ExpensiveOperations = "expensive-operations";

    // ============ Per-IP Policy (anonymous protection) ============

    /// <summary>
    ///     Per-IP rate limiting for anonymous/unauthenticated requests.
    ///     Algorithm: Sliding Window, Partitioned by: IP Address.
    ///     Default: 30 requests per minute.
    ///     Purpose: Protect against anonymous abuse.
    /// </summary>
    public const string PerIp = "per-ip";

    // ============ Webhook Callback Policy (provider flood protection) ============

    /// <summary>
    ///     Per-IP rate limiting for anonymous provider callback endpoints (e.g. billing webhooks).
    ///     Algorithm: Fixed Window, Partitioned by: IP Address.
    ///     Default: 120 requests per minute.
    ///     Purpose: Absorb legitimate provider retry bursts while throttling spoofed request
    ///     floods before payload signature verification runs.
    /// </summary>
    public const string Webhook = "webhook";
}
