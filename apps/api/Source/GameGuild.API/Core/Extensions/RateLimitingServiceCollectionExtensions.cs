using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using GameGuild.Configuration;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.API;

/// <summary>
///     Extension methods for configuring rate limiting services and policies.
/// </summary>
public static class RateLimitingServiceCollectionExtensions
{
    public static IServiceCollection SetupRateLimiting(this IServiceCollection services, IConfiguration configuration,
        RateLimitingOptions? options)
    {
        options ??= OptionBuilderUtilities.CreateAndBind(configuration, "RateLimiting",
            RateLimitingOptions.CreateDefault);
        options.Validate();
        var accessOptions = configuration.GetSection("RateLimiting:AccessControl").Get<RateLimitAccessOptions>()
            ?? new RateLimitAccessOptions();
        accessOptions.Validate();
        var redisEnabled = configuration.GetValue<bool>("Redis:Enabled");
        if (redisEnabled)
        {
            ValidateRedisWindows(options);
        }

        services.AddSingleton(options);
        services.AddSingleton(accessOptions);

        var trustedProxies = options.TrustedProxyAddresses.Select(IPAddress.Parse).Distinct().ToArray();
        services.PostConfigure<ForwardedHeadersOptions>(forwardedHeadersOptions =>
        {
            // Hosting-level settings can enable forwarded headers for every proxy. Replace those
            // settings so only the explicit RateLimiting:TrustedProxyAddresses list is trusted.
            forwardedHeadersOptions.ForwardedHeaders = trustedProxies.Length == 0
                ? ForwardedHeaders.None
                : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            forwardedHeadersOptions.ForwardLimit = options.TrustedProxyForwardLimit;
            forwardedHeadersOptions.KnownProxies.Clear();
            forwardedHeadersOptions.KnownIPNetworks.Clear();

            foreach (var trustedProxy in trustedProxies)
            {
                forwardedHeadersOptions.KnownProxies.Add(trustedProxy);
            }
        });

        services.AddRateLimiter(rateLimiterOptions =>
            {
                // Apply the configured global limit to every request. Authenticated traffic
                // is partitioned by user and anonymous traffic by the trusted client address.
                rateLimiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                {
                    if (accessOptions.IsAllowlisted(httpContext))
                    {
                        return RateLimitPartition.GetNoLimiter($"allowlisted:global:{GetUserOrIpPartitionKey(httpContext)}");
                    }

                    if (IsExemptPath(httpContext.Request.Path, options.ExemptPaths))
                    {
                        return RateLimitPartition.GetNoLimiter($"exempt:{httpContext.Request.Path}");
                    }

                    var partitionKey = GetUserOrIpPartitionKey(httpContext);
                    return redisEnabled
                        ? RateLimitPartition.GetNoLimiter($"redis:global:{partitionKey}")
                        : RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey,
                            _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = options.Limit,
                                Window = options.Period,
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                                QueueLimit = options.QueueLimit,
                                AutoReplenishment = true
                            });
                });

                // Global rejection handler for rate limit exceeded
                rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                rateLimiterOptions.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                    var policy = context.HttpContext.GetEndpoint()?.Metadata
                        .GetMetadata<EnableRateLimitingAttribute>()?.PolicyName ?? "global";
                    RateLimitingMetrics.RecordRejection(policy, "aspnetcore");

                    var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue)
                        ? retryAfterValue.TotalSeconds
                        : 60;

                    SetRateLimitHeaders(context.HttpContext.Response, GetRequestLimit(context.HttpContext, options), retryAfter);

                    context.HttpContext.RequestServices.GetService<ILoggerFactory>()?
                        .CreateLogger("GameGuild.API.RateLimiting")
                        .LogWarning("Rate limit exceeded for {Path} using policy {Policy}",
                            LogRedaction.Sanitize(context.HttpContext.Request.Path),
                            LogRedaction.Sanitize(policy));

                    var problemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too Many Requests",
                        Detail = $"Rate limit exceeded. Please retry after {retryAfter:F0} seconds.",
                        Instance = context.HttpContext.Request.Path
                    };

                    await RateLimitProblemDetailsWriter.WriteAsync(
                        context.HttpContext,
                        problemDetails,
                        cancellationToken).ConfigureAwait(false);
                };

                // ============ FIXED WINDOW POLICIES ============

                // Authentication policy: Partitioned by IP (anonymous) or User ID (authenticated)
                // 10 requests per minute to prevent brute-force attacks
                rateLimiterOptions.AddPolicy(RateLimitPolicies.Authentication, httpContext =>
                {
                    var partitionKey = GetAuthenticationPartitionKey(httpContext);
                    if (accessOptions.IsAllowlisted(httpContext))
                    {
                        return RateLimitPartition.GetNoLimiter($"allowlisted:{RateLimitPolicies.Authentication}:{partitionKey}");
                    }

                    return redisEnabled
                        ? RateLimitPartition.GetNoLimiter($"redis:{RateLimitPolicies.Authentication}:{partitionKey}")
                        : RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey,
                            _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = options.AuthenticationRequestsPerMinute,
                                Window = options.AuthenticationWindow,
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                                QueueLimit = options.QueueLimit
                            });
                });

                // Authorization policy: Partitioned by User ID + Tenant ID
                // 100 requests per minute to prevent DoS on permission evaluation
                rateLimiterOptions.AddPolicy(RateLimitPolicies.Authorization, httpContext =>
                {
                    var partitionKey = GetUserTenantPartitionKey(httpContext);
                    if (accessOptions.IsAllowlisted(httpContext))
                    {
                        return RateLimitPartition.GetNoLimiter($"allowlisted:{RateLimitPolicies.Authorization}:{partitionKey}");
                    }

                    return redisEnabled
                        ? RateLimitPartition.GetNoLimiter($"redis:{RateLimitPolicies.Authorization}:{partitionKey}")
                        : RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey,
                            _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = options.AuthorizationRequestsPerMinute,
                                Window = options.AuthorizationWindow,
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                                QueueLimit = options.QueueLimit
                            });
                });

                // Internal policy: Relaxed limits for admin/internal endpoints
                // Partitioned by User ID
                // 200 requests per minute
                rateLimiterOptions.AddPolicy(RateLimitPolicies.Internal, httpContext =>
                {
                    var partitionKey = GetUserPartitionKey(httpContext);
                    if (accessOptions.IsAllowlisted(httpContext))
                    {
                        return RateLimitPartition.GetNoLimiter($"allowlisted:{RateLimitPolicies.Internal}:{partitionKey}");
                    }

                    return redisEnabled
                        ? RateLimitPartition.GetNoLimiter($"redis:{RateLimitPolicies.Internal}:{partitionKey}")
                        : RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey,
                            _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = options.InternalRequestsPerMinute,
                                Window = options.InternalWindow,
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                                QueueLimit = options.QueueLimit * 2
                            });
                });

                // ============ SLIDING WINDOW POLICIES ============

                // API policy: General API endpoints with sliding window for smoother distribution
                // Partitioned by User ID (authenticated) or IP (anonymous)
                // 60 requests per minute for general API calls
                rateLimiterOptions.AddPolicy(RateLimitPolicies.Api, httpContext =>
                {
                    var partitionKey = GetUserOrIpPartitionKey(httpContext);
                    if (accessOptions.IsAllowlisted(httpContext))
                    {
                        return RateLimitPartition.GetNoLimiter($"allowlisted:{RateLimitPolicies.Api}:{partitionKey}");
                    }

                    return redisEnabled
                        ? RateLimitPartition.GetNoLimiter($"redis:{RateLimitPolicies.Api}:{partitionKey}")
                        : RateLimitPartition.GetSlidingWindowLimiter(
                            partitionKey,
                            _ => new SlidingWindowRateLimiterOptions
                            {
                                PermitLimit = options.ApiRequestsPerMinute,
                                Window = options.ApiWindow,
                                SegmentsPerWindow = options.SlidingWindowSegments,
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                                QueueLimit = options.QueueLimit
                            });
                });

                // Per-Tenant policy: Sliding window partitioned by Tenant ID
                // 1000 requests per minute per tenant
                rateLimiterOptions.AddPolicy(RateLimitPolicies.PerTenant, httpContext =>
                {
                    var partitionKey = GetTenantPartitionKey(httpContext);
                    if (accessOptions.IsAllowlisted(httpContext))
                    {
                        return RateLimitPartition.GetNoLimiter($"allowlisted:{RateLimitPolicies.PerTenant}:{partitionKey}");
                    }

                    return redisEnabled
                        ? RateLimitPartition.GetNoLimiter($"redis:{RateLimitPolicies.PerTenant}:{partitionKey}")
                        : RateLimitPartition.GetSlidingWindowLimiter(
                            partitionKey,
                            _ => new SlidingWindowRateLimiterOptions
                            {
                                PermitLimit = options.TenantRequestsPerMinute,
                                Window = options.TenantWindow,
                                SegmentsPerWindow = options.SlidingWindowSegments,
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                                QueueLimit = options.QueueLimit
                            });
                });

                // Per-User policy: Sliding window partitioned by User ID
                // 300 requests per minute per authenticated user
                rateLimiterOptions.AddPolicy(RateLimitPolicies.PerUser, httpContext =>
                {
                    var partitionKey = GetUserPartitionKey(httpContext);
                    if (accessOptions.IsAllowlisted(httpContext))
                    {
                        return RateLimitPartition.GetNoLimiter($"allowlisted:{RateLimitPolicies.PerUser}:{partitionKey}");
                    }

                    return redisEnabled
                        ? RateLimitPartition.GetNoLimiter($"redis:{RateLimitPolicies.PerUser}:{partitionKey}")
                        : RateLimitPartition.GetSlidingWindowLimiter(
                            partitionKey,
                            _ => new SlidingWindowRateLimiterOptions
                            {
                                PermitLimit = options.UserRequestsPerMinute,
                                Window = options.UserWindow,
                                SegmentsPerWindow = options.SlidingWindowSegments,
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                                QueueLimit = options.QueueLimit
                            });
                });

                // Per-IP policy: Sliding window partitioned by IP address for anonymous protection.
                rateLimiterOptions.AddPolicy(RateLimitPolicies.PerIp, httpContext =>
                {
                    var partitionKey = GetIpPartitionKey(httpContext);
                    if (accessOptions.IsAllowlisted(httpContext))
                    {
                        return RateLimitPartition.GetNoLimiter($"allowlisted:{RateLimitPolicies.PerIp}:{partitionKey}");
                    }

                    return redisEnabled
                        ? RateLimitPartition.GetNoLimiter($"redis:{RateLimitPolicies.PerIp}:{partitionKey}")
                        : RateLimitPartition.GetSlidingWindowLimiter(
                            partitionKey,
                            _ => new SlidingWindowRateLimiterOptions
                            {
                                PermitLimit = options.IpRequestsPerMinute,
                                Window = options.IpWindow,
                                SegmentsPerWindow = options.SlidingWindowSegments,
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                                QueueLimit = 0 // No queuing for per-IP to prevent resource exhaustion
                            });
                });

                // ============ TOKEN BUCKET POLICIES ============

                // Bursty policy: Token bucket for bursty traffic patterns
                // Partitioned by User ID or IP
                rateLimiterOptions.AddPolicy(RateLimitPolicies.Bursty, httpContext =>
                {
                    var partitionKey = GetUserOrIpPartitionKey(httpContext);
                    if (accessOptions.IsAllowlisted(httpContext))
                    {
                        return RateLimitPartition.GetNoLimiter($"allowlisted:{RateLimitPolicies.Bursty}:{partitionKey}");
                    }
                    if (redisEnabled)
                    {
                        return RateLimitPartition.GetNoLimiter($"redis:{RateLimitPolicies.Bursty}:{partitionKey}");
                    }

                    return RateLimitPartition.GetTokenBucketLimiter(
                        partitionKey: partitionKey,
                        factory: _ => new TokenBucketRateLimiterOptions
                        {
                            TokenLimit = options.TokenBucketLimit,
                            ReplenishmentPeriod = options.TokenReplenishmentPeriod,
                            TokensPerPeriod = options.TokensPerPeriod,
                            AutoReplenishment = true,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = options.QueueLimit
                        });
                });

                // API Key policy: Token bucket partitioned by API key with tiered limits
                rateLimiterOptions.AddPolicy(RateLimitPolicies.ApiKey, httpContext =>
                {
                    var partitionKey = GetApiKeyPartitionKey(httpContext);
                    if (accessOptions.IsAllowlisted(httpContext))
                    {
                        return RateLimitPartition.GetNoLimiter($"allowlisted:{RateLimitPolicies.ApiKey}:{partitionKey}");
                    }
                    if (redisEnabled)
                    {
                        return RateLimitPartition.GetNoLimiter($"redis:{RateLimitPolicies.ApiKey}:{partitionKey}");
                    }

                    return RateLimitPartition.GetTokenBucketLimiter(
                        partitionKey: partitionKey,
                        factory: partition => new TokenBucketRateLimiterOptions
                        {
                            TokenLimit = partition.StartsWith("premium:")
                                ? options.PremiumApiKeyRequestsPerMinute
                                : options.StandardApiKeyRequestsPerMinute,
                            ReplenishmentPeriod = options.ApiKeyWindow,
                            TokensPerPeriod = partition.StartsWith("premium:")
                                ? options.PremiumApiKeyRequestsPerMinute
                                : options.StandardApiKeyRequestsPerMinute,
                            AutoReplenishment = true,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = options.QueueLimit
                        });
                });

                // ============ CONCURRENCY POLICIES ============

                // Expensive Operations policy: Concurrency limiter for reports, exports, etc.
                // Partitioned by User ID to limit concurrent expensive operations per user
                rateLimiterOptions.AddPolicy(RateLimitPolicies.ExpensiveOperations, httpContext =>
                {
                    var partitionKey = GetUserPartitionKey(httpContext);
                    if (accessOptions.IsAllowlisted(httpContext))
                    {
                        return RateLimitPartition.GetNoLimiter($"allowlisted:{RateLimitPolicies.ExpensiveOperations}:{partitionKey}");
                    }

                    if (redisEnabled)
                    {
                        return RateLimitPartition.GetNoLimiter($"redis:{RateLimitPolicies.ExpensiveOperations}:{partitionKey}");
                    }

                    return RateLimitPartition.GetConcurrencyLimiter(
                        partitionKey: partitionKey,
                        factory: _ => new ConcurrencyLimiterOptions
                        {
                            PermitLimit = options.MaxConcurrentRequests,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = options.QueueLimit
                        });
                });

                foreach (var (policyName, policy) in options.Policies)
                {
                    rateLimiterOptions.AddPolicy(policyName, httpContext => CreateConfiguredPolicyPartition(
                        policyName,
                        policy,
                        httpContext,
                        redisEnabled,
                        accessOptions));
                }
            }
        );

        return services;
    }

    private static void ValidateRedisWindows(RateLimitingOptions options)
    {
        var windows = new (string Name, TimeSpan Value)[]
        {
            (nameof(options.Period), options.Period),
            (nameof(options.AuthenticationWindow), options.AuthenticationWindow),
            (nameof(options.AuthorizationWindow), options.AuthorizationWindow),
            (nameof(options.InternalWindow), options.InternalWindow),
            (nameof(options.ApiWindow), options.ApiWindow),
            (nameof(options.TenantWindow), options.TenantWindow),
            (nameof(options.UserWindow), options.UserWindow),
            (nameof(options.IpWindow), options.IpWindow),
            (nameof(options.ApiKeyWindow), options.ApiKeyWindow),
            (nameof(options.TokenReplenishmentPeriod), options.TokenReplenishmentPeriod)
        };

        foreach (var (name, window) in windows)
        {
            if (window.TotalMilliseconds < 1)
            {
                throw new InvalidOperationException(
                    $"Redis-backed rate limiting requires '{name}' to be at least one millisecond.");
            }
        }

        foreach (var (name, policy) in options.Policies)
        {
            if (policy.Window.TotalMilliseconds < 1)
            {
                throw new InvalidOperationException(
                    $"Redis-backed rate limiting requires policy '{name}' to have a window of at least one millisecond.");
            }
        }
    }

    private static RateLimitPartition<string> CreateConfiguredPolicyPartition(
        string policyName,
        RateLimitPolicyOptions policy,
        HttpContext httpContext,
        bool redisEnabled,
        RateLimitAccessOptions accessOptions)
    {
        var partitionKey = GetPartitionKey(httpContext, policy.PartitionBy);
        if (accessOptions.IsAllowlisted(httpContext) || redisEnabled)
        {
            return RateLimitPartition.GetNoLimiter($"{(redisEnabled ? "redis" : "allowlisted")}:{policyName}:{partitionKey}");
        }

        return policy.Algorithm switch
        {
            RateLimitingAlgorithm.FixedWindow => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = policy.PermitLimit,
                    Window = policy.Window,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = policy.QueueLimit,
                    AutoReplenishment = true
                }),
            RateLimitingAlgorithm.SlidingWindow => RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey,
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = policy.PermitLimit,
                    Window = policy.Window,
                    SegmentsPerWindow = policy.SlidingWindowSegments,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = policy.QueueLimit
                }),
            RateLimitingAlgorithm.TokenBucket => RateLimitPartition.GetTokenBucketLimiter(
                partitionKey,
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = policy.PermitLimit,
                    ReplenishmentPeriod = policy.Window,
                    TokensPerPeriod = policy.TokensPerPeriod,
                    AutoReplenishment = true,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = policy.QueueLimit
                }),
            _ => throw new InvalidOperationException($"Rate-limiting policy '{policyName}' has an unsupported algorithm.")
        };
    }

    internal static string GetPartitionKey(HttpContext httpContext, RateLimitPartitionStrategy partitionBy)
    {
        return partitionBy switch
        {
            RateLimitPartitionStrategy.Global => "global",
            RateLimitPartitionStrategy.User => GetUserPartitionKey(httpContext),
            RateLimitPartitionStrategy.Ip => GetIpPartitionKey(httpContext),
            RateLimitPartitionStrategy.UserOrIp => GetUserOrIpPartitionKey(httpContext),
            RateLimitPartitionStrategy.Tenant => GetTenantPartitionKey(httpContext),
            RateLimitPartitionStrategy.Endpoint => GetEndpointPartitionKey(httpContext),
            _ => throw new InvalidOperationException($"Unknown rate-limit partition strategy '{partitionBy}'.")
        };
    }

    internal static string GetEndpointPartitionKey(HttpContext httpContext)
    {
        var endpoint = httpContext.GetEndpoint();
        var route = endpoint is RouteEndpoint routeEndpoint
            ? routeEndpoint.RoutePattern.RawText
            : endpoint?.DisplayName ?? "unknown";

        return $"endpoint:{httpContext.Request.Method}:{route}";
    }

    #region Rate Limiting Partition Key Helpers

    /// <summary>
    /// Gets partition key based on IP for anonymous or User ID for authenticated.
    /// Used for authentication endpoints.
    /// </summary>
    internal static string GetAuthenticationPartitionKey(HttpContext httpContext)
    {
        var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        return $"ip:{GetClientIpAddress(httpContext)}";
    }

    /// <summary>
    /// Gets partition key based on User ID only.
    /// Falls back to IP for anonymous users.
    /// </summary>
    internal static string GetUserPartitionKey(HttpContext httpContext)
    {
        if (TryGetApiKeyId(httpContext, out var apiKeyId))
        {
            return $"api-key:{apiKeyId}";
        }

        var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        return $"anonymous:{GetClientIpAddress(httpContext)}";
    }

    /// <summary>
    /// Gets partition key based on User ID + Tenant ID.
    /// Used for authorization/permission check endpoints.
    /// </summary>
    internal static string GetUserTenantPartitionKey(HttpContext httpContext)
    {
        var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        return $"user:{userId}:{GetTenantPartitionKey(httpContext)}";
    }

    /// <summary>
    /// Gets partition key based on User ID (authenticated) or IP (anonymous).
    /// Used for general API rate limiting.
    /// </summary>
    internal static string GetUserOrIpPartitionKey(HttpContext httpContext)
    {
        if (TryGetApiKeyId(httpContext, out var apiKeyId))
        {
            return $"api-key:{apiKeyId}";
        }

        var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        return $"ip:{GetClientIpAddress(httpContext)}";
    }

    /// <summary>
    /// Gets partition key based on Tenant ID.
    /// Used for per-tenant rate limiting.
    /// </summary>
    internal static string GetTenantPartitionKey(HttpContext httpContext)
    {
        if (TryGetResolvedTenantId(httpContext, out var tenantId))
        {
            return $"tenant:{tenantId:D}";
        }

        // Tenant resolution runs before the rate limiter. Use only its validated context,
        // never the raw request header, and partition unresolved requests by client IP.
        return $"no-tenant:{GetClientIpAddress(httpContext)}";
    }

    /// <summary>
    /// Gets partition key based on IP address only.
    /// Used for per-IP rate limiting (anonymous protection).
    /// </summary>
    internal static string GetIpPartitionKey(HttpContext httpContext)
    {
        return $"ip:{GetClientIpAddress(httpContext)}";
    }

    /// <summary>
    /// Gets partition key based on API key from header.
    /// Returns tier prefix (premium: or standard:) for tiered limits.
    /// </summary>
    internal static string GetApiKeyPartitionKey(HttpContext httpContext)
    {
        if (!TryGetApiKeyId(httpContext, out var apiKeyId))
        {
            // Never partition on the raw credential. It is attacker-controlled until the
            // API-key authentication handler validates it and provides a stable key ID.
            return GetUserOrIpPartitionKey(httpContext);
        }

        var tier = string.Equals(httpContext.User.FindFirst("api_key_tier")?.Value, "premium",
            StringComparison.OrdinalIgnoreCase)
            ? "premium"
            : "standard";

        return $"{tier}:{apiKeyId}";
    }

    /// <summary>
    /// Gets the client IP address, handling proxies and load balancers.
    /// </summary>
    private static bool TryGetResolvedTenantId(HttpContext httpContext, out Guid tenantId)
    {
        if (httpContext.Items.TryGetValue(HttpContextKeys.AuthorizationTenantId, out var resolvedTenant) &&
            resolvedTenant is Guid value && value != Guid.Empty)
        {
            tenantId = value;
            return true;
        }

        tenantId = Guid.Empty;
        return false;
    }

    /// <summary>
    /// Gets the client address after the configured Forwarded Headers middleware has
    /// applied trusted proxy information.
    /// </summary>
    internal static string GetClientIpAddress(HttpContext httpContext)
    {
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    internal static bool IsExemptPath(PathString requestPath, IEnumerable<string> exemptPaths)
    {
        return exemptPaths.Any(path => requestPath.StartsWithSegments(new PathString(path)));
    }

    internal static void SetRateLimitHeaders(HttpResponse response, int limit, double retryAfterSeconds)
    {
        ArgumentNullException.ThrowIfNull(response);

        var retryAfter = Math.Max(0, (int)Math.Ceiling(retryAfterSeconds));
        response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);
        response.Headers["RateLimit-Limit"] = limit.ToString(CultureInfo.InvariantCulture);
        response.Headers["RateLimit-Remaining"] = "0";
        response.Headers["RateLimit-Reset"] = retryAfter.ToString(CultureInfo.InvariantCulture);
    }

    private static int GetRequestLimit(HttpContext httpContext, RateLimitingOptions options)
    {
        var policyName = httpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        return policyName switch
        {
            RateLimitPolicies.Authentication => options.AuthenticationRequestsPerMinute,
            RateLimitPolicies.Authorization => options.AuthorizationRequestsPerMinute,
            RateLimitPolicies.Internal => options.InternalRequestsPerMinute,
            RateLimitPolicies.Api => options.ApiRequestsPerMinute,
            RateLimitPolicies.PerTenant => options.TenantRequestsPerMinute,
            RateLimitPolicies.PerUser => options.UserRequestsPerMinute,
            RateLimitPolicies.PerIp => options.IpRequestsPerMinute,
            RateLimitPolicies.Bursty => options.TokenBucketLimit,
            RateLimitPolicies.ApiKey => GetApiKeyPartitionKey(httpContext).StartsWith("premium:", StringComparison.Ordinal)
                ? options.PremiumApiKeyRequestsPerMinute
                : options.StandardApiKeyRequestsPerMinute,
            RateLimitPolicies.ExpensiveOperations => options.MaxConcurrentRequests,
            _ => options.Limit
        };
    }

    private static bool TryGetApiKeyId(HttpContext httpContext, out string apiKeyId)
    {
        var claimValue = httpContext.User.FindFirst("api_key_id")?.Value;
        var authMethod = httpContext.User.FindFirst("auth_method")?.Value;
        if (!string.IsNullOrWhiteSpace(claimValue) &&
            string.Equals(authMethod, "api_key", StringComparison.Ordinal))
        {
            apiKeyId = claimValue;
            return true;
        }

        apiKeyId = string.Empty;
        return false;
    }

    #endregion
}
