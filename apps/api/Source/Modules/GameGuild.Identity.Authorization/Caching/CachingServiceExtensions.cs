using GameGuild.Configuration.PresentationLayer.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace GameGuild.Identity.Authorization.Caching;

/// <summary>
///     Extension methods for registering authorization caching services.
/// </summary>
public static class CachingServiceExtensions
{
    /// <summary>
    ///     Adds authorization caching services with optional Redis distributed cache.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional cache options configuration delegate.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    ///     <para>
    ///         This method configures a hybrid caching strategy:
    ///         <list type="bullet">
    ///             <item>L1 (IMemoryCache): Always enabled, fast per-instance cache</item>
    ///             <item>L2 (IDistributedCache): Optional Redis cache for multi-instance deployments</item>
    ///         </list>
    ///     </para>
    ///     <para>
    ///         To enable Redis:
    ///         <code>
    ///         services.AddAuthorizationRedisCache("localhost:6379");
    ///         services.AddAuthorizationCaching();
    ///         </code>
    ///     </para>
    /// </remarks>
    public static IServiceCollection AddAuthorizationCaching(
        this IServiceCollection services,
        Action<AuthorizationCacheOptions>? configureOptions = null)
    {
        // Configure options
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.AddOptions<AuthorizationCacheOptions>();
        }

        // Cache metrics (singleton for aggregated stats)
        services.AddSingleton<ICacheMetricsService>(sp => new CacheMetricsService(
            sp.GetRequiredService<IOptions<AuthorizationCacheOptions>>().Value.EnableMetrics));
        services.AddHostedService<PermissionCacheMetricsMonitor>();
        services.AddSingleton<IPermissionCachePopularityTracker, PermissionCachePopularityTracker>();
        services.AddSingleton<IPermissionCacheKeyTracker>(sp => new PermissionCacheKeyTracker(
            sp.GetRequiredService<IMemoryCache>(),
            sp.GetRequiredService<ICacheMetricsService>(),
            sp.GetRequiredService<IOptions<AuthorizationCacheOptions>>().Value.MaxL1CacheSize));

        // Hybrid cache (scoped to allow tenant-specific behavior)
        services.AddScoped<IHybridPermissionCache>(sp =>
        {
            var memoryCache = sp.GetRequiredService<IMemoryCache>();
            var options = sp.GetRequiredService<IOptions<AuthorizationCacheOptions>>();
            var metrics = sp.GetRequiredService<ICacheMetricsService>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HybridPermissionCache>>();
            var keyTracker = sp.GetRequiredService<IPermissionCacheKeyTracker>();

            // Only inject distributed cache if configured
            IDistributedCache? distributedCache = null;
            if (options.Value.UseDistributedCache)
            {
                distributedCache = sp.GetService<IDistributedCache>();
            }

            return new HybridPermissionCache(memoryCache, options, metrics, logger, distributedCache, keyTracker);
        });

        // Cache invalidation service (scoped)
        services.AddScoped<ICacheInvalidationService>(sp =>
        {
            var memoryCache = sp.GetRequiredService<IMemoryCache>();
            var versionStore = sp.GetRequiredService<ITenantSecurityVersionStore>();
            var hybridCache = sp.GetRequiredService<IHybridPermissionCache>();
            var metrics = sp.GetRequiredService<ICacheMetricsService>();
            var options = sp.GetRequiredService<IOptions<AuthorizationCacheOptions>>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CacheInvalidationService>>();
            var publisher = sp.GetService<ICacheInvalidationPublisher>();
            var keyTracker = sp.GetRequiredService<IPermissionCacheKeyTracker>();

            return new CacheInvalidationService(memoryCache, versionStore, hybridCache, metrics, options, logger, publisher, keyTracker);
        });

        return services;
    }

    /// <summary>
    ///     Adds Redis distributed cache for authorization (optional).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="redisConnectionString">Redis connection string.</param>
    /// <param name="instanceName">Redis instance name prefix.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    ///     Call this method BEFORE <see cref="AddAuthorizationCaching"/> if you want to use Redis.
    /// </remarks>
    public static IServiceCollection AddAuthorizationRedisCache(
        this IServiceCollection services,
        string redisConnectionString,
        string instanceName = "gg:auth:")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(redisConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = instanceName;
        });

        return services.AddAuthorizationRedisInvalidation(redisConnectionString, instanceName);
    }

    /// <summary>
    ///     Adds authorization Redis Pub/Sub invalidation using the host's existing distributed cache.
    /// </summary>
    public static IServiceCollection AddAuthorizationRedisInvalidation(
        this IServiceCollection services,
        string redisConnectionString,
        string instanceName = "gg:auth:")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(redisConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);

        services.Configure<AuthorizationCacheOptions>(options =>
        {
            options.UseDistributedCache = true;
            options.RedisConnectionString = redisConnectionString;
            options.RedisInstanceName = instanceName;
        });
        services.PostConfigure<AuthorizationCacheOptions>(options =>
        {
            if (options.UseDistributedCache && options.UsePubSubInvalidation &&
                string.IsNullOrWhiteSpace(options.InvalidationChannelName))
            {
                throw new InvalidOperationException(
                    "InvalidationChannelName is required when Redis Pub/Sub invalidation is enabled.");
            }
        });

        services.TryAddSingleton<IConnectionMultiplexer>(sp =>
        {
            var configuration = ConfigurationOptions.Parse(redisConnectionString);
            configuration.AbortOnConnectFail = false;
            ApplyRedisTopology(configuration, sp.GetRequiredService<IOptions<AuthorizationCacheOptions>>().Value);
            return ConnectionMultiplexer.Connect(configuration);
        });
        services.TryAddSingleton<ICacheInvalidationPublisher, RedisPermissionCacheInvalidationPublisher>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService,
            RedisPermissionCacheInvalidationSubscriber>());

        return services;
    }

    /// <summary>
    ///     Applies the configured Redis topology and failover pass-through settings to a parsed
    ///     <see cref="ConfigurationOptions"/>. Values left unset (<c>null</c>) do not override the
    ///     connection string or client defaults.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Wired:</b> <see cref="AuthorizationCacheOptions.RedisServiceName"/> (Sentinel
    ///         master set name — list the Sentinel endpoints themselves in the connection string),
    ///         <see cref="AuthorizationCacheOptions.RedisProxy"/> (<c>None</c>/<c>Twemproxy</c>/
    ///         <c>Envoyproxy</c>), <see cref="AuthorizationCacheOptions.RedisConnectTimeoutMilliseconds"/>,
    ///         and <see cref="AuthorizationCacheOptions.RedisConnectRetry"/> (failover retries; the
    ///         multiplexer is always built with <c>AbortOnConnectFail = false</c>).
    ///     </para>
    ///     <para>
    ///         <b>Cluster:</b> no dedicated option exists because StackExchange.Redis follows
    ///         MOVED/ASK redirects automatically — list the cluster endpoints comma-separated in
    ///         the connection string.
    ///     </para>
    ///     <para>
    ///         <b>Deferred:</b> TLS client-certificate selection and per-endpoint Sentinel
    ///         credentials are not exposed here; configure them through the connection string.
    ///     </para>
    /// </remarks>
    public static void ApplyRedisTopology(ConfigurationOptions configuration, AuthorizationCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.RedisServiceName))
        {
            configuration.ServiceName = options.RedisServiceName.Trim();
        }

        if (options.RedisProxy is not null)
        {
            if (!Enum.TryParse<Proxy>(options.RedisProxy.Trim(), ignoreCase: true, out var proxy) ||
                !Enum.IsDefined(proxy))
            {
                throw new InvalidOperationException(
                    $"RedisProxy '{options.RedisProxy}' is not supported. Use None, Twemproxy, or Envoyproxy.");
            }

            configuration.Proxy = proxy;
        }

        if (options.RedisConnectTimeoutMilliseconds is > 0)
        {
            configuration.ConnectTimeout = options.RedisConnectTimeoutMilliseconds.Value;
        }

        if (options.RedisConnectRetry is >= 0)
        {
            configuration.ConnectRetry = options.RedisConnectRetry.Value;
        }
    }
}
