using System.Diagnostics;
using GameGuild.Configuration.PresentationLayer;
using GameGuild.Configuration.PresentationLayer.GraphQL;
using GameGuild.Configuration.PresentationLayer.OpenAPI;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.API.Setup;

/// <summary>
///     Extension methods for configuring the presentation layer services.
///     Includes controllers, API versioning, CORS, and other web-specific services.
/// </summary>
public static class PresentationLayerExtensions
{
    #region WebApplicationBuilder Extensions

    /// <summary>
    ///     Registers services and configurations for the presentation layer with default options.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder instance</param>
    /// <returns>The WebApplicationBuilder for method chaining</returns>
    /// <exception cref="ArgumentNullException">Thrown when the builder is null</exception>
    public static WebApplicationBuilder AddPresentationLayer(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var logger = StartupLogger.Create();
        builder.Services.AddPresentationLayer(builder.Configuration, logger);

        return builder;
    }

    /// <summary>
    ///     Registers services and configurations for the presentation layer with custom options.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder instance</param>
    /// <param name="configureOptions">Action to configure presentation options</param>
    /// <returns>The WebApplicationBuilder for method chaining</returns>
    /// <exception cref="ArgumentNullException">Thrown when the builder or configureOptions is null</exception>
    public static WebApplicationBuilder AddPresentationLayer(this WebApplicationBuilder builder,
        Action<PresentationLayerOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var logger = StartupLogger.Create();
        builder.Services.AddPresentationLayer(builder.Configuration, logger, configureOptions);

        return builder;
    }

    #endregion

    #region IServiceCollection Extensions

    /// <summary>
    ///     Adds presentation layer services to the service collection with default options and logging.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <param name="configuration">The application configuration</param>
    /// <param name="logger">Logger for diagnostics</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddPresentationLayer(
        this IServiceCollection services,
        IConfiguration configuration,
        ILogger logger)
    {
        return services.AddPresentationLayer(configuration, logger, _ => { });
    }

    /// <summary>
    ///     Adds presentation layer services to the service collection with custom options and logging.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <param name="configuration">The application configuration</param>
    /// <param name="logger">Logger for diagnostics</param>
    /// <param name="configureOptions">Action to configure presentation options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddPresentationLayer(
        this IServiceCollection services,
        IConfiguration configuration,
        ILogger logger,
        Action<PresentationLayerOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Starting presentation layer setup...");

        var options = PresentationLayerOptionsBuilder.Create(configuration);
        configureOptions(options);
        options.Validate();

        var openApiOptions = options.OpenApi ?? OpenApiOptionsBuilder.Create(configuration);
        if (!options.EnableOpenApi)
        {
            openApiOptions.EnableOpenApi = false;
        }
        openApiOptions.Validate();
        services.TryAddSingleton(openApiOptions);

        // Presentation layer services registration order matters for some services.

        // 01. HttpContext access (required by auth, tenant, policy context provider)
        var stepStopwatch = Stopwatch.StartNew();
        services.AddHttpContextAccessor();
        logger.LogInformation("HttpContextAccessor registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 02. HTTP Logging (capture everything)
        if (options.EnableHttpLogging)
        {
            stepStopwatch.Restart();
            services.SetupHttpLogging(configuration, options.HttpLogging);
            logger.LogInformation("HttpLogging registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }

        // 03. Exception Handling/Problem Details (early error handling)
        if (options.EnableProblemDetails)
        {
            stepStopwatch.Restart();
            services.SetupProblemDetails(configuration, options.ProblemDetails);
            logger.LogInformation("ProblemDetails registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }

        // 04. Localization (early for error messages)
        if (options.EnableLocalization)
        {
            stepStopwatch.Restart();
            services.SetupLocalization(configuration, options.Localization);
            logger.LogInformation("Localization registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }

        // 05. Feature Flags (OpenFeature)
        if (options.EnableFeatureFlags)
        {
            stepStopwatch.Restart();
            services.SetupFeatureFlags(configuration, options.FeatureFlags);
            logger.LogInformation("FeatureFlags registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }

        // 06. Response Caching (after memory caching)
        if (options.EnableResponseCaching)
        {
            stepStopwatch.Restart();
            services.SetupResponseCaching(configuration, options.ResponseCaching);
            logger.LogInformation("ResponseCaching registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }

        // 07. Response Compression
        if (options.EnableResponseCompression)
        {
            stepStopwatch.Restart();
            services.SetupResponseCompression(configuration, options.ResponseCompression);
            logger.LogInformation("ResponseCompression registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }

        // 08. CORS (Cross-Origin Resource Sharing)
        if (options.EnableCors)
        {
            stepStopwatch.Restart();
            services.SetupCors(configuration, options.Cors);
            logger.LogInformation("CORS registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }

        // 09. Authentication (identify user, and tenant)
        if (options.EnableAuthentication)
        {
            stepStopwatch.Restart();
            services.SetupAuthentication(configuration, options.Authentication);
            logger.LogInformation("Authentication registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }

        // 10. Request Context (after authentication, unified request context handling)
        if (options.EnableRequestContext)
        {
            stepStopwatch.Restart();
            services.SetupRequestContext(configuration, options.RequestContext);
            logger.LogInformation("RequestContext registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }

        // 11. Authorization (after context is established and feature flags are checked)
        if (options.EnableAuthorization)
        {
            stepStopwatch.Restart();
            services.SetupAuthorization(configuration, options.Authorization);
            logger.LogInformation("Authorization registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }

        // 12. Rate Limiting
        if (options.EnableRateLimiting)
        {
            stepStopwatch.Restart();
            services.SetupRateLimiting(configuration, options.RateLimiting);
            logger.LogInformation("RateLimiting registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }
        else
        {
            services.SetupTrustedProxyForwarding(configuration, options.RateLimiting);
        }

        // 13. Model Validation
        if (options.EnableModelValidation)
        {
            stepStopwatch.Restart();
            services.SetupModelValidation(configuration, options.ModelValidation);
            logger.LogInformation("ModelValidation registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }

        // 14. API Versioning
        if (options.EnableApiVersioning)
        {
            stepStopwatch.Restart();
            services.SetupApiVersioning(configuration, options.ApiVersioning);
            logger.LogInformation("ApiVersioning registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }

        // 15. Health Checks
        if (options.EnableHealthChecks)
        {
            stepStopwatch.Restart();
            services.SetupHealthChecks(configuration, options.HealthChecks);
            logger.LogInformation("HealthChecks registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }

        // 16. Controllers/Endpoints
        if (options.EnableControllers)
        {
            stepStopwatch.Restart();
            services.SetupControllers(configuration, options.Controllers);
            logger.LogInformation("Controllers registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }

        // 17. Minimal API Endpoints
        if (options.EnableEndpoints)
        {
            stepStopwatch.Restart();
            services.SetupEndpoints(configuration, options.Endpoints);
            logger.LogInformation("Endpoints registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }

        // 18. SignalR (real-time communication)
        if (options.EnableSignalR)
        {
            stepStopwatch.Restart();
            services.SetupSignalR(configuration, options.SignalR);
            logger.LogInformation("SignalR registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }

        var graphQlOptions = options.GraphQL ?? GraphQLOptionsBuilder.Create(configuration);
        graphQlOptions.EnableGraphQL |= options.EnableGraphQL;
        if (graphQlOptions.EnableGraphQL)
        {
            if (!options.EnableAuthentication || !options.EnableAuthorization)
            {
                throw new InvalidOperationException("GraphQL requires both authentication and authorization to be enabled.");
            }

            stepStopwatch.Restart();
            services.SetupGraphQL(configuration, graphQlOptions);
            logger.LogInformation("GraphQL registered at {Endpoint} in {ElapsedMs}ms", graphQlOptions.Endpoint, stepStopwatch.ElapsedMilliseconds);
        }

        // 19. API Explorer - MUST be called AFTER controllers and application parts are registered
        if (options.EnableApiExplorer)
        {
            stepStopwatch.Restart();
            services.SetupApiExplorer(configuration, options.ApiVersioning);
            logger.LogInformation("ApiExplorer registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }

        // 20. OpenAPI/Swagger
        if (options.EnableOpenApi && openApiOptions.EnableOpenApi)
        {
            stepStopwatch.Restart();
            services.SetupOpenApi(configuration, openApiOptions);
            logger.LogInformation("OpenApi registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }

        // 21. Custom Middlewares (CorrelationId, SecurityHeaders, TenantResolution)
        stepStopwatch.Restart();
        services.SetupMiddlewares(configuration);
        logger.LogInformation("Middlewares registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        stopwatch.Stop();
        logger.LogInformation("Completed presentation layer setup in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

        return services;
    }

    #endregion
}
