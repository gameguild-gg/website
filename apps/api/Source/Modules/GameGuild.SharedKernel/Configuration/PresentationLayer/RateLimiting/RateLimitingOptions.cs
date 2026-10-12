using System.Net;

namespace GameGuild.Configuration.PresentationLayer.RateLimiting;

public enum RateLimitingAlgorithm
{
    FixedWindow,
    SlidingWindow,
    TokenBucket
}

public enum RateLimitPartitionStrategy
{
    Global,
    User,
    Ip,
    UserOrIp,
    Tenant,
    Endpoint
}

public enum RedisRateLimitFailureMode
{
    FailOpen,
    FailClosed
}

/// <summary>
///     A named, endpoint-selectable rate limit policy.
/// </summary>
public sealed class RateLimitPolicyOptions
{
    public RateLimitingAlgorithm Algorithm { get; set; } = RateLimitingAlgorithm.FixedWindow;

    public RateLimitPartitionStrategy PartitionBy { get; set; } = RateLimitPartitionStrategy.UserOrIp;

    public int PermitLimit { get; set; } = 60;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    public int QueueLimit { get; set; }

    public int SlidingWindowSegments { get; set; } = 4;

    public int TokensPerPeriod { get; set; } = 10;

    internal void Validate(string policyName)
    {
        if (PermitLimit <= 0)
        {
            throw new InvalidOperationException($"Rate-limiting policy '{policyName}' must have a positive permit limit.");
        }

        if (Window <= TimeSpan.Zero)
        {
            throw new InvalidOperationException($"Rate-limiting policy '{policyName}' must have a positive window.");
        }

        if (QueueLimit < 0)
        {
            throw new InvalidOperationException($"Rate-limiting policy '{policyName}' cannot have a negative queue limit.");
        }

        if (SlidingWindowSegments <= 0)
        {
            throw new InvalidOperationException($"Rate-limiting policy '{policyName}' must have a positive sliding-window segment count.");
        }

        if (TokensPerPeriod <= 0)
        {
            throw new InvalidOperationException($"Rate-limiting policy '{policyName}' must replenish a positive number of tokens.");
        }

        if (!Enum.IsDefined(Algorithm))
        {
            throw new InvalidOperationException($"Rate-limiting policy '{policyName}' has an unknown algorithm.");
        }

        if (!Enum.IsDefined(PartitionBy))
        {
            throw new InvalidOperationException($"Rate-limiting policy '{policyName}' has an unknown partition strategy.");
        }
    }
}

/// <summary>
///     Configuration options for rate limiting policies.
///     Supports multiple partitioning strategies (IP, User, Tenant, API Key)
///     and multiple algorithms (Fixed Window, Sliding Window, Token Bucket, Concurrency).
/// </summary>
public sealed class RateLimitingOptions : BaseOptions
{
    /// <summary>
    ///     The configuration section name for this options type.
    /// </summary>
    public const string SectionName = "RateLimiting";

    /// <summary>
    ///     Whether rate limiting is enabled globally.
    /// </summary>
    public bool EnableRateLimiting { get; set; } = false;

    /// <summary>
    /// Determines whether Redis-backed admission allows or rejects requests when Redis is unavailable.
    /// FailOpen preserves availability; FailClosed returns 503 until the rate-limit store recovers.
    /// </summary>
    public RedisRateLimitFailureMode RedisFailureMode { get; set; } = RedisRateLimitFailureMode.FailOpen;

    /// <summary>
    ///     Default limit for requests (used by global policy).
    /// </summary>
    public int Limit { get; set; } = 100;

    /// <summary>
    ///     Default time period for rate limiting.
    /// </summary>
    public TimeSpan Period { get; set; } = TimeSpan.FromMinutes(1);

    // Compatibility properties
    public int RequestsPerMinute { get; set; } = 60;

    public int BurstSize { get; set; } = 10;

    public string[] ExemptPaths { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Exact proxy IP addresses permitted to supply X-Forwarded-For and X-Forwarded-Proto values.
    ///     Forwarded client addresses and external request schemes are ignored unless they arrive from
    ///     one of these proxies.
    /// </summary>
    public string[] TrustedProxyAddresses { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Maximum number of trusted proxy hops to read from forwarded client-address and scheme chains.
    /// </summary>
    public int TrustedProxyForwardLimit { get; set; } = 1;

    /// <summary>
    ///     Additional endpoint-selectable policies keyed by the name used in EnableRateLimiting.
    /// </summary>
    public Dictionary<string, RateLimitPolicyOptions> Policies { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Authentication endpoint rate limit (requests per minute).
    ///     Default: 10 req/min to prevent brute-force attacks.
    ///     Partitioned by: IP address (anonymous) or User ID (authenticated).
    /// </summary>
    public int AuthenticationRequestsPerMinute { get; set; } = 10;

    public TimeSpan AuthenticationWindow { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    ///     Authorization/permission check endpoint rate limit (requests per minute).
    ///     Default: 100 req/min for normal permission checks.
    ///     Partitioned by: User ID + Tenant ID.
    /// </summary>
    public int AuthorizationRequestsPerMinute { get; set; } = 100;

    public TimeSpan AuthorizationWindow { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    ///     API endpoint rate limit (requests per minute).
    ///     Default: 60 req/min for general API calls.
    ///     Partitioned by: User ID (authenticated) or IP (anonymous).
    /// </summary>
    public int ApiRequestsPerMinute { get; set; } = 60;

    public TimeSpan ApiWindow { get; set; } = TimeSpan.FromMinutes(1);

    public int IpRequestsPerMinute { get; set; } = 30;

    public TimeSpan IpWindow { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    ///     Provider callback endpoint rate limit (requests per minute), applied by the
    ///     <see cref="RateLimitPolicies.Webhook"/> policy on anonymous webhook endpoints.
    ///     Default: 120 req/min to absorb provider retry bursts while throttling spoofed floods.
    ///     Partitioned by: IP address.
    /// </summary>
    public int WebhookRequestsPerMinute { get; set; } = 120;

    public TimeSpan WebhookWindow { get; set; } = TimeSpan.FromMinutes(1);

    public int SlidingWindowSegments { get; set; } = 4;

    /// <summary>
    ///     Queue limit for requests that exceed the rate limit.
    ///     Requests beyond this are rejected immediately.
    /// </summary>
    public int QueueLimit { get; set; } = 2;

    // ============ Per-Tenant Rate Limiting ============

    /// <summary>
    ///     Per-tenant rate limit (requests per minute).
    ///     Default: 1000 req/min per tenant to prevent one tenant from impacting others.
    /// </summary>
    public int TenantRequestsPerMinute { get; set; } = 1000;

    public TimeSpan TenantWindow { get; set; } = TimeSpan.FromMinutes(1);

    // ============ Per-User Rate Limiting ============

    /// <summary>
    ///     Per-user rate limit (requests per minute).
    ///     Default: 300 req/min per authenticated user.
    /// </summary>
    public int UserRequestsPerMinute { get; set; } = 300;

    public TimeSpan UserWindow { get; set; } = TimeSpan.FromMinutes(1);

    // ============ API Key Rate Limiting ============

    /// <summary>
    ///     Standard API key rate limit (requests per minute).
    ///     Default: 100 req/min for standard tier API keys.
    /// </summary>
    public int StandardApiKeyRequestsPerMinute { get; set; } = 100;

    /// <summary>
    ///     Premium API key rate limit (requests per minute).
    ///     Default: 1000 req/min for premium tier API keys.
    /// </summary>
    public int PremiumApiKeyRequestsPerMinute { get; set; } = 1000;

    public TimeSpan ApiKeyWindow { get; set; } = TimeSpan.FromMinutes(1);

    public int InternalRequestsPerMinute { get; set; } = 200;

    public TimeSpan InternalWindow { get; set; } = TimeSpan.FromMinutes(1);

    // ============ Token Bucket Settings ============

    /// <summary>
    ///     Token bucket limit (max tokens).
    ///     Used for bursty traffic patterns.
    /// </summary>
    public int TokenBucketLimit { get; set; } = 100;

    /// <summary>
    ///     Token bucket replenishment period.
    /// </summary>
    public TimeSpan TokenReplenishmentPeriod { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Tokens added per replenishment period.
    /// </summary>
    public int TokensPerPeriod { get; set; } = 20;

    // ============ Concurrency Limiting ============

    /// <summary>
    ///     Maximum concurrent requests allowed.
    ///     Used for expensive operations (reports, exports).
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 10;

    /// <summary>
    ///     Lease duration for distributed concurrency permits. Active requests renew their lease;
    ///     expiration recovers permits left behind by a process that stopped unexpectedly.
    /// </summary>
    public TimeSpan ConcurrencyLeaseDuration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    ///     Applies escalating temporary blocks after repeated rate-limit rejections.
    /// </summary>
    public bool EnableProgressivePenalties { get; set; }

    public int PenaltyViolationThreshold { get; set; } = 5;

    public TimeSpan PenaltyDecayWindow { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan PenaltyBaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan PenaltyMaxDuration { get; set; } = TimeSpan.FromMinutes(15);

    public override void Validate()
    {
        base.Validate();

        if (!Enum.IsDefined(RedisFailureMode))
        {
            throw new InvalidOperationException("Rate-limiting option 'RedisFailureMode' must be FailOpen or FailClosed.");
        }

        if (Limit <= 0)
        {
            throw new InvalidOperationException("The global rate limit must be greater than zero.");
        }

        if (Period <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("The global rate-limit period must be greater than zero.");
        }

        if (QueueLimit < 0)
        {
            throw new InvalidOperationException("Rate-limiting option 'QueueLimit' must not be negative.");
        }

        ValidatePositive(AuthenticationRequestsPerMinute, nameof(AuthenticationRequestsPerMinute));
        ValidatePositive(AuthorizationRequestsPerMinute, nameof(AuthorizationRequestsPerMinute));
        ValidatePositive(ApiRequestsPerMinute, nameof(ApiRequestsPerMinute));
        ValidatePositive(IpRequestsPerMinute, nameof(IpRequestsPerMinute));
        ValidatePositive(WebhookRequestsPerMinute, nameof(WebhookRequestsPerMinute));
        ValidatePositive(TenantRequestsPerMinute, nameof(TenantRequestsPerMinute));
        ValidatePositive(UserRequestsPerMinute, nameof(UserRequestsPerMinute));
        ValidatePositive(StandardApiKeyRequestsPerMinute, nameof(StandardApiKeyRequestsPerMinute));
        ValidatePositive(PremiumApiKeyRequestsPerMinute, nameof(PremiumApiKeyRequestsPerMinute));
        ValidatePositive(InternalRequestsPerMinute, nameof(InternalRequestsPerMinute));
        ValidatePositive(TokenBucketLimit, nameof(TokenBucketLimit));
        ValidatePositive(TokensPerPeriod, nameof(TokensPerPeriod));
        ValidatePositive(MaxConcurrentRequests, nameof(MaxConcurrentRequests));
        ValidatePositive(AuthenticationWindow, nameof(AuthenticationWindow));
        ValidatePositive(AuthorizationWindow, nameof(AuthorizationWindow));
        ValidatePositive(ApiWindow, nameof(ApiWindow));
        ValidatePositive(IpWindow, nameof(IpWindow));
        ValidatePositive(WebhookWindow, nameof(WebhookWindow));
        ValidatePositive(TenantWindow, nameof(TenantWindow));
        ValidatePositive(UserWindow, nameof(UserWindow));
        ValidatePositive(ApiKeyWindow, nameof(ApiKeyWindow));
        ValidatePositive(InternalWindow, nameof(InternalWindow));
        ValidatePositive(TokenReplenishmentPeriod, nameof(TokenReplenishmentPeriod));
        ValidatePositive(ConcurrencyLeaseDuration, nameof(ConcurrencyLeaseDuration));
        ValidatePositive(PenaltyViolationThreshold, nameof(PenaltyViolationThreshold));
        ValidatePositive(PenaltyDecayWindow, nameof(PenaltyDecayWindow));
        ValidatePositive(PenaltyBaseDuration, nameof(PenaltyBaseDuration));
        ValidatePositive(PenaltyMaxDuration, nameof(PenaltyMaxDuration));
        if (PenaltyMaxDuration < PenaltyBaseDuration)
        {
            throw new InvalidOperationException("Rate-limiting option 'PenaltyMaxDuration' must be greater than or equal to 'PenaltyBaseDuration'.");
        }

        if (SlidingWindowSegments <= 0)
        {
            throw new InvalidOperationException("Rate-limiting option 'SlidingWindowSegments' must be greater than zero.");
        }

        if (RequestsPerMinute <= 0)
        {
            throw new InvalidOperationException("Rate-limiting option 'RequestsPerMinute' must be greater than zero.");
        }

        if (BurstSize <= 0)
        {
            throw new InvalidOperationException("Rate-limiting option 'BurstSize' must be greater than zero.");
        }

        if (ExemptPaths is null || ExemptPaths.Any(path => string.IsNullOrWhiteSpace(path) || !path.StartsWith("/", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Rate-limiting exempt paths must be non-empty paths beginning with '/'.");
        }

        if (TrustedProxyAddresses is null || TrustedProxyAddresses.Any(address =>
                string.IsNullOrWhiteSpace(address) || !IPAddress.TryParse(address, out _)))
        {
            throw new InvalidOperationException("Rate-limiting trusted proxy addresses must be valid IP addresses.");
        }

        if (TrustedProxyForwardLimit <= 0)
        {
            throw new InvalidOperationException("Rate-limiting trusted proxy forward limit must be greater than zero.");
        }

        if (Policies is null)
        {
            throw new InvalidOperationException("Rate-limiting policies cannot be null.");
        }

        var builtInPolicyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            RateLimitPolicies.Authentication,
            RateLimitPolicies.Authorization,
            RateLimitPolicies.Internal,
            RateLimitPolicies.Api,
            RateLimitPolicies.PerTenant,
            RateLimitPolicies.PerUser,
            RateLimitPolicies.Bursty,
            RateLimitPolicies.ApiKey,
            RateLimitPolicies.ExpensiveOperations,
            RateLimitPolicies.PerIp,
            RateLimitPolicies.Webhook
        };
        var configuredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, policy) in Policies)
        {
            if (string.IsNullOrWhiteSpace(name) || !configuredNames.Add(name))
            {
                throw new InvalidOperationException("Rate-limiting policy names must be non-empty and unique, ignoring case.");
            }

            if (builtInPolicyNames.Contains(name))
            {
                throw new InvalidOperationException($"Rate-limiting policy name '{name}' is reserved for a built-in policy.");
            }

            if (policy is null)
            {
                throw new InvalidOperationException($"Rate-limiting policy '{name}' cannot be null.");
            }

            policy.Validate(name);
        }
    }

    private static void ValidatePositive(int value, string optionName)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException($"Rate-limiting option '{optionName}' must be greater than zero.");
        }
    }

    private static void ValidatePositive(TimeSpan value, string optionName)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new InvalidOperationException($"Rate-limiting option '{optionName}' must be greater than zero.");
        }
    }

    public static RateLimitingOptions CreateDefault() { return new RateLimitingOptions(); }
}
